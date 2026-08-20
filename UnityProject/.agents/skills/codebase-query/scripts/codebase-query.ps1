# codebase-query-entrypoint
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('build', 'status', 'architecture', 'search', 'callers', 'impact', 'changed')]
    [string]$Command = 'status',

    [string]$Query,
    [string]$Path,
    [string]$Root = (Get-Location).Path,
    [string]$IndexPath = '.agent-memory/zworkflow/local/code-query-index.json',
    [string]$ProgressPath = '.agent-memory/zworkflow/local/code-query-progress.json',
    [string[]]$SourceRoots = @(),
    [string[]]$ExcludeRoots = @(),
    [switch]$IncludeAll,
    [ValidateRange(1, 200)]
    [int]$Limit = 30,
    [switch]$Pretty
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:IndexVersion = 3
$script:PathComparison = if ($IsWindows) {
    [StringComparison]::OrdinalIgnoreCase
}
else {
    [StringComparison]::Ordinal
}
$script:PathComparer = if ($IsWindows) {
    [StringComparer]::OrdinalIgnoreCase
}
else {
    [StringComparer]::Ordinal
}

function Find-CodebaseQuerySkillRoot {
    param([string]$ProjectRoot)

    $candidates = [System.Collections.Generic.List[string]]::new()
    $cursor = [System.IO.DirectoryInfo]::new($PSScriptRoot)
    while ($null -ne $cursor) {
        $candidates.Add($cursor.FullName)
        $cursor = $cursor.Parent
    }
    $candidates.Add((Join-Path $ProjectRoot '.agents/skills/codebase-query'))

    foreach ($candidate in @($candidates | Select-Object -Unique)) {
        $skillFile = Join-Path $candidate 'SKILL.md'
        if (-not (Test-Path -LiteralPath $skillFile -PathType Leaf)) { continue }
        if (Select-String -LiteralPath $skillFile -Pattern '^name:\s*codebase-query\s*$' -Quiet) {
            return $candidate
        }
    }
    throw 'Cannot locate the codebase-query skill root. Keep implementation scripts inside the installed skill directory or pass the target project with -Root.'
}

$initialProjectRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
if ($IncludeAll) { $ExcludeRoots = [string[]]::new(0) }
$script:CodebaseQuerySkillRoot = Find-CodebaseQuerySkillRoot -ProjectRoot $initialProjectRoot
$bindingCandidates = @(Get-ChildItem -LiteralPath $script:CodebaseQuerySkillRoot -Recurse -Filter '*.ps1' -File |
    Where-Object { $_.FullName -ne $PSCommandPath -and
        (Select-String -LiteralPath $_.FullName -Pattern '^# codebase-query-binding-library\s*$' -Quiet) })
if ($bindingCandidates.Count -ne 1) {
    throw "Expected exactly one codebase-query binding library under $script:CodebaseQuerySkillRoot; found $($bindingCandidates.Count)."
}
. $bindingCandidates[0].FullName

