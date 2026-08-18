param(
    [Parameter(Mandatory = $true)][string]$Unity,
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path,
    [string]$BuildTarget = 'StandaloneWindows64',
    [string]$Output = '',
    [switch]$ForceIl2Cpp
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = Join-Path $ProjectPath 'Temp/RTSPlayerBuild/ZFrameworkRTSTest.exe'
}

function Invoke-Unity([string[]]$Arguments, [string]$LogName) {
    $logPath = Join-Path $ProjectPath "Temp/$LogName.log"
    & $Unity -batchmode -quit -projectPath $ProjectPath -logFile $logPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath $logPath -Tail 200
        throw "Unity failed with exit code $LASTEXITCODE. Log: $logPath"
    }
}

Invoke-Unity -Arguments @('-executeMethod', 'ZFramework.RTS.Editor.RtsSourcePromotion.ExportZeroRtsForBatch') -LogName 'rts-production-export'
$buildArguments = @(
    '-executeMethod', 'ZFramework.RTS.Editor.RtsCiBuild.BuildPlayerForBatch',
    '-rtsBuildTarget', $BuildTarget,
    '-rtsBuildOutput', $Output
)
if ($ForceIl2Cpp) { $buildArguments += '-rtsForceIl2Cpp' }
Invoke-Unity -Arguments $buildArguments -LogName 'rts-player-build'
Write-Host "Zero-RTS Player build passed: $Output"
