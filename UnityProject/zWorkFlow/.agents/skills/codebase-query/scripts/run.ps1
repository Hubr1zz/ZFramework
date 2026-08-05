[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('build', 'status', 'architecture', 'search', 'callers', 'impact', 'changed')]
    [string]$Command = 'status',
    [string]$Query,
    [string]$Path,
    [string]$Root = (Get-Location).Path,
    [string]$IndexPath = '.agent-memory/zworkflow/local/code-query-index.json',
    [string[]]$SourceRoots = @(),
    [string[]]$ExcludeRoots = @('Assets/Plugins', 'Assets/ThirdParty', 'Assets/External', 'Assets/Standard Assets'),
    [ValidateRange(1, 200)]
    [int]$Limit = 30,
    [switch]$Pretty
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'codebase-query requires PowerShell 7+ (pwsh) for consistent UTF-8 and cross-platform behavior.'
}
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = $utf8NoBom
[Console]::OutputEncoding = $utf8NoBom
$OutputEncoding = $utf8NoBom

$skillRoot = Split-Path -Parent $PSScriptRoot
$entrypoints = @(Get-ChildItem -LiteralPath $skillRoot -Recurse -Filter '*.ps1' -File |
    Where-Object { $_.FullName -ne $PSCommandPath -and
        (Select-String -LiteralPath $_.FullName -Pattern '^# codebase-query-entrypoint\s*$' -Quiet) })
if ($entrypoints.Count -ne 1) {
    throw "Expected exactly one codebase-query implementation under $skillRoot; found $($entrypoints.Count)."
}

$arguments = @{
    Command = $Command
    Root = $Root
    IndexPath = $IndexPath
    SourceRoots = $SourceRoots
    ExcludeRoots = $ExcludeRoots
    Limit = $Limit
    Pretty = $Pretty
}
if ($PSBoundParameters.ContainsKey('Query')) { $arguments.Query = $Query }
if ($PSBoundParameters.ContainsKey('Path')) { $arguments.Path = $Path }

& $entrypoints[0].FullName @arguments
