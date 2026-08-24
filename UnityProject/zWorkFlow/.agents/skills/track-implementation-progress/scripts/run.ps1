[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('refresh', 'discover', 'validate', 'checkpoint')]
    [string]$Command = 'refresh',
    [string]$ProjectRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()

function Resolve-ProjectRoot {
    param([string]$RequestedRoot)
    if (-not [string]::IsNullOrWhiteSpace($RequestedRoot)) {
        return [IO.Path]::GetFullPath($RequestedRoot)
    }
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
    $json = $Value | ConvertTo-Json -Depth 100
    $next = $json + [Environment]::NewLine
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        $current = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8)
        if ($current -eq $next) { return }
    }
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        [IO.Directory]::CreateDirectory($parent) | Out-Null
    }
    [IO.File]::WriteAllText($Path, $next, [Text.UTF8Encoding]::new($false))
}

function Get-NormalizedFileHash {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return '' }
    $text = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8).Replace("`r`n", "`n").Replace("`r", "`n")
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($text)
    $hash = [Security.Cryptography.SHA256]::HashData($bytes)
    return [Convert]::ToHexString($hash).ToLowerInvariant()
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
    if ($Object -is [Collections.IDictionary]) {
        return $Object.Contains($Name) -and $null -ne $Object[$Name] ? $Object[$Name] : $Default
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return $Default }
    return $property.Value
}

function Get-SpecTitle {
    param([string]$SpecPath, [string]$Fallback)
    if (-not (Test-Path -LiteralPath $SpecPath -PathType Leaf)) { return $Fallback }
    $match = [regex]::Match([IO.File]::ReadAllText($SpecPath, [Text.Encoding]::UTF8), '(?m)^title:\s*["'']?(?<title>[^\r\n"'']+)')
    return $match.Success ? $match.Groups['title'].Value.Trim() : $Fallback
}

function Get-EvidenceState {
    param([string]$Root, [object[]]$Evidence, [hashtable]$IndexByPath)
    $result = [Collections.Generic.List[object]]::new()
    foreach ($item in @($Evidence)) {
        if ($null -eq $item) { continue }
        $path = Get-RelativeProjectPath $Root ([string](Get-PropertyValue $item 'displayPath' (Get-PropertyValue $item 'path' '')))
        if ([string]::IsNullOrWhiteSpace($path)) { continue }
        $expectedHash = [string](Get-PropertyValue $item 'fileHash' '')
        $absolutePath = Join-Path $Root $path
        $currentHash = Get-NormalizedFileHash $absolutePath
        $state = if ([string]::IsNullOrWhiteSpace($currentHash)) { 'missing' } elseif (-not [string]::IsNullOrWhiteSpace($expectedHash) -and $expectedHash -ne $currentHash) { 'stale' } else { 'current' }
        $result.Add([ordered]@{
            path = $path
            feature = [string](Get-PropertyValue $item 'feature' '')
            recordedHash = $expectedHash
            currentHash = $currentHash
            state = $state
        })
    }
    return @($result)
}

function Get-StatusRank {
    param([string]$Status)
    switch ($Status) {
        'stale' { return 6 }
        'blocked' { return 5 }
        'partial' { return 4 }
        'unknown' { return 0 }
        'planned' { return 3 }
        'implemented' { return 2 }
        'verified' { return 1 }
        default { return 0 }
    }
}

function Resolve-ReviewStatus {
    param([object]$Review, [object[]]$EvidenceState)
    if (@($EvidenceState | Where-Object { $_.state -ne 'current' }).Count -gt 0) { return 'stale' }
    $verification = Get-PropertyValue $Review 'verification' $null
    $verificationStatus = [string](Get-PropertyValue $verification 'status' '')
    $codeReadiness = [string](Get-PropertyValue $Review 'codeReadiness' '')
    $readiness = [string](Get-PropertyValue $Review 'readiness' '')
    if ($verificationStatus -in @('verified', 'implemented') -and $readiness -in @('implemented', 'ready')) { return 'verified' }
    if ($verificationStatus -in @('verified', 'implemented') -or $codeReadiness -eq 'implemented' -or $readiness -eq 'implemented') { return 'implemented' }
    if ($codeReadiness -eq 'partial') { return 'partial' }
    if ($readiness -like 'blocked*') { return 'blocked' }
    return 'planned'
}

