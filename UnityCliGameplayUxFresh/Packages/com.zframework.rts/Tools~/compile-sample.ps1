param(
    [string]$Configuration = "Debug",
    [Parameter(Mandatory = $true)][string]$Session
)
$packageRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = Split-Path -Parent (Split-Path -Parent $packageRoot)
$contractAssembly = Join-Path $projectRoot 'Library/ScriptAssemblies/ZFramework.RTS.Contracts.dll'
if (-not (Test-Path -LiteralPath $contractAssembly)) { throw "Open Unity once to compile: $contractAssembly" }
$sourceRoot = Join-Path $projectRoot "RTSWorkspace/Sessions/$Session/Sources"
if (-not (Test-Path -LiteralPath $sourceRoot)) { throw "RTS Session source root does not exist: $sourceRoot" }
dotnet run --project (Join-Path $PSScriptRoot 'Compiler/ZFramework.RTS.Compiler.csproj') --configuration $Configuration -- `
    --source $sourceRoot `
    --output (Join-Path $projectRoot "Library/ZFrameworkRTS/Compiled/Sessions/$Session") `
    --reference $contractAssembly
exit $LASTEXITCODE
