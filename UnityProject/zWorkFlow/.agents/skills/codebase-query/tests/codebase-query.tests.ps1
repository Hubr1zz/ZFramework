[CmdletBinding()]
param([string]$Root = (Get-Location).Path)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'codebase-query tests require PowerShell 7+ (pwsh).'
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "Assertion failed: $Message" }
}

$projectRoot = [System.IO.Path]::GetFullPath($Root)
$skillRoot = Split-Path -Parent $PSScriptRoot
$queryScript = Join-Path $skillRoot 'scripts/run.ps1'
$localRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot '.agent-memory/zworkflow/local'))
New-Item -ItemType Directory -Path $localRoot -Force | Out-Null

$build = & $queryScript build -Root $projectRoot | ConvertFrom-Json
Assert-True ($build.fileCount -gt 0) 'at least one target C# file should be indexed.'
Assert-True ($build.qualifiedTypeCount -gt 0) 'qualified C# types should be indexed.'
Assert-True ($build.coveragePercent -eq 100 -and $build.missingFileCount -eq 0 -and $build.unexpectedFileCount -eq 0) `
    'the default build must verify complete Assets C# coverage.'
$repeatBuild = & $queryScript build -Root $projectRoot | ConvertFrom-Json
Assert-True ($repeatBuild.parsedFileCount -eq 0 -and $repeatBuild.reusedFileCount -eq $repeatBuild.fileCount) `
    'a repeated manual build should reuse every unchanged file extraction.'