function Add-OrMergeRequirement {
    param([hashtable]$Requirements, [hashtable]$Requirement)
    $id = [string]$Requirement.id
    if ([string]::IsNullOrWhiteSpace($id)) { return }
    if (-not $Requirements.ContainsKey($id)) {
        $Requirements[$id] = $Requirement
        return
    }
    $existing = $Requirements[$id]
    if ((Get-StatusRank $Requirement.effectiveStatus) -gt (Get-StatusRank $existing.effectiveStatus)) {
        $existing.status = $Requirement.status
        $existing.effectiveStatus = $Requirement.effectiveStatus
        $existing.progress = $Requirement.progress
        $existing.verificationStatus = $Requirement.verificationStatus
        $existing.summary = $Requirement.summary
    }
    $existing.sources = @($existing.sources + $Requirement.sources | Sort-Object -Unique)
    $evidence = @($existing.evidence + $Requirement.evidence)
    $existing.evidence = @($evidence | Sort-Object path, feature -Unique)
}

function Build-Summary {
    param([string]$Root)
    $openSpecRoot = Join-Path $Root 'openspec'
    $ledgerPath = Join-Path $openSpecRoot 'implementation-ledger.json'
    $indexPath = Join-Path $Root '.agents/codebase-query/code-query-index.json'
    $ledger = Read-JsonFile $ledgerPath
    if ($null -eq $ledger) { $ledger = [pscustomobject]@{ schemaVersion = 3; discoveryRevision = ''; entries = @() } }
    if ([int](Get-PropertyValue $ledger 'schemaVersion' 0) -lt 2) { throw 'implementation-ledger.json schemaVersion 必须至少为 2。' }

    $index = Read-JsonFile $indexPath
    $indexByPath = @{}
    foreach ($file in @(Get-PropertyValue $index 'files' @())) {
        if ($null -ne $file -and -not [string]::IsNullOrWhiteSpace([string]$file.path)) { $indexByPath[[string]$file.path] = $file }
    }

    $requirements = @{}
    $staleEvidence = [Collections.Generic.List[object]]::new()
    $metadataPath = Join-Path $openSpecRoot 'spec-metadata/dependencies.json'
    $metadata = Read-JsonFile $metadataPath
    $metadataById = @{}
    foreach ($node in @(Get-PropertyValue $metadata 'nodes' @())) {
        $nodeId = [string](Get-PropertyValue $node 'id' '')
        if (-not [string]::IsNullOrWhiteSpace($nodeId)) { $metadataById[$nodeId] = $node }
    }
    $specRoot = Join-Path $openSpecRoot 'specs'
    if (Test-Path -LiteralPath $specRoot -PathType Container) {
        foreach ($directory in Get-ChildItem -LiteralPath $specRoot -Directory | Sort-Object Name) {
            $id = $directory.Name
            $specPath = Join-Path $directory.FullName 'spec.md'
            if (-not (Test-Path -LiteralPath $specPath -PathType Leaf)) { continue }
            $reviewPath = Join-Path $directory.FullName 'spec-review.json'
            $review = Read-JsonFile $reviewPath
            $verification = Get-PropertyValue $review 'verification' $null
            $evidenceState = Get-EvidenceState $Root @(Get-PropertyValue $verification 'codeEvidence' @()) $indexByPath
            foreach ($evidence in @($evidenceState | Where-Object { $_.state -ne 'current' })) { $staleEvidence.Add([ordered]@{ capability = $id; path = $evidence.path; state = $evidence.state }) }
            $metadataNode = $metadataById[$id]
            $metadataReadiness = [string](Get-PropertyValue $metadataNode 'readiness' 'unknown')
            $effectiveStatus = if ($null -ne $review) { Resolve-ReviewStatus $review $evidenceState } elseif ($metadataReadiness -eq 'implemented') { 'implemented' } elseif ($metadataReadiness -like 'blocked*') { 'blocked' } elseif ($metadataReadiness -eq 'partial') { 'partial' } else { 'unknown' }
            $sources = @((Get-RelativeProjectPath $Root $specPath))
            if ($null -ne $metadataNode) { $sources += Get-RelativeProjectPath $Root $metadataPath }
            Add-OrMergeRequirement $requirements ([ordered]@{
                id = $id
                label = Get-SpecTitle $specPath $id
                status = [string](Get-PropertyValue $review 'readiness' $metadataReadiness)
                effectiveStatus = $effectiveStatus
                progress = $effectiveStatus -in @('verified', 'implemented') ? 100 : 0
                verificationStatus = [string](Get-PropertyValue $verification 'status' 'unverified')
                summary = [string](Get-PropertyValue $verification 'summary' '')
                sources = $sources
                evidence = $evidenceState
            })
        }
    }

    $changesRoot = Join-Path $openSpecRoot 'changes'
    if (Test-Path -LiteralPath $changesRoot -PathType Container) {
        foreach ($reviewFile in Get-ChildItem -LiteralPath $changesRoot -Recurse -Filter 'change-review.json' -File | Sort-Object FullName) {
            $review = Read-JsonFile $reviewFile.FullName
            if ($null -eq $review) { continue }
            $verification = Get-PropertyValue $review 'verification' $null
            $evidenceState = Get-EvidenceState $Root @(Get-PropertyValue $verification 'codeEvidence' @()) $indexByPath
            $status = Resolve-ReviewStatus $review $evidenceState
            foreach ($id in @(Get-PropertyValue $review 'capabilities' @())) {
                Add-OrMergeRequirement $requirements ([ordered]@{
                    id = [string]$id
                    label = [string](Get-PropertyValue $review 'title' $id)
                    status = [string](Get-PropertyValue $review 'codeReadiness' 'unimplemented')
                    effectiveStatus = $status
                    progress = $status -in @('verified', 'implemented') ? 100 : ($status -eq 'partial' ? 50 : 0)
                    verificationStatus = [string](Get-PropertyValue $verification 'status' 'unverified')
                    summary = [string](Get-PropertyValue $verification 'summary' '')
                    sources = @((Get-RelativeProjectPath $Root $reviewFile.FullName))
                    evidence = $evidenceState
                })
            }
        }
    }

    foreach ($entry in @(Get-PropertyValue $ledger 'entries' @())) {
        if ($null -eq $entry) { continue }
        $id = [string](Get-PropertyValue $entry 'implementationId' '')
        if ([string]::IsNullOrWhiteSpace($id)) { continue }
        $evidenceState = Get-EvidenceState $Root @(Get-PropertyValue $entry 'evidence' @()) $indexByPath
        foreach ($evidence in @($evidenceState | Where-Object { $_.state -ne 'current' })) { $staleEvidence.Add([ordered]@{ capability = $id; path = $evidence.path; state = $evidence.state }) }
        $status = [string](Get-PropertyValue $entry 'implementationStatus' 'planned')
        if (@($evidenceState | Where-Object { $_.state -ne 'current' }).Count -gt 0) { $status = 'stale' }
        Add-OrMergeRequirement $requirements ([ordered]@{
            id = $id
            label = [string](Get-PropertyValue $entry 'implementationLabel' $id)
            status = [string](Get-PropertyValue $entry 'implementationStatus' 'planned')
            effectiveStatus = $status
            progress = [Math]::Clamp([int](Get-PropertyValue $entry 'implementationProgress' 0), 0, 100)
            verificationStatus = [string](Get-PropertyValue $entry 'verificationStatus' 'unverified')
            summary = [string](Get-PropertyValue $entry 'changeSummary' '')
            sources = @("$([string](Get-PropertyValue $entry 'sourceId' ''))::$([string](Get-PropertyValue $entry 'documentPath' ''))")
            evidence = $evidenceState
        })
    }

    $orderedRequirements = @($requirements.Values | Sort-Object id)
    $attention = @($orderedRequirements | Where-Object { $_.effectiveStatus -notin @('verified', 'implemented') } | ForEach-Object { $_.id })
    $verificationRequired = @($orderedRequirements | Where-Object { $_.effectiveStatus -eq 'implemented' -and $_.verificationStatus -notin @('verified', 'implemented') } | ForEach-Object { $_.id })
    $counts = [ordered]@{}
    foreach ($status in @('verified', 'implemented', 'partial', 'planned', 'unknown', 'blocked', 'stale')) {
        $counts[$status] = @($orderedRequirements | Where-Object { $_.effectiveStatus -eq $status }).Count
    }
    return [ordered]@{
        schemaVersion = 1
        role = 'derived-index'
        sourceLedgerHash = Get-NormalizedFileHash $ledgerPath
        discoveryRevision = [string](Get-PropertyValue $ledger 'discoveryRevision' '')
        counts = $counts
        attentionRequired = $attention
        verificationRequired = $verificationRequired
        staleEvidence = @($staleEvidence | ForEach-Object { [pscustomobject]$_ } | Sort-Object capability, path -Unique)
        requirements = $orderedRequirements
    }
}