function Convert-ToRelativePath {
    param([string]$BasePath, [string]$TargetPath)

    $base = [System.IO.Path]::GetFullPath($BasePath)
    $target = [System.IO.Path]::GetFullPath($TargetPath)
    return [System.IO.Path]::GetRelativePath($base, $target).Replace('\', '/')
}

function Convert-ToPortablePath {
    param([string]$Path)

    $value = ($Path ?? '').Replace('\', '/')
    while ($value.StartsWith('./', [StringComparison]::Ordinal)) {
        $value = $value.Substring(2)
    }
    return $value.TrimStart('/')
}

function Write-CodeQueryProgress {
    param(
        [string]$Stage,
        [int]$Completed,
        [int]$Total,
        [string]$Message
    )

    if ([string]::IsNullOrWhiteSpace($script:ResolvedProgressPath)) { return }
    $directory = Split-Path -Parent $script:ResolvedProgressPath
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    $value = [ordered]@{
        stage = $Stage
        completed = $Completed
        total = $Total
        percent = if ($Total -gt 0) { [Math]::Round($Completed * 100.0 / $Total, 1) } else { 0 }
        message = $Message
        updatedAtUtc = [DateTime]::UtcNow.ToString('o')
    }
    [System.IO.File]::WriteAllText($script:ResolvedProgressPath, ($value | ConvertTo-Json -Compress), [System.Text.UTF8Encoding]::new($false))
}

function Resolve-IndexedPath {
    param(
        [string]$ProjectRoot,
        [string]$RelativePath
    )

    $nativeRelativePath = (Convert-ToPortablePath -Path $RelativePath).Replace(
        '/',
        [System.IO.Path]::DirectorySeparatorChar.ToString())
    return [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot $nativeRelativePath))
}

function Test-PathInsideProject {
    param(
        [string]$ProjectRoot,
        [string]$CandidatePath
    )

    $relative = [System.IO.Path]::GetRelativePath($ProjectRoot, $CandidatePath).Replace('\', '/')
    return -not [System.IO.Path]::IsPathRooted($relative) -and
        $relative -ne '..' -and
        -not $relative.StartsWith('../', [StringComparison]::Ordinal)
}

function Test-PortablePathInCollection {
    param(
        [string]$Path,
        [string[]]$Candidates
    )

    return @($Candidates | Where-Object { $_.Equals($Path, $script:PathComparison) }).Count -gt 0
}

function Get-SourceFiles {
    param(
        [string]$ProjectRoot,
        [string[]]$ConfiguredSourceRoots,
        [string[]]$ConfiguredExcludeRoots
    )

    $roots = if (@($ConfiguredSourceRoots).Count -gt 0) {
        @($ConfiguredSourceRoots)
    }
    else {
        @('Assets')
    }

    $resolvedRoots = [System.Collections.Generic.List[string]]::new()
    foreach ($sourceRoot in $roots) {
        $candidate = if ([System.IO.Path]::IsPathRooted($sourceRoot)) {
            [System.IO.Path]::GetFullPath($sourceRoot)
        }
        else {
            [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot $sourceRoot))
        }
        if (-not (Test-PathInsideProject -ProjectRoot $ProjectRoot -CandidatePath $candidate)) {
            throw "Source root must stay inside the project: $candidate"
        }
        if (-not (Test-Path -LiteralPath $candidate -PathType Container)) {
            throw "C# source root not found: $candidate"
        }
        $resolvedRoots.Add($candidate)
    }

    $resolvedExclusions = if (@($ConfiguredExcludeRoots).Count -gt 0) { @($ConfiguredExcludeRoots | ForEach-Object {
        if ([System.IO.Path]::IsPathRooted($_)) { [System.IO.Path]::GetFullPath($_) }
        else { [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot $_)) }
    }) } else { @() }

    $files = @($resolvedRoots | ForEach-Object {
        Get-ChildItem -LiteralPath $_ -Recurse -Filter '*.cs' -File
    } | Where-Object {
        $filePath = $_.FullName
        -not @($resolvedExclusions | Where-Object {
            $prefix = $_.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
            $filePath.StartsWith($prefix, $script:PathComparison)
        }).Count
    } | Sort-Object FullName -Unique)
    if ($files.Count -eq 0) {
        throw "No C# files found under: $($resolvedRoots -join ', ')"
    }
    return $files
}

