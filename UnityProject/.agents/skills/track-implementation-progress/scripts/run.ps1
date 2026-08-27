[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('refresh', 'discover', 'validate', 'checkpoint', 'query')]
    [string]$Command = 'refresh',
    [string]$ProjectRoot = '',
    [ValidateSet('all', 'attention', 'capability', 'path')]
    [string]$Slice = 'all',
    [string]$Capability = '',
    [string]$Path = '',
    [switch]$Attention
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()

function Resolve-ProjectRoot {
    param([string]$RequestedRoot)
    if (-not [string]::IsNullOrWhiteSpace($RequestedRoot)) { return [IO.Path]::GetFullPath($RequestedRoot) }
    return [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
}

function Read-JsonFile {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $text = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8)
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    return $text | ConvertFrom-Json -Depth 100
}

function Write-DeterministicJson {
    param([string]$Path, [object]$Value)
    $next = ($Value | ConvertTo-Json -Depth 100) + [Environment]::NewLine
    if ((Test-Path -LiteralPath $Path -PathType Leaf) -and [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8) -eq $next) { return }
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { [IO.Directory]::CreateDirectory($parent) | Out-Null }
    [IO.File]::WriteAllText($Path, $next, [Text.UTF8Encoding]::new($false))
}

function Get-Sha256Text {
    param([string]$Text)
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Text)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function Get-NormalizedFileHash {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return '' }
    $text = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8).Replace("`r`n", "`n").Replace("`r", "`n")
    return Get-Sha256Text $text
}

function Get-CanonicalJson {
    param([object]$Value)
    return $Value | ConvertTo-Json -Compress -Depth 100
}