function Get-GitOutput {
    param([string]$Root, [string[]]$Arguments)
    $output = & git -C $Root @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) { return @() }
    return @($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Build-Discovery {
    param([string]$Root, [object]$Summary)
    $baseline = [string](Get-PropertyValue $Summary 'discoveryRevision' '')
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
    if ($baselineMissing) {
        foreach ($path in Get-GitOutput $Root @('ls-files', '--', '*.cs')) { $paths.Add($path.Replace('\', '/')) | Out-Null }
    }
    else {
        foreach ($gitPath in Get-GitOutput $Root @('diff', '--name-only', "$baseline..HEAD", '--', '*.cs')) {
            $path = $gitPath.Replace('\', '/')
            if (-not [string]::IsNullOrWhiteSpace($gitPrefix) -and $path.StartsWith($gitPrefix, [StringComparison]::OrdinalIgnoreCase)) { $path = $path.Substring($gitPrefix.Length) }
            $paths.Add($path) | Out-Null
        }
    }

    $ledger = Read-JsonFile (Join-Path $Root 'openspec/implementation-ledger.json')
    $exclusions = @(Get-PropertyValue $ledger 'discoveryExclusions' @())
    $excludedPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($path in @($paths)) {
        foreach ($exclusion in $exclusions) {
            $excludedPath = [string](Get-PropertyValue $exclusion 'path' '')
            $excludedPrefix = [string](Get-PropertyValue $exclusion 'pathPrefix' '')
            $exclusionReason = [string](Get-PropertyValue $exclusion 'reason' '')
            if ([string]::IsNullOrWhiteSpace($exclusionReason)) { continue }
            if (-not [string]::IsNullOrWhiteSpace($excludedPath) -and $path.Equals($excludedPath.Replace('\', '/'), [StringComparison]::OrdinalIgnoreCase)) { $excludedPaths.Add($path) | Out-Null; break }
            if (-not [string]::IsNullOrWhiteSpace($excludedPrefix) -and $path.StartsWith($excludedPrefix.Replace('\', '/'), [StringComparison]::OrdinalIgnoreCase)) { $excludedPaths.Add($path) | Out-Null; break }
        }
    }

    $index = Read-JsonFile (Join-Path $Root '.agents/codebase-query/code-query-index.json')
    $indexByPath = @{}
    foreach ($file in @(Get-PropertyValue $index 'files' @())) { if ($null -ne $file) { $indexByPath[[string]$file.path] = $file } }
    $mappedPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($requirement in @(Get-PropertyValue $Summary 'requirements' @())) {
        foreach ($evidence in @(Get-PropertyValue $requirement 'evidence' @())) { if ($null -ne $evidence) { $mappedPaths.Add([string]$evidence.path) | Out-Null } }
    }
    $changedFiles = foreach ($path in @($paths | Sort-Object)) {
        $indexFile = $indexByPath[$path]
        [ordered]@{
            path = $path
            mappedByExistingEvidence = $mappedPaths.Contains($path)
            types = @((Get-PropertyValue $indexFile 'types' @()) | ForEach-Object { $_.qualifiedName })
            methods = @((Get-PropertyValue $indexFile 'methodDefinitions' @()) | ForEach-Object { $_.qualifiedName })
        }
    }
    $auditedChanges = @($changedFiles | Where-Object { -not $excludedPaths.Contains($_.path) })
    return [ordered]@{
        schemaVersion = 1
        role = 'local-audit'
        baselineMissing = $baselineMissing
        discoveryRevision = $baseline
        changedCSharpFiles = $auditedChanges
        unmappedCSharpChanges = @($auditedChanges | Where-Object { -not $_.mappedByExistingEvidence })
        excludedCSharpChanges = @($changedFiles | Where-Object { $excludedPaths.Contains($_.path) })
        staleEvidence = @(Get-PropertyValue $Summary 'staleEvidence' @())
    }
}

$root = Resolve-ProjectRoot $ProjectRoot
$summaryPath = Join-Path $root 'openspec/implementation-summary.json'
$ledgerPath = Join-Path $root 'openspec/implementation-ledger.json'
$discoveryPath = Join-Path $root '.agent-memory/zworkflow/local/implementation-discovery.json'
$summary = Build-Summary $root

switch ($Command) {
    'validate' {
        $summary | ConvertTo-Json -Depth 100
        break
    }
    'refresh' {
        Write-DeterministicJson $summaryPath $summary
        $summary | ConvertTo-Json -Depth 100
        break
    }
    'discover' {
        Write-DeterministicJson $summaryPath $summary
        $discovery = Build-Discovery $root $summary
        Write-DeterministicJson $discoveryPath $discovery
        $discovery | ConvertTo-Json -Depth 100
        break
    }
    'checkpoint' {
        $discovery = Build-Discovery $root $summary
        if (@(Get-PropertyValue $discovery 'unmappedCSharpChanges' @()).Count -gt 0 -or @(Get-PropertyValue $discovery 'staleEvidence' @()).Count -gt 0) {
            throw '仍存在未映射 C# 变化或过期证据，不能建立审计检查点。'
        }
        $ledger = Read-JsonFile $ledgerPath
        if ($null -eq $ledger) { $ledger = [pscustomobject]@{ schemaVersion = 3; updatedAt = ''; discoveryRevision = ''; entries = @() } }
        $revision = (Get-GitOutput $root @('rev-parse', 'HEAD') | Select-Object -First 1)
        if ([string]::IsNullOrWhiteSpace($revision)) { throw '无法读取 Git HEAD，不能建立审计检查点。' }
        $ledger.schemaVersion = 3
        if ($null -eq $ledger.PSObject.Properties['discoveryRevision']) { $ledger | Add-Member -NotePropertyName discoveryRevision -NotePropertyValue $revision } else { $ledger.discoveryRevision = $revision }
        $ledger.updatedAt = [DateTimeOffset]::Now.ToString('o')
        Write-DeterministicJson $ledgerPath $ledger
        $summary = Build-Summary $root
        Write-DeterministicJson $summaryPath $summary
        if (Test-Path -LiteralPath $discoveryPath -PathType Leaf) { Remove-Item -LiteralPath $discoveryPath -Force }
        $summary | ConvertTo-Json -Depth 100
        break
    }
}
