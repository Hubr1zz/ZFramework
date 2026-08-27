[CmdletBinding()]
param(
    [ValidateSet('Compile', 'EditModeTests', 'PlayModeTests')]
    [string]$Action = 'Compile',
    [string]$UnityPath,
    [string]$ProjectPath,
    [string]$TestFilter,
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = $utf8NoBom
[Console]::OutputEncoding = $utf8NoBom
$OutputEncoding = $utf8NoBom

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $repositoryRoot 'UnityProject'
}
$ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)

$versionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $versionFile)) {
    throw "Unity project version file was not found: $versionFile"
}

$versionLine = Get-Content -LiteralPath $versionFile -Encoding UTF8 | Select-Object -First 1
$projectVersion = ($versionLine -replace '^m_EditorVersion:\s*', '').Trim()
if ([string]::IsNullOrWhiteSpace($projectVersion)) {
    throw "Unity project version could not be read from: $versionFile"
}

function Resolve-UnityExecutable {
    param([string]$RequestedPath, [string]$Version)

    $candidates = [System.Collections.Generic.List[string]]::new()
    foreach ($configuredPath in @($RequestedPath, $env:UNITY_EDITOR_PATH, $env:UNITYEDITOR_PATH)) {
        if ([string]::IsNullOrWhiteSpace($configuredPath)) {
            continue
        }
        $candidates.Add($configuredPath)
        $candidates.Add((Join-Path $configuredPath 'Unity.exe'))
        $candidates.Add((Join-Path $configuredPath 'Editor\Unity.exe'))
    }

    $candidates.Add((Join-Path 'D:\UnityVersions' "$Version\Editor\Unity.exe"))
    $candidates.Add((Join-Path $env:ProgramFiles "Unity\Hub\Editor\$Version\Editor\Unity.exe"))

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    throw "Unity $Version was not found. Set UNITY_EDITOR_PATH to Unity.exe or its Editor directory."
}

$unityExecutable = Resolve-UnityExecutable -RequestedPath $UnityPath -Version $projectVersion
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repositoryRoot '.agent-memory\unity-cli'
}
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

$runName = $Action.ToLowerInvariant()
$logPath = Join-Path $OutputPath "$runName.log"
$resultPath = Join-Path $OutputPath "$runName-results.xml"
$arguments = @('-batchmode', '-nographics', '-projectPath', $ProjectPath, '-logFile', $logPath)

if ($Action -eq 'Compile') {
    $arguments += '-quit'
}
else {
    $arguments += @('-runTests', '-testPlatform', $Action.Replace('Tests', ''), '-testResults', $resultPath)
    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        $arguments += @('-testFilter', $TestFilter)
    }
}

Write-Host "Unity CLI: $Action with $projectVersion"
Write-Host "Unity: $unityExecutable"
Write-Host "Log: $logPath"
$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $unityExecutable
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
foreach ($argument in $arguments) {
    $startInfo.ArgumentList.Add($argument)
}
$process = [System.Diagnostics.Process]::Start($startInfo)
$process.WaitForExit()
$exitCode = $process.ExitCode
$process.Dispose()

if (-not (Test-Path -LiteralPath $logPath)) {
    throw "Unity did not create the expected log: $logPath"
}

$compileErrors = Select-String -LiteralPath $logPath -Pattern 'error CS\d+|Compilation failed' -Encoding UTF8
if ($compileErrors) {
    $compileErrors | Select-Object -First 20 | ForEach-Object { Write-Error $_.Line }
    throw "Unity compilation failed. See $logPath"
}
if ($exitCode -ne 0) {
    throw "Unity exited with code $exitCode. See $logPath"
}

if ($Action -ne 'Compile') {
    if (-not (Test-Path -LiteralPath $resultPath)) {
        throw "Unity did not create test results: $resultPath"
    }
    [xml]$testResults = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8
    $testRun = $testResults.'test-run'
    if ($null -eq $testRun) {
        throw "Unity test results do not contain a test-run root: $resultPath"
    }
    Write-Host "Tests: total=$($testRun.total), passed=$($testRun.passed), failed=$($testRun.failed), skipped=$($testRun.skipped)"
    if ([int]$testRun.failed -gt 0 -or $testRun.result -ne 'Passed') {
        throw "Unity tests failed. See $resultPath and $logPath"
    }
    Write-Host "Results: $resultPath"
}
Write-Host 'Unity CLI completed successfully.'