function Get-RelativeProjectPath {
    param([string]$Root, [string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return '' }
    $candidate = $Path.Replace('\', '/')
    if (-not [IO.Path]::IsPathRooted($candidate)) { return $candidate.TrimStart('/') }
    return [IO.Path]::GetRelativePath($Root, $candidate).Replace('\', '/')
}

function Get-PropertyValue {
    param([object]$Object, [string]$Name, [object]$Default = $null)
    if ($null -eq $Object) { return $Default }
    if ($Object -is [Collections.IDictionary]) { return $Object.Contains($Name) -and $null -ne $Object[$Name] ? $Object[$Name] : $Default }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return $Default }
    return $property.Value
}

function Sort-Ordinal {
    param([object[]]$Items, [string[]]$Properties = @())
    $ordered = [object[]]@($Items)
    [Array]::Sort($ordered, [Comparison[object]]{
        param($left, $right)
        if ($Properties.Count -eq 0) { return [StringComparer]::Ordinal.Compare([string]$left, [string]$right) }
        foreach ($property in $Properties) {
            $comparison = [StringComparer]::Ordinal.Compare([string](Get-PropertyValue $left $property ''), [string](Get-PropertyValue $right $property ''))
            if ($comparison -ne 0) { return $comparison }
        }
        return 0
    })
    return $ordered
}

function Get-SpecTitle {
    param([string]$SpecPath, [string]$Fallback)
    if (-not (Test-Path -LiteralPath $SpecPath -PathType Leaf)) { return $Fallback }
    $match = [regex]::Match([IO.File]::ReadAllText($SpecPath, [Text.Encoding]::UTF8), '(?m)^title:\s*["'']?(?<title>[^\r\n"'']+)')
    return $match.Success ? $match.Groups['title'].Value.Trim() : $Fallback
}

function Get-EvidenceState {
    param([string]$Root, [object[]]$Evidence)
    $result = [Collections.Generic.List[object]]::new()
    foreach ($item in @($Evidence)) {
        if ($null -eq $item) { continue }
        $path = Get-RelativeProjectPath $Root ([string](Get-PropertyValue $item 'displayPath' (Get-PropertyValue $item 'path' '')))
        if ([string]::IsNullOrWhiteSpace($path)) { continue }
        $expectedHash = [string](Get-PropertyValue $item 'fileHash' (Get-PropertyValue $item 'sha256' (Get-PropertyValue $item 'hash' '')))
        $currentHash = Get-NormalizedFileHash (Join-Path $Root $path)
        $state = if ([string]::IsNullOrWhiteSpace($currentHash)) { 'missing' } elseif ([string]::IsNullOrWhiteSpace($expectedHash)) { 'unverified' } elseif ($expectedHash -ne $currentHash) { 'stale' } else { 'current' }
        $result.Add([ordered]@{ path = $path; state = $state })
    }
    return @($result)
}

function Get-ImplementationEvidence {
    param([object]$Implementation)
    $evidence = @(Get-PropertyValue $Implementation 'evidence' @())
    $verification = Get-PropertyValue $Implementation 'verification' $null
    if ($evidence.Count -eq 0) { $evidence = @(Get-PropertyValue $verification 'evidence' @()) }
    if ($evidence.Count -eq 0) { $evidence = @(Get-PropertyValue $verification 'codeEvidence' @()) }
    return $evidence
}

function Get-ImplementationStatus {
    param([object]$Implementation)
    $status = [string](Get-PropertyValue $Implementation 'effectiveStatus' (Get-PropertyValue $Implementation 'implementationStatus' (Get-PropertyValue $Implementation 'status' (Get-PropertyValue $Implementation 'readiness' ''))))
    if ([string]::IsNullOrWhiteSpace($status)) { $status = [string](Get-PropertyValue $Implementation 'codeReadiness' 'unknown') }
    if ($status -eq 'implemented' -and (Get-ImplementationVerificationStatus $Implementation) -eq 'verified') { return 'verified' }
    return $status
}

function Get-ImplementationVerificationStatus {
    param([object]$Implementation)
    return [string](Get-PropertyValue $Implementation 'verificationStatus' (Get-PropertyValue (Get-PropertyValue $Implementation 'verification' $null) 'status' 'unverified'))
}

function Get-ImplementationSummaryText {
    param([object]$Implementation)
    return [string](Get-PropertyValue $Implementation 'summary' (Get-PropertyValue (Get-PropertyValue $Implementation 'verification' $null) 'summary' ''))
}

function Get-DesignSources {
    param([string]$Root, [object]$Implementation)
    $rawSources = @(Get-PropertyValue $Implementation 'sourceReferences' (Get-PropertyValue $Implementation 'designSources' @()))
    $result = [Collections.Generic.List[object]]::new()
    foreach ($source in $rawSources) {
        $sourceId = ''
        $rawPath = ''
        if ($source -is [string]) { $rawPath = [string]$source }
        else {
            $sourceId = [string](Get-PropertyValue $source 'sourceId' (Get-PropertyValue $source 'id' ''))
            $rawPath = [string](Get-PropertyValue $source 'path' (Get-PropertyValue $source 'documentPath' (Get-PropertyValue $source 'displayPath' '')))
        }
        if ([string]::IsNullOrWhiteSpace($rawPath)) { continue }
        $path = Get-RelativeProjectPath $Root $rawPath
        if ([string]::IsNullOrWhiteSpace($path) -or $path.StartsWith('../', [StringComparison]::Ordinal) -or [IO.Path]::IsPathRooted($path)) { continue }
        if (-not [string]::Equals([IO.Path]::GetExtension($path), '.md', [StringComparison]::OrdinalIgnoreCase)) { continue }
        $result.Add([ordered]@{ sourceId = $sourceId; path = $path })
    }
    return @(Sort-Ordinal @($result | Group-Object { "$($_.sourceId)|$($_.path)" } | ForEach-Object { $_.Group[0] }) @('sourceId', 'path'))
}

function Get-InputManifestEntry {
    param([string]$Root, [string]$Path)
    $normalized = Get-RelativeProjectPath $Root $Path
    $absolute = Join-Path $Root $normalized
    $hash = Get-NormalizedFileHash $absolute
    return [ordered]@{ path = $normalized; sha256 = $hash; state = if ([string]::IsNullOrWhiteSpace($hash)) { 'missing' } else { 'current' } }
}

function Add-InputManifestPath {
    param([string]$Root, [Collections.Generic.HashSet[string]]$Seen, [Collections.Generic.List[object]]$Manifest, [string]$Path)
    $normalized = Get-RelativeProjectPath $Root $Path
    if ([string]::IsNullOrWhiteSpace($normalized) -or -not $Seen.Add($normalized)) { return }
    $Manifest.Add((Get-InputManifestEntry $Root $normalized))
}

function Get-ManifestDigests {
    param([object[]]$Manifest)
    $ordered = @(Sort-Ordinal $Manifest @('path') | ForEach-Object { [ordered]@{ path = [string]$_.path; sha256 = [string]$_.sha256; state = [string]$_.state } })
    $manifestDigest = Get-Sha256Text (Get-CanonicalJson $ordered)
    $contentDigest = Get-Sha256Text (($ordered | ForEach-Object { "$($_.path)`n$($_.sha256)`n$($_.state)" }) -join "`n")
    return [ordered]@{ inputDigest = $contentDigest; inputManifestDigest = $manifestDigest }
}

function ConvertTo-DigestToken {
    param([object]$Value)
    return [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$Value))
}

function Get-RoutingOutputDigest {
    param([object[]]$Requirements)
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($item in @($Requirements)) {
        $id = [string](Get-PropertyValue $item 'id' '')
        $lines.Add(('R|{0}|{1}|{2}|{3}|{4}|{5}' -f (ConvertTo-DigestToken $id), (ConvertTo-DigestToken (Get-PropertyValue $item 'label' '')), (ConvertTo-DigestToken (Get-PropertyValue $item 'effectiveStatus' '')), (ConvertTo-DigestToken (Get-PropertyValue $item 'progress' 0)), (ConvertTo-DigestToken (Get-PropertyValue $item 'verificationStatus' '')), (ConvertTo-DigestToken (Get-PropertyValue $item 'summary' ''))))
        foreach ($source in @(Get-PropertyValue $item 'designSources' @())) { $lines.Add(('D|{0}|{1}|{2}' -f (ConvertTo-DigestToken $id), (ConvertTo-DigestToken (Get-PropertyValue $source 'sourceId' '')), (ConvertTo-DigestToken (Get-PropertyValue $source 'path' '')))) }
        foreach ($evidence in @(Get-PropertyValue $item 'evidence' @())) { $lines.Add(('E|{0}|{1}|{2}' -f (ConvertTo-DigestToken $id), (ConvertTo-DigestToken (Get-PropertyValue $evidence 'path' '')), (ConvertTo-DigestToken (Get-PropertyValue $evidence 'state' '')))) }
    }
    $ordered = $lines.ToArray()
    [Array]::Sort($ordered, [StringComparer]::Ordinal)
    return Get-Sha256Text ($ordered -join "`n")
}

function Build-Summary {
    param([string]$Root)
    $openSpecRoot = Join-Path $Root 'openspec'
    $metadataPath = Join-Path $openSpecRoot 'spec-metadata/dependencies.json'
    $metadata = Read-JsonFile $metadataPath
    $metadataById = @{}
    foreach ($node in @(Get-PropertyValue $metadata 'nodes' @())) {
        $id = [string](Get-PropertyValue $node 'id' '')
        if (-not [string]::IsNullOrWhiteSpace($id)) { $metadataById[$id] = $node }
    }

    $manifest = [Collections.Generic.List[object]]::new()
    $manifestPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    Add-InputManifestPath $Root $manifestPaths $manifest $metadataPath
    $requirements = [Collections.Generic.List[object]]::new()
    $staleEvidence = [Collections.Generic.List[object]]::new()
    $specRoot = Join-Path $openSpecRoot 'specs'
    if (Test-Path -LiteralPath $specRoot -PathType Container) {
        foreach ($directory in Sort-Ordinal @(Get-ChildItem -LiteralPath $specRoot -Directory) @('Name')) {
            $id = $directory.Name
            $specPath = Join-Path $directory.FullName 'spec.md'
            $implementationPath = Join-Path $directory.FullName 'implementation.json'
            Add-InputManifestPath $Root $manifestPaths $manifest $specPath
            Add-InputManifestPath $Root $manifestPaths $manifest $implementationPath
            $implementation = Read-JsonFile $implementationPath
            $metadataNode = $metadataById[$id]
            if ($null -eq $implementation) {
                $requirements.Add([ordered]@{ id = $id; label = Get-SpecTitle $specPath $id; status = 'unknown'; effectiveStatus = 'unknown'; progress = 0; verificationStatus = 'unverified'; summary = '缺少正式 implementation.json。'; sources = @((Get-RelativeProjectPath $Root $specPath), (Get-RelativeProjectPath $Root $implementationPath)); evidence = @() })
                continue
            }
            $evidenceState = Get-EvidenceState $Root (Get-ImplementationEvidence $implementation)
            foreach ($evidence in @($evidenceState)) {
                Add-InputManifestPath $Root $manifestPaths $manifest $evidence.path
                if ($evidence.state -ne 'current') { $staleEvidence.Add([ordered]@{ capability = $id; path = $evidence.path; state = $evidence.state }) }
            }
            $status = Get-ImplementationStatus $implementation
            $verification = Get-PropertyValue $implementation 'verification' $null
            $currentSpecHash = Get-NormalizedFileHash $specPath
            $assertedSpecHash = [string](Get-PropertyValue $implementation 'specHash' '')
            $validatedSpecHash = [string](Get-PropertyValue $verification 'validatedAgainstSpecHash' '')
            $assertionValid = [int](Get-PropertyValue $implementation 'schemaVersion' 0) -eq 1 -and [string](Get-PropertyValue $implementation 'artifactRole' '') -eq 'formal-implementation-assertion' -and [string](Get-PropertyValue $implementation 'capability' '') -eq $id -and -not [string]::IsNullOrWhiteSpace($currentSpecHash) -and $assertedSpecHash -eq $currentSpecHash -and $validatedSpecHash -eq $currentSpecHash
            $verifiedWithoutCurrentEvidence = $status -eq 'verified' -and ($evidenceState.Count -eq 0 -or @($evidenceState | Where-Object { $_.state -ne 'current' }).Count -gt 0)
            if (-not $assertionValid -or $verifiedWithoutCurrentEvidence -or @($evidenceState | Where-Object { $_.state -ne 'current' }).Count -gt 0) {
                $status = 'stale'
                if (-not $assertionValid) { $staleEvidence.Add([ordered]@{ capability = $id; path = Get-RelativeProjectPath $Root $implementationPath; state = 'spec-binding-invalid' }) }
            }
            $defaultProgress = if ($status -in @('verified', 'implemented')) { 100 } else { 0 }
            $progress = [int](Get-PropertyValue $implementation 'progress' (Get-PropertyValue $implementation 'implementationProgress' $defaultProgress))
            $sources = @((Get-RelativeProjectPath $Root $specPath), (Get-RelativeProjectPath $Root $implementationPath))
            if ($null -ne $metadataNode) { $sources += Get-RelativeProjectPath $Root $metadataPath }
            $designSources = Get-DesignSources $Root $implementation
            foreach ($designSource in @($designSources)) { Add-InputManifestPath $Root $manifestPaths $manifest $designSource.path; $sources += $designSource.path }
            $requirements.Add([ordered]@{ id = $id; label = [string](Get-PropertyValue $implementation 'title' (Get-SpecTitle $specPath $id)); status = Get-ImplementationStatus $implementation; effectiveStatus = $status; progress = [Math]::Clamp($progress, 0, 100); verificationStatus = Get-ImplementationVerificationStatus $implementation; summary = Get-ImplementationSummaryText $implementation; designSources = @($designSources); sources = @($sources); evidence = @($evidenceState) })
        }
    }
    $orderedRequirements = @(Sort-Ordinal @($requirements) @('id'))
    $orderedManifest = @(Sort-Ordinal @($manifest) @('path'))
    $digests = Get-ManifestDigests $orderedManifest
    $counts = [ordered]@{}
    foreach ($status in @('verified', 'implemented', 'partial', 'planned', 'unknown', 'blocked', 'stale')) { $counts[$status] = @($orderedRequirements | Where-Object { $_.effectiveStatus -eq $status }).Count }
    $orderedStaleEvidence = @(Sort-Ordinal @($staleEvidence | Group-Object { "$($_.capability)|$($_.path)" } | ForEach-Object { $_.Group[0] }) @('capability', 'path'))
    return [ordered]@{ schemaVersion = 2; role = 'derived-routing-index'; inputDigest = $digests.inputDigest; inputManifestDigest = $digests.inputManifestDigest; outputDigest = (Get-RoutingOutputDigest $orderedRequirements); inputManifest = $orderedManifest; counts = $counts; attentionRequired = @($orderedRequirements | Where-Object { $_.effectiveStatus -notin @('verified', 'implemented') } | ForEach-Object { $_.id }); verificationRequired = @($orderedRequirements | Where-Object { $_.effectiveStatus -eq 'implemented' -and $_.verificationStatus -notin @('verified', 'implemented') } | ForEach-Object { $_.id }); staleEvidence = $orderedStaleEvidence; requirements = $orderedRequirements }
}

function Get-GitOutput {
    param([string]$Root, [string[]]$Arguments)
    $output = & git -C $Root @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) { return @() }
    return @($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Read-Audit {
    param([string]$Root)
    $path = Join-Path $Root 'openspec/implementation-audit.json'
    $audit = Read-JsonFile $path
    if ($null -eq $audit) { return [ordered]@{ schemaVersion = 1; discoveryRevision = ''; discoveryExclusions = @() } }
    if ([int](Get-PropertyValue $audit 'schemaVersion' 0) -ne 1) { throw 'implementation-audit.json schemaVersion 必须为 1。' }
    return $audit
}

function Build-Discovery {
    param([string]$Root, [object]$Summary, [object]$Audit)
    $baseline = [string](Get-PropertyValue $Audit 'discoveryRevision' '')
    $gitPrefix = [string](Get-GitOutput $Root @('rev-parse', '--show-prefix') | Select-Object -First 1)
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($line in Get-GitOutput $Root @('status', '--porcelain=v1', '--untracked-files=all')) {
        if ($line.Length -lt 4) { continue }
        $path = $line.Substring(3).Trim().Trim('"').Replace('\', '/')
        if ($path.Contains(' -> ')) { $path = ($path -split ' -> ')[-1].Trim().Trim('"') }
        if (-not [string]::IsNullOrWhiteSpace($gitPrefix) -and $path.StartsWith($gitPrefix, [StringComparison]::OrdinalIgnoreCase)) { $path = $path.Substring($gitPrefix.Length) }
        if ($path.EndsWith('.cs', [StringComparison]::OrdinalIgnoreCase)) { $paths.Add($path) | Out-Null }
    }
    $baselineMissing = [string]::IsNullOrWhiteSpace($baseline)
    if ($baselineMissing) { foreach ($path in Get-GitOutput $Root @('ls-files', '--', '*.cs')) { $paths.Add($path.Replace('\', '/')) | Out-Null } }
    else {
        foreach ($gitPath in Get-GitOutput $Root @('diff', '--name-only', "$baseline..HEAD", '--', '*.cs')) {
            $path = $gitPath.Replace('\', '/')
            if (-not [string]::IsNullOrWhiteSpace($gitPrefix) -and $path.StartsWith($gitPrefix, [StringComparison]::OrdinalIgnoreCase)) { $path = $path.Substring($gitPrefix.Length) }
            $paths.Add($path) | Out-Null
        }
    }
    $excludedPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($path in @($paths)) {
        foreach ($exclusion in @(Get-PropertyValue $Audit 'discoveryExclusions' @())) {
            $excludedPath = [string](Get-PropertyValue $exclusion 'path' '')
            $excludedPrefix = [string](Get-PropertyValue $exclusion 'pathPrefix' '')
            $reason = [string](Get-PropertyValue $exclusion 'reason' '')
            if ([string]::IsNullOrWhiteSpace($reason)) { continue }
            if (-not [string]::IsNullOrWhiteSpace($excludedPath) -and $path.Equals($excludedPath.Replace('\', '/'), [StringComparison]::OrdinalIgnoreCase)) { $excludedPaths.Add($path) | Out-Null; break }
            if (-not [string]::IsNullOrWhiteSpace($excludedPrefix) -and $path.StartsWith($excludedPrefix.Replace('\', '/'), [StringComparison]::OrdinalIgnoreCase)) { $excludedPaths.Add($path) | Out-Null; break }
        }
    }
    $mappedPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($requirement in @($Summary.requirements)) { foreach ($evidence in @($requirement.evidence)) { if ($null -ne $evidence) { $mappedPaths.Add([string]$evidence.path) | Out-Null } } }
    $index = Read-JsonFile (Join-Path $Root '.agents/codebase-query/code-query-index.json')
    $indexByPath = @{}
    foreach ($file in @(Get-PropertyValue $index 'files' @())) { if ($null -ne $file) { $indexByPath[[string]$file.path] = $file } }
    $changedFiles = foreach ($path in @(Sort-Ordinal @($paths))) {
        $indexFile = $indexByPath[$path]
        [ordered]@{ path = $path; mappedByExistingEvidence = $mappedPaths.Contains($path); types = @((Get-PropertyValue $indexFile 'types' @()) | ForEach-Object { $_.qualifiedName }); methods = @((Get-PropertyValue $indexFile 'methodDefinitions' @()) | ForEach-Object { $_.qualifiedName }) }
    }
    $auditedChanges = @($changedFiles | Where-Object { -not $excludedPaths.Contains($_.path) })
    return [ordered]@{ schemaVersion = 1; role = 'local-audit'; baselineMissing = $baselineMissing; discoveryRevision = $baseline; changedCSharpFiles = $auditedChanges; unmappedCSharpChanges = @($auditedChanges | Where-Object { -not $_.mappedByExistingEvidence }); excludedCSharpChanges = @($changedFiles | Where-Object { $excludedPaths.Contains($_.path) }); staleEvidence = @(Get-PropertyValue $Summary 'staleEvidence' @()) }
}

function Read-CurrentSummary {
    param([string]$Root)
    $summaryPath = Join-Path $Root 'openspec/implementation-summary.json'
    $summary = Read-JsonFile $summaryPath
    if ($null -eq $summary) { throw '缺少 openspec/implementation-summary.json，请先运行 refresh。' }
    if ([int](Get-PropertyValue $summary 'schemaVersion' 0) -ne 2 -or [string](Get-PropertyValue $summary 'role' '') -ne 'derived-routing-index') { throw 'implementation-summary.json schema 或 role 不受支持。' }
    $current = Build-Summary $Root
    if ([string]$summary.inputDigest -ne [string]$current.inputDigest -or [string]$summary.inputManifestDigest -ne [string]$current.inputManifestDigest) { throw 'implementation-summary.json 已过期；输入事实或证据发生变化，请先运行 refresh。' }
    if ((Get-CanonicalJson $summary) -ne (Get-CanonicalJson $current)) { throw 'implementation-summary.json 派生内容已被修改或生成器版本不一致，请先运行 refresh。' }
    return $summary
}

$root = Resolve-ProjectRoot $ProjectRoot
$summaryPath = Join-Path $root 'openspec/implementation-summary.json'
$auditPath = Join-Path $root 'openspec/implementation-audit.json'
$discoveryPath = Join-Path $root '.agent-memory/zworkflow/local/implementation-discovery.json'

switch ($Command) {
    'refresh' {
        $summary = Build-Summary $root
        Write-DeterministicJson $summaryPath $summary
        $summary | ConvertTo-Json -Depth 100
        break
    }
    'discover' {
        $summary = Build-Summary $root
        $discovery = Build-Discovery $root $summary (Read-Audit $root)
        Write-DeterministicJson $discoveryPath $discovery
        $discovery | ConvertTo-Json -Depth 100
        break
    }
    'validate' {
        $summary = Read-CurrentSummary $root
        $summary | ConvertTo-Json -Depth 100
        break
    }
    'query' {
        $summary = Read-CurrentSummary $root
        $selected = @($summary.requirements)
        $useAttention = $Attention -or $Slice -eq 'attention'
        if ($useAttention) { $selected = @($selected | Where-Object { $_.effectiveStatus -notin @('verified', 'implemented') }) }
        if ($Slice -eq 'capability' -or -not [string]::IsNullOrWhiteSpace($Capability)) {
            $ids = @($Capability -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
            if ($ids.Count -eq 0) { throw 'query capability slice 需要 -Capability。' }
            $selected = @($selected | Where-Object { $ids -contains $_.id })
        }
        if ($Slice -eq 'path' -or -not [string]::IsNullOrWhiteSpace($Path)) {
            if ([string]::IsNullOrWhiteSpace($Path)) { throw 'query path slice 需要 -Path。' }
            $needle = $Path.Replace('\', '/').TrimStart('/')
            $selected = @($selected | Where-Object { @($_.evidence | Where-Object { $_.path -eq $needle -or $_.path.StartsWith($needle, [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0 })
        }
        [ordered]@{ schemaVersion = 1; role = 'query-slice'; sourceSchemaVersion = $summary.schemaVersion; slice = $Slice; attention = [bool]$useAttention; capability = $Capability; path = $Path; requirements = $selected } | ConvertTo-Json -Depth 100
        break
    }
    'checkpoint' {
        $summary = Read-CurrentSummary $root
        $audit = Read-Audit $root
        $discovery = Build-Discovery $root $summary $audit
        if (@($discovery.unmappedCSharpChanges).Count -gt 0 -or @($discovery.staleEvidence).Count -gt 0) { throw '仍存在未映射 C# 变化或过期证据，不能建立审计检查点。' }
        $revision = Get-GitOutput $root @('rev-parse', 'HEAD') | Select-Object -First 1
        if ([string]::IsNullOrWhiteSpace($revision)) { throw '无法读取 Git HEAD，不能建立审计检查点。' }
        $audit.schemaVersion = 1
        $audit.discoveryRevision = $revision
        Write-DeterministicJson $auditPath $audit
        $summary | ConvertTo-Json -Depth 100
        break
    }
}
