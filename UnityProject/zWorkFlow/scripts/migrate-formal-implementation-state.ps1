param(
    [string]$ProjectRoot = (Get-Location).Path,
    [switch]$KeepLegacyReview
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = [Text.UTF8Encoding]::new($false)

function Get-NormalizedSha256([string]$Path) {
    $content = [IO.File]::ReadAllText($Path).Replace("`r`n", "`n")
    $bytes = $utf8NoBom.GetBytes($content)
    $hash = [Security.Cryptography.SHA256]::HashData($bytes)
    return [Convert]::ToHexString($hash).ToLowerInvariant()
}

$specsRoot = Join-Path $ProjectRoot 'openspec/specs'
if (-not (Test-Path -LiteralPath $specsRoot -PathType Container)) {
    throw "Formal Spec directory not found: $specsRoot"
}

$migrated = 0
Get-ChildItem -LiteralPath $specsRoot -Recurse -Filter 'spec-review.json' -File | ForEach-Object {
    $reviewPath = $_.FullName
    $directory = $_.DirectoryName
    $specPath = Join-Path $directory 'spec.md'
    if (-not (Test-Path -LiteralPath $specPath -PathType Leaf)) {
        throw "Legacy formal review has no adjacent spec.md: $reviewPath"
    }

    $review = Get-Content -LiteralPath $reviewPath -Raw -Encoding utf8 | ConvertFrom-Json
    $blockingIssues = @($review.reviewIssues | Where-Object {
        $_ -and $_.blocksApproval -and $_.status -notin @('resolved', 'accepted', 'closed')
    })
    if ($blockingIssues.Count -gt 0) {
        throw "Cannot migrate unresolved formal review: $reviewPath"
    }

    $capability = if ($review.capability) { [string]$review.capability } else { Split-Path $directory -Leaf }
    $verification = $review.verification
    $implementation = [ordered]@{
        schemaVersion = 1
        artifactRole = 'formal-implementation-assertion'
        capability = $capability
        specHash = Get-NormalizedSha256 $specPath
        codeReadiness = if ($review.readiness) { [string]$review.readiness } else { 'unknown' }
        verification = [ordered]@{
            status = if ($verification.status) { [string]$verification.status } else { 'unverified' }
            summary = if ($verification.summary) { [string]$verification.summary } else { '' }
            validatedAgainstSpecHash = Get-NormalizedSha256 $specPath
            evidence = @($verification.codeEvidence)
            tests = @($verification.evidence)
            verifiedAt = if ($verification.verifiedAt) { [string]$verification.verifiedAt } else { '' }
        }
        implementationOutline = @($review.implementationOutline)
        editorGuidance = $review.editorGuidance
        sourceReferences = @($review.sourceReferences)
        publishedBy = [ordered]@{
            mode = 'legacy-formal-review-migration'
            changeId = ''
        }
    }

    if (-not $implementation.editorGuidance) {
        $implementation.Remove('editorGuidance')
    }
    $implementationPath = Join-Path $directory 'implementation.json'
    $json = $implementation | ConvertTo-Json -Depth 20
    [IO.File]::WriteAllText($implementationPath, $json + [Environment]::NewLine, $utf8NoBom)
    if (-not $KeepLegacyReview) {
        Remove-Item -LiteralPath $reviewPath
    }
    $script:migrated++
}

Write-Output "Migrated formal implementation assertions: $migrated"