$relocationRoot = Join-Path $localRoot ("codebase-query-relocation-" + [Guid]::NewGuid().ToString('N'))
try {
    Copy-Item -LiteralPath $skillRoot -Destination $relocationRoot -Recurse
    $implementationRoot = Join-Path $relocationRoot 'implementation/renamed'
    New-Item -ItemType Directory -Path $implementationRoot -Force | Out-Null
    Move-Item -LiteralPath (Join-Path $relocationRoot 'scripts/codebase-query.ps1') `
        -Destination (Join-Path $implementationRoot 'query-engine.ps1')
    Move-Item -LiteralPath (Join-Path $relocationRoot 'scripts/lib/csharp-binding.ps1') `
        -Destination (Join-Path $implementationRoot 'type-binding.ps1')
    $relocatedStatus = & (Join-Path $relocationRoot 'scripts/run.ps1') status -Root $projectRoot |
        ConvertFrom-Json
    Assert-True ($relocatedStatus.schemaVersion -eq 5) `
        'renaming and moving implementation scripts inside the skill must preserve the stable entrypoint.'
}
finally {
    if (Test-Path -LiteralPath $relocationRoot) {
        $resolvedRelocationRoot = [System.IO.Path]::GetFullPath($relocationRoot)
        $relativeCleanupPath = [System.IO.Path]::GetRelativePath($localRoot, $resolvedRelocationRoot).Replace('\', '/')
        if ($relativeCleanupPath -eq '..' -or $relativeCleanupPath.StartsWith('../', [StringComparison]::Ordinal)) {
            throw 'Refusing to clean a relocation test path outside the local cache root.'
        }
        Remove-Item -LiteralPath $resolvedRelocationRoot -Recurse -Force
    }
}

$fixtureRoot = Join-Path $localRoot ("codebase-query-unity-fixture-" + [Guid]::NewGuid().ToString('N'))
try {
    $fixtureCodeRoot = Join-Path $fixtureRoot 'Assets/GameRuntime/Code'
    $fixtureUnicodeRoot = Join-Path $fixtureRoot 'Assets/中文目录'
    $fixturePackageRoot = Join-Path $fixtureRoot 'Packages/LocalGame/Runtime'
    New-Item -ItemType Directory -Path $fixtureCodeRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $fixtureUnicodeRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $fixturePackageRoot -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $fixtureRoot 'ProjectSettings') -Force | Out-Null
    @'
namespace PortableFixture
{
    public class BaseView
    {
        public virtual void Click() { }
    }
}
'@ | Set-Content -LiteralPath (Join-Path $fixtureCodeRoot 'BaseView.cs') -Encoding utf8
    @'
namespace PortableFixture
{
    public sealed class DerivedView : BaseView
    {
        public override void Click() { base.Click(); }
    }
}
'@ | Set-Content -LiteralPath (Join-Path $fixtureCodeRoot 'DerivedView.cs') -Encoding utf8
    @'
namespace PortableFixture
{
    public sealed class PortableService
    {
        public void Run() { }
    }

    public static class PortableEvents
    {
        public static void Publish<T>(T value) { }
    }
}
'@ | Set-Content -LiteralPath (Join-Path $fixtureCodeRoot 'Services.cs') -Encoding utf8
    @'
using ServiceAlias = PortableFixture.PortableService;

namespace PortableFixture
{
    public sealed class PortableConsumer
    {
        private readonly PortableService _service = new PortableService();
        private readonly ServiceAlias _aliasedService = new ServiceAlias();
        public void Tick()
        {
            _service.Run();
            _aliasedService.Run();
            PortableEvents.Publish<int>(1);
        }
    }
}
'@ | Set-Content -LiteralPath (Join-Path $fixtureCodeRoot 'PortableConsumer.cs') -Encoding utf8
    @'
namespace PortableFixture.PackageCode
{
    public sealed class EmbeddedPackageService
    {
        public void Execute() { }
    }

    public sealed class EmbeddedPackageConsumer
    {
        public void Tick()
        {
            var service = new EmbeddedPackageService();
            service.Execute();
        }
    }
}
'@ | Set-Content -LiteralPath (Join-Path $fixturePackageRoot 'EmbeddedPackage.cs') -Encoding utf8
    @'
namespace PortableFixture
{
    public sealed class UnicodePathConsumer
    {
        private readonly PortableService _service = new PortableService();
        public void Tick() { _service.Run(); }
    }
}
'@ | Set-Content -LiteralPath (Join-Path $fixtureUnicodeRoot '中文调用者.cs') -Encoding utf8

    $serviceResult = & $queryScript callers -Root $fixtureRoot -Query 'PortableService.Run' -Limit 20 |
        ConvertFrom-Json
    $servicePaths = @($serviceResult.resolvedCallers | ForEach-Object { $_.path })
    Assert-True ($servicePaths -contains 'Assets/GameRuntime/Code/PortableConsumer.cs') `
        "default Unity discovery must work without an Assets/Scripts convention. Result: $($serviceResult | ConvertTo-Json -Depth 6 -Compress)"
    Assert-True (@($serviceResult.resolvedCallers | Where-Object { $_.receiver -eq '_aliasedService' }).Count -eq 1) `
        'using aliases should resolve without recursive alias expansion.'

    $eventResult = & $queryScript callers -Root $fixtureRoot -Query 'PortableEvents.Publish' -Limit 20 |
        ConvertFrom-Json
    $eventPaths = @($eventResult.resolvedCallers | ForEach-Object { $_.path })
    Assert-True ($eventPaths -contains 'Assets/GameRuntime/Code/PortableConsumer.cs') `
        'generic static method calls should resolve.'

    $baseResult = & $queryScript callers -Root $fixtureRoot -Query 'BaseView.Click' -Limit 20 |
        ConvertFrom-Json
    $basePaths = @($baseResult.resolvedCallers | ForEach-Object { $_.path })
    Assert-True ($basePaths -contains 'Assets/GameRuntime/Code/DerivedView.cs') `
        'base method calls should resolve through inheritance.'

    Move-Item -LiteralPath (Join-Path $fixtureCodeRoot 'PortableConsumer.cs') `
        -Destination (Join-Path $fixtureCodeRoot 'RenamedPortableConsumer.cs')
    $renamedResult = & $queryScript callers -Root $fixtureRoot -Query 'PortableService.Run' -Limit 20 |
        ConvertFrom-Json
    $renamedPaths = @($renamedResult.resolvedCallers | ForEach-Object { $_.path })
    Assert-True (($renamedPaths -contains 'Assets/GameRuntime/Code/RenamedPortableConsumer.cs') -and
        ($renamedPaths -notcontains 'Assets/GameRuntime/Code/PortableConsumer.cs')) `
        'renaming or moving a target C# file must invalidate stale cached paths.'
    $incrementalStatus = & $queryScript status -Root $fixtureRoot | ConvertFrom-Json
    Assert-True ($incrementalStatus.parsedFileCount -eq 1 -and $incrementalStatus.reusedFileCount -eq 4) `
        'renaming one file should parse the new path once and reuse all unchanged file facts.'

    $invalidIndexPath = Join-Path $fixtureRoot 'invalid-index.json'
    '{}' | Set-Content -LiteralPath $invalidIndexPath -Encoding utf8
    $recoveredStatus = & $queryScript status -Root $fixtureRoot -IndexPath $invalidIndexPath | ConvertFrom-Json
    Assert-True ($recoveredStatus.schemaVersion -eq 5 -and $recoveredStatus.fileCount -eq 5) `
        'a valid JSON file with an incompatible cache shape should be discarded and rebuilt.'

    $packageResult = & $queryScript callers -Root $fixtureRoot `
        -SourceRoots @('Assets', 'Packages/LocalGame') -Query 'EmbeddedPackageService.Execute' -Limit 20 |
        ConvertFrom-Json
    $packagePaths = @($packageResult.resolvedCallers | ForEach-Object { $_.path })
    Assert-True ($packagePaths -contains 'Packages/LocalGame/Runtime/EmbeddedPackage.cs') `
        'configured project-owned source roots should support embedded Unity package layouts.'

    $unicodeResult = & $queryScript callers -Root $fixtureRoot -Query 'PortableService.Run' -Limit 20 |
        ConvertFrom-Json
    $unicodePaths = @($unicodeResult.resolvedCallers | ForEach-Object { $_.path })
    Assert-True ($unicodePaths -contains 'Assets/中文目录/中文调用者.cs') `
        'UTF-8 Chinese paths should survive indexing and JSON output.'

    & git -C $fixtureRoot init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'git init failed for the portable-path fixture.' }
    $changedResult = & $queryScript changed -Root $fixtureRoot -Limit 50 | ConvertFrom-Json
    Assert-True (@($changedResult.changedCSharpFiles) -contains 'Assets/中文目录/中文调用者.cs') `
        'git status parsing should preserve an untracked Chinese C# path.'
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        $resolvedFixtureRoot = [System.IO.Path]::GetFullPath($fixtureRoot)
        $relativeCleanupPath = [System.IO.Path]::GetRelativePath($localRoot, $resolvedFixtureRoot).Replace('\', '/')
        if ($relativeCleanupPath -eq '..' -or $relativeCleanupPath.StartsWith('../', [StringComparison]::Ordinal)) {
            throw 'Refusing to clean a Unity fixture outside the local cache root.'
        }
        Remove-Item -LiteralPath $resolvedFixtureRoot -Recurse -Force
    }
}

[pscustomobject]@{
    passed = $true
    schemaVersion = 5
    targetFileCount = $build.fileCount
    qualifiedTypeCount = $build.qualifiedTypeCount
    resolvedCallCount = $build.resolvedCallCount
    assertions = 15
} | ConvertTo-Json -Compress