function Get-SourceSignature {
    param(
        [string]$ProjectRoot,
        [System.IO.FileInfo[]]$Files
    )

    # Include relative paths so a pure rename/move cannot reuse stale locations.
    $entries = @($Files | ForEach-Object {
        $relativePath = Convert-ToRelativePath -BasePath $ProjectRoot -TargetPath $_.FullName
        "$relativePath|$($_.LastWriteTimeUtc.Ticks)|$($_.Length)"
    } | Sort-Object)
    $bytes = [Text.Encoding]::UTF8.GetBytes(($entries -join "`n"))
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($sha256.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-RegexValues {
    param(
        [string]$Text,
        [string]$Pattern,
        [int]$Group = 1
    )

    return @([regex]::Matches($Text, $Pattern) |
        ForEach-Object { $_.Groups[$Group].Value } |
        Where-Object { $_ } |
        Sort-Object -Unique)
}

function New-CodeIndex {
    param(
        [string]$ProjectRoot,
        [string]$ResolvedIndexPath,
        [System.IO.FileInfo[]]$SourceFiles,
        [string[]]$ConfiguredSourceRoots,
        [string[]]$ConfiguredExcludeRoots
    )

    $keywords = @(
        'if', 'for', 'foreach', 'while', 'switch', 'catch', 'using', 'lock', 'nameof',
        'typeof', 'sizeof', 'default', 'checked', 'unchecked', 'return', 'new', 'base',
        'this', 'get', 'set', 'add', 'remove'
    )
    $keywordSet = @{}
    foreach ($keyword in $keywords) { $keywordSet[$keyword] = $true }

    $records = [System.Collections.Generic.List[object]]::new()
    $parsedFileCount = 0
    foreach ($file in $SourceFiles) {
        $text = Get-Content -Raw -LiteralPath $file.FullName -Encoding utf8
        $namespaces = Get-RegexValues -Text $text -Pattern '(?m)^\s*namespace\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)'

        $typeMatches = [regex]::Matches(
            $text,
            '(?m)^\s*(?:(?:public|internal|protected|private|static|abstract|sealed|partial|readonly|ref|new)\s+)*(class|interface|struct|enum|record)\s+([A-Za-z_]\w*)'
        )
        $types = @($typeMatches | ForEach-Object {
            [pscustomobject]@{ kind = $_.Groups[1].Value; name = $_.Groups[2].Value }
        })

        $methodPattern = '(?m)^\s*(?:(?:public|private|protected|internal|static|virtual|override|abstract|sealed|async|extern|new|partial|unsafe)\s+)+[A-Za-z_][A-Za-z0-9_<>,\[\].?]*\s+([A-Za-z_]\w*)(?:\s*<[^;{}()]+>)?\s*\('
        $methods = @(Get-RegexValues -Text $text -Pattern $methodPattern)

        $constructorNames = @($types | ForEach-Object { [regex]::Escape($_.name) })
        if ($constructorNames.Count -gt 0) {
            $constructorPattern = '(?m)^\s*(?:(?:public|private|protected|internal|static|extern|unsafe)\s+)+(' + ($constructorNames -join '|') + ')\s*\('
            $methods += Get-RegexValues -Text $text -Pattern $constructorPattern
            $methods = @($methods | Sort-Object -Unique)
        }

        # Keep identifiers that are also declared in this file: a method can call another
        # method in the same file. Definition lines may therefore appear as caller candidates,
        # which is preferable to silently missing internal calls.
        $calls = @(Get-RegexValues -Text $text -Pattern '\b([A-Za-z_]\w*)(?:\s*<[^;{}()]+>)?\s*\(' |
            Where-Object { -not $keywordSet.ContainsKey($_) })

        $records.Add([pscustomobject]@{
            path = Convert-ToRelativePath -BasePath $ProjectRoot -TargetPath $file.FullName
            namespaces = @($namespaces)
            types = @($types)
            methods = @($methods)
            calls = @($calls)
            typeReferences = @()
        })
        $parsedFileCount++
        if ($parsedFileCount -eq $SourceFiles.Count -or $parsedFileCount % 50 -eq 0) {
            Write-CodeQueryProgress -Stage 'parse' -Completed $parsedFileCount -Total $SourceFiles.Count -Message $file.Name
        }
    }
    Write-CodeQueryProgress -Stage 'bind' -Completed 0 -Total $SourceFiles.Count -Message 'Resolving C# types and calls'
    $bindingSummary = Add-CSharpTypeBindings -Records $records -ProjectRoot $ProjectRoot
    foreach ($record in $records) {
        $record.typeReferences = @($record.qualifiedTypeReferences | ForEach-Object { ($_ -split '\.')[-1] } | Sort-Object -Unique)
    }

    $index = [ordered]@{
        schemaVersion = $script:IndexVersion
        role = 'derived-index'
        root = $ProjectRoot
        generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        sourceSignature = Get-SourceSignature -ProjectRoot $ProjectRoot -Files $SourceFiles
        sourceRoots = if (@($ConfiguredSourceRoots).Count -gt 0) { @($ConfiguredSourceRoots) } else { @('Assets') }
        excludeRoots = @($ConfiguredExcludeRoots)
        fileCount = $SourceFiles.Count
        typeCount = @($records | ForEach-Object { $_.types }).Count
        methodCount = @($records | ForEach-Object { $_.methods }).Count
        qualifiedTypeCount = $bindingSummary.qualifiedTypeCount
        resolvedCallCount = $bindingSummary.resolvedCallCount
        includesAllSourceFiles = @($ConfiguredExcludeRoots).Count -eq 0
        files = @($records)
    }

    $directory = Split-Path -Parent $ResolvedIndexPath
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    [System.IO.File]::WriteAllText(
        $ResolvedIndexPath,
        ($index | ConvertTo-Json -Depth 8 -Compress),
        [System.Text.UTF8Encoding]::new($false))
    Write-CodeQueryProgress -Stage 'complete' -Completed $SourceFiles.Count -Total $SourceFiles.Count -Message 'Index written and verified'
    return [pscustomobject]$index
}

function Read-CodeIndex {
    param([string]$ResolvedIndexPath)

    return Get-Content -Raw -LiteralPath $ResolvedIndexPath -Encoding utf8 | ConvertFrom-Json
}

function Get-FreshIndex {
    param(
        [string]$ProjectRoot,
        [string]$ResolvedIndexPath,
        [string[]]$ConfiguredSourceRoots,
        [string[]]$ConfiguredExcludeRoots,
        [switch]$Force
    )

    $sourceFiles = Get-SourceFiles -ProjectRoot $ProjectRoot -ConfiguredSourceRoots $ConfiguredSourceRoots `
        -ConfiguredExcludeRoots $ConfiguredExcludeRoots
    $signature = Get-SourceSignature -ProjectRoot $ProjectRoot -Files $sourceFiles
    $expectedSourceRoots = if (@($ConfiguredSourceRoots).Count -gt 0) { @($ConfiguredSourceRoots) } else { @('Assets') }
    $expectedExcludeRoots = @($ConfiguredExcludeRoots)
    if (-not $Force -and (Test-Path -LiteralPath $ResolvedIndexPath)) {
        try {
            $existing = Read-CodeIndex -ResolvedIndexPath $ResolvedIndexPath
            if ($existing.schemaVersion -eq $script:IndexVersion -and
                $existing.root -eq $ProjectRoot -and
                $existing.sourceSignature -eq $signature -and
                (@($existing.sourceRoots) -join '|') -eq ($expectedSourceRoots -join '|') -and
                (@($existing.excludeRoots) -join '|') -eq ($expectedExcludeRoots -join '|')) {
                return $existing
            }
        }
        catch {
            # Invalid caches are disposable and rebuilt below.
        }
    }

    return New-CodeIndex -ProjectRoot $ProjectRoot -ResolvedIndexPath $ResolvedIndexPath `
        -SourceFiles $sourceFiles -ConfiguredSourceRoots $ConfiguredSourceRoots `
        -ConfiguredExcludeRoots $ConfiguredExcludeRoots
}

function Find-Impact {
    param(
        [object]$Index,
        [string[]]$Symbols,
        [string[]]$DefinitionPaths,
        [string[]]$QualifiedTypes = @(),
        [string[]]$QualifiedMethods = @(),
        [int]$MaxResults
    )

    $symbolSet = @{}
    foreach ($symbol in $Symbols) {
        if ($symbol) { $symbolSet[$symbol] = $true }
    }
    $qualifiedTypeSet = @{}
    foreach ($typeName in $QualifiedTypes) { if ($typeName) { $qualifiedTypeSet[$typeName] = $true } }
    $qualifiedMethodSet = @{}
    foreach ($methodName in $QualifiedMethods) { if ($methodName) { $qualifiedMethodSet[$methodName] = $true } }

    $results = foreach ($file in $Index.files) {
        if (Test-PortablePathInCollection -Path $file.path -Candidates $DefinitionPaths) { continue }
        $lexicalMatches = @($file.calls + $file.typeReferences |
            Where-Object { $symbolSet.ContainsKey($_) } | Sort-Object -Unique)
        $resolvedTargets = @($file.resolvedCalls | Where-Object {
            $qualifiedTypeSet.ContainsKey($_.targetType) -or $qualifiedMethodSet.ContainsKey($_.targetQualifiedName)
        } | Select-Object -ExpandProperty targetQualifiedName -Unique)
        $referencedTypes = @($file.qualifiedTypeReferences |
            Where-Object { $qualifiedTypeSet.ContainsKey($_) } | Sort-Object -Unique)
        if ($resolvedTargets.Count -gt 0 -or $referencedTypes.Count -gt 0 -or $lexicalMatches.Count -gt 0) {
            [pscustomobject]@{
                path = $file.path
                confidence = if ($resolvedTargets.Count -gt 0) { 'resolved-call' }
                    elseif ($referencedTypes.Count -gt 0) { 'resolved-type' }
                    else { 'lexical' }
                resolvedTargets = $resolvedTargets
                referencedTypes = $referencedTypes
                lexicalSymbols = $lexicalMatches
            }
        }
    }
    $confidenceOrder = @{ 'resolved-call' = 0; 'resolved-type' = 1; 'lexical' = 2 }
    return @($results | Sort-Object @{ Expression = { $confidenceOrder[$_.confidence] } }, path |
        Select-Object -First $MaxResults)
}

function Write-Result {
    param([object]$Value)

    if (-not $Value.PSObject.Properties['engine']) {
        $Value | Add-Member -NotePropertyName engine -NotePropertyValue 'codebase-query-regex-binding-v3'
    }
    if (-not $Value.PSObject.Properties['schemaVersion']) {
        $Value | Add-Member -NotePropertyName schemaVersion -NotePropertyValue $script:IndexVersion
    }
    $arguments = @{ Depth = 8; Compress = -not $Pretty }
    $Value | ConvertTo-Json @arguments
}

$projectRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
if (-not (Test-Path -LiteralPath $projectRoot -PathType Container)) {
    throw "Project root not found: $projectRoot"
}
$resolvedIndexPath = if ([System.IO.Path]::IsPathRooted($IndexPath)) {
    [System.IO.Path]::GetFullPath($IndexPath)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $projectRoot $IndexPath))
}
$script:ResolvedProgressPath = if ([string]::IsNullOrWhiteSpace($ProgressPath)) { $null }
elseif ([System.IO.Path]::IsPathRooted($ProgressPath)) { [System.IO.Path]::GetFullPath($ProgressPath) }
else { [System.IO.Path]::GetFullPath((Join-Path $projectRoot $ProgressPath)) }
Write-CodeQueryProgress -Stage 'scan' -Completed 0 -Total 0 -Message 'Discovering C# source files'

$index = Get-FreshIndex -ProjectRoot $projectRoot -ResolvedIndexPath $resolvedIndexPath `
    -ConfiguredSourceRoots $SourceRoots -Force:($Command -eq 'build') `
    -ConfiguredExcludeRoots $ExcludeRoots

switch ($Command) {
    'build' {
        $indexedPaths = @($index.files | ForEach-Object { $_.path } | Sort-Object -Unique)
        $sourceFiles = Get-SourceFiles -ProjectRoot $projectRoot -ConfiguredSourceRoots $SourceRoots -ConfiguredExcludeRoots $ExcludeRoots
        $sourcePaths = @($sourceFiles | ForEach-Object { Convert-ToRelativePath -BasePath $projectRoot -TargetPath $_.FullName } | Sort-Object -Unique)
        $missingPaths = @($sourcePaths | Where-Object { $indexedPaths -notcontains $_ })
        $unexpectedPaths = @($indexedPaths | Where-Object { $sourcePaths -notcontains $_ })
        Write-Result ([pscustomobject]@{
            command = 'build'
            indexPath = Convert-ToRelativePath -BasePath $projectRoot -TargetPath $resolvedIndexPath
            generatedAtUtc = $index.generatedAtUtc
            fileCount = $index.fileCount
            typeCount = @($index.files | ForEach-Object { $_.types }).Count
            methodCount = @($index.files | ForEach-Object { $_.methods }).Count
            qualifiedTypeCount = $index.qualifiedTypeCount
            resolvedCallCount = $index.resolvedCallCount
            discoveredFileCount = $sourcePaths.Count
            indexedFileCount = $indexedPaths.Count
            missingFileCount = $missingPaths.Count
            unexpectedFileCount = $unexpectedPaths.Count
            coveragePercent = if ($sourcePaths.Count -gt 0) { [Math]::Round(($sourcePaths.Count - $missingPaths.Count) * 100.0 / $sourcePaths.Count, 2) } else { 0 }
            includesAllSourceFiles = $index.includesAllSourceFiles
        })
    }
    'status' {
        Write-Result ([pscustomobject]@{
            command = 'status'
            schemaVersion = $index.schemaVersion
            role = $index.role
            generatedAtUtc = $index.generatedAtUtc
            fileCount = $index.fileCount
            qualifiedTypeCount = $index.qualifiedTypeCount
            resolvedCallCount = $index.resolvedCallCount
            fresh = $true
            indexPath = Convert-ToRelativePath -BasePath $projectRoot -TargetPath $resolvedIndexPath
        })
    }
    'architecture' {
        $folders = $index.files | Group-Object {
            $parts = @($_.path -split '\\')
            if ($parts.Count -ge 4 -and $parts[0] -eq 'Assets' -and $parts[1] -eq 'Scripts') {
                $parts[2]
            }
            elseif ($parts.Count -ge 3 -and $parts[0] -eq 'Assets') {
                $parts[1]
            }
            elseif ($parts.Count -ge 2) {
                $parts[0]
            }
            else {
                '(root)'
            }
        } |
            Sort-Object Count -Descending | Select-Object -First $Limit |
            ForEach-Object { [pscustomobject]@{ name = $_.Name; files = $_.Count } }
        $namespaces = $index.files | ForEach-Object { $_.namespaces } | Group-Object | Sort-Object Count -Descending |
            Select-Object -First $Limit | ForEach-Object { [pscustomobject]@{ name = $_.Name; files = $_.Count } }
        Write-Result ([pscustomobject]@{
            command = 'architecture'
            totals = [pscustomobject]@{
                files = $index.fileCount
                types = @($index.files | ForEach-Object { $_.types }).Count
                methods = @($index.files | ForEach-Object { $_.methods }).Count
                resolvedCalls = $index.resolvedCallCount
            }
            topFolders = @($folders)
            namespaces = @($namespaces)
        })
    }
    'search' {
        if ([string]::IsNullOrWhiteSpace($Query)) { throw 'search requires -Query.' }
        $escaped = [regex]::Escape($Query)
        $fileMatches = @($index.files | Where-Object { $_.path -match $escaped } |
            Select-Object -First $Limit -ExpandProperty path)
        $typeMatches = @($index.files | ForEach-Object {
            $file = $_
            $_.types | Where-Object { $_.name -match $escaped } |
                ForEach-Object {
                    [pscustomobject]@{
                        name = $_.name
                        qualifiedName = $_.qualifiedName
                        kind = $_.kind
                        baseTypes = @($_.baseTypes)
                        path = $file.path
                    }
                }
        } | Select-Object -First $Limit)
        $methodMatches = @($index.files | ForEach-Object {
            $file = $_
            $_.methodDefinitions | Where-Object {
                $_.name -match $escaped -or $_.qualifiedName -match $escaped
            } | ForEach-Object {
                [pscustomobject]@{
                    name = $_.name
                    qualifiedName = $_.qualifiedName
                    declaringType = $_.declaringType
                    returnType = $_.returnType
                    path = $file.path
                }
            }
        } | Select-Object -First $Limit)
        Write-Result ([pscustomobject]@{
            command = 'search'; query = $Query
            files = $fileMatches; types = $typeMatches; methods = $methodMatches
        })
    }
    'callers' {
        if ([string]::IsNullOrWhiteSpace($Query)) { throw 'callers requires -Query.' }
        $queryMethodName = if ($Query.Contains('.')) { ($Query -split '\.')[-1] } else { $Query }
        $definitions = @($index.files | ForEach-Object {
            $file = $_
            $_.methodDefinitions | Where-Object {
                $_.name -eq $queryMethodName -and
                (-not $Query.Contains('.') -or $_.qualifiedName.EndsWith($Query, [StringComparison]::Ordinal))
            } | ForEach-Object {
                [pscustomobject]@{ path = $file.path; qualifiedName = $_.qualifiedName; declaringType = $_.declaringType }
            }
        })
        $resolvedCallers = @($index.files | ForEach-Object {
            $file = $_
            $_.resolvedCalls | Where-Object {
                $_.method -eq $queryMethodName -and
                (-not $Query.Contains('.') -or $_.targetQualifiedName.EndsWith($Query, [StringComparison]::Ordinal))
            } | ForEach-Object {
                [pscustomobject]@{
                    path = $file.path
                    target = $_.targetQualifiedName
                    receiver = $_.receiver
                    bindingSource = $_.bindingSource
                }
            }
        } | Select-Object -First $Limit)
        $resolvedPaths = @($resolvedCallers | Select-Object -ExpandProperty path -Unique)
        $definitionPathsForQuery = @($definitions | Select-Object -ExpandProperty path -Unique)
        $candidates = @($index.files | Where-Object {
            $_.calls -contains $queryMethodName -and
            -not (Test-PortablePathInCollection -Path $_.path -Candidates $resolvedPaths) -and
            -not (Test-PortablePathInCollection -Path $_.path -Candidates $definitionPathsForQuery)
        } | Select-Object -First $Limit -ExpandProperty path)
        Write-Result ([pscustomobject]@{
            command = 'callers'; query = $Query
            definitionFiles = $definitions
            resolvedCallers = $resolvedCallers
            lexicalFallbackFiles = $candidates
            resolvedBindingCount = $resolvedCallers.Count
            bindingCoverage = 'partial'
        })
    }
    'impact' {
        if ([string]::IsNullOrWhiteSpace($Path) -and [string]::IsNullOrWhiteSpace($Query)) {
            throw 'impact requires -Path or -Query.'
        }

        if (-not [string]::IsNullOrWhiteSpace($Path)) {
            $normalizedPath = Convert-ToPortablePath -Path $Path
            $definitions = @($index.files | Where-Object {
                $_.path.Equals($normalizedPath, $script:PathComparison) -or
                $_.path.EndsWith($normalizedPath, $script:PathComparison)
            })
            if ($definitions.Count -eq 0) { throw "Indexed C# file not found: $Path" }
            $definitionPaths = @($definitions.path)
            $symbols = @(
                @($definitions | ForEach-Object { $_.types | ForEach-Object { $_.name } }) +
                @($definitions | ForEach-Object { $_.methods }) |
                Sort-Object -Unique
            )
            $qualifiedTypes = @($definitions | ForEach-Object { $_.types | ForEach-Object { $_.qualifiedName } } |
                Sort-Object -Unique)
            $qualifiedMethods = @($definitions | ForEach-Object {
                $_.methodDefinitions | ForEach-Object { $_.qualifiedName }
            } | Sort-Object -Unique)
        }
        else {
            $definitionPaths = @($index.files | Where-Object {
                @($_.types | ForEach-Object { $_.name }) -contains $Query -or
                @($_.types | ForEach-Object { $_.qualifiedName }) -contains $Query -or
                $_.methods -contains $Query
            } | Select-Object -ExpandProperty path)
            $symbols = @($Query)
            $qualifiedTypes = @($index.files | ForEach-Object { $_.types } | Where-Object {
                $_.name -eq $Query -or $_.qualifiedName -eq $Query
            } | Select-Object -ExpandProperty qualifiedName -Unique)
            $queryMethodName = if ($Query.Contains('.')) { ($Query -split '\.')[-1] } else { $Query }
            $qualifiedMethods = @($index.files | ForEach-Object { $_.methodDefinitions } | Where-Object {
                $_.name -eq $queryMethodName -and
                (-not $Query.Contains('.') -or $_.qualifiedName.EndsWith($Query, [StringComparison]::Ordinal))
            } | Select-Object -ExpandProperty qualifiedName -Unique)
        }

        Write-Result ([pscustomobject]@{
            command = 'impact'
            target = if ($Path) { $Path } else { $Query }
            definitionFiles = $definitionPaths
            symbols = @($symbols | Select-Object -First $Limit)
            candidates = @(Find-Impact -Index $index -Symbols $symbols -DefinitionPaths $definitionPaths `
                -QualifiedTypes $qualifiedTypes -QualifiedMethods $qualifiedMethods -MaxResults $Limit)
            bindingCoverage = 'partial'
        })
    }
    'changed' {
        $statusLines = @(& git -c core.quotepath=false -C $projectRoot status --porcelain=v1 --untracked-files=all)
        if ($LASTEXITCODE -ne 0) { throw 'git status failed.' }
        $indexedPathSet = [System.Collections.Generic.HashSet[string]]::new($script:PathComparer)
        foreach ($indexedFile in @($index.files)) { $null = $indexedPathSet.Add($indexedFile.path) }
        $changedPaths = @($statusLines | ForEach-Object {
            if ($_.Length -lt 4) { return }
            $value = $_.Substring(3).Trim('"')
            if ($value -match ' -> ') { $value = ($value -split ' -> ')[-1] }
            Convert-ToPortablePath -Path $value
        } | Where-Object { $_.EndsWith('.cs', [StringComparison]::OrdinalIgnoreCase) -and
            $indexedPathSet.Contains($_) } | Sort-Object -Unique)

        $allCandidates = [System.Collections.Generic.List[object]]::new()
        foreach ($changedPath in $changedPaths) {
            $definition = @($index.files | Where-Object {
                $_.path.Equals($changedPath, $script:PathComparison)
            })
            if ($definition.Count -eq 0) { continue }
            $symbols = @(
                @($definition | ForEach-Object { $_.types | ForEach-Object { $_.name } }) +
                @($definition | ForEach-Object { $_.methods }) |
                Sort-Object -Unique
            )
            $qualifiedTypes = @($definition | ForEach-Object { $_.types | ForEach-Object { $_.qualifiedName } })
            $qualifiedMethods = @($definition | ForEach-Object {
                $_.methodDefinitions | ForEach-Object { $_.qualifiedName }
            })
            foreach ($candidate in (Find-Impact -Index $index -Symbols $symbols -DefinitionPaths @($changedPath) `
                -QualifiedTypes $qualifiedTypes -QualifiedMethods $qualifiedMethods -MaxResults $Limit)) {
                $allCandidates.Add([pscustomobject]@{
                    changedPath = $changedPath
                    affectedPath = $candidate.path
                    confidence = $candidate.confidence
                    resolvedTargets = $candidate.resolvedTargets
                    referencedTypes = $candidate.referencedTypes
                    lexicalSymbols = $candidate.lexicalSymbols
                })
            }
        }
        Write-Result ([pscustomobject]@{
            command = 'changed'
            changedCSharpFileCount = $changedPaths.Count
            changedCSharpFiles = @($changedPaths | Select-Object -First $Limit)
            changedFilesTruncated = $changedPaths.Count -gt $Limit
            candidates = @($allCandidates | Select-Object -First $Limit)
            bindingCoverage = 'partial'
        })
    }
}
