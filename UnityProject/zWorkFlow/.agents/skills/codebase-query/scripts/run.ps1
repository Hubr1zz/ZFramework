[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('build', 'status', 'architecture', 'search', 'callers', 'impact', 'changed', 'context')]
    [string]$Command = 'status',
    [string]$Query,
    [string]$Path,
    [string]$Root = (Get-Location).Path,
    [string]$IndexPath = '.agents/codebase-query/code-query-index.json',
    [string]$ProgressPath = '.agent-memory/zworkflow/local/code-query-progress.json',
    [string]$StatePath = '.agent-memory/zworkflow/local/code-query-state.json',
    [string[]]$SourceRoots = @(),
    [string[]]$ExcludeRoots = @(),
    [switch]$IncludeAll,
    [ValidateRange(1, 200)]
    [int]$Limit = 8,
    [switch]$IncludeLexical,
    [switch]$IncludeMethods,
    [ValidateRange(2048, 1048576)]
    [int]$MaxOutputBytes = 12288,
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
    ProgressPath = $ProgressPath
    StatePath = $StatePath
    SourceRoots = $SourceRoots
    ExcludeRoots = $ExcludeRoots
    IncludeAll = $IncludeAll
    Limit = $Limit
    IncludeLexical = $IncludeLexical
    IncludeMethods = $IncludeMethods
    MaxOutputBytes = $MaxOutputBytes
    Pretty = $Pretty
}
if ($PSBoundParameters.ContainsKey('Query')) { $arguments.Query = $Query }
if ($PSBoundParameters.ContainsKey('Path')) { $arguments.Path = $Path }

& $entrypoints[0].FullName @arguments
