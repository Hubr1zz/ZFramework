[CmdletBinding()]
param([string]$Root = (Get-Location).Path)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'zworkflow-ui-maintenance layout audit requires PowerShell 7+ (pwsh).'
}

function Assert-LayoutRule {
    param([bool]$Condition, [string]$Message)

    if (-not $Condition) { throw "Layout audit failed: $Message" }
}

$projectRoot = [System.IO.Path]::GetFullPath($Root)
$workbenchRoot = Join-Path $projectRoot 'Assets/Editor/zWorkFlow'
$templateRoot = Join-Path $projectRoot 'zWorkFlow/setup/assets'
$changesPath = Join-Path $projectRoot 'Assets/Editor/zWorkFlow/AgentWorkbenchWindow.Changes.cs'
$markdownPath = Join-Path $projectRoot 'Assets/Editor/zWorkFlow/AgentWorkbenchWindow.ImportReports.cs'
$windowPath = Join-Path $projectRoot 'Assets/Editor/zWorkFlow/AgentWorkbenchWindow.cs'
$manifestPath = Join-Path $projectRoot 'zWorkFlow/setup/PACKAGE_MANIFEST.json'
$packagedSkillPath = Join-Path $projectRoot 'zWorkFlow/.agents/skills/zworkflow-ui-maintenance'
$changesTemplatePath = Join-Path $projectRoot 'zWorkFlow/setup/assets/AgentWorkbenchWindow.Changes.cs.template'
$markdownTemplatePath = Join-Path $projectRoot 'zWorkFlow/setup/assets/AgentWorkbenchWindow.ImportReports.cs.template'
$windowTemplatePath = Join-Path $projectRoot 'zWorkFlow/setup/assets/AgentWorkbenchWindow.cs.template'

Assert-LayoutRule (Test-Path -LiteralPath $workbenchRoot -PathType Container) 'Workbench source directory is missing.'
Assert-LayoutRule (Test-Path -LiteralPath $templateRoot -PathType Container) 'portable template directory is missing.'
Assert-LayoutRule (Test-Path -LiteralPath $changesPath -PathType Leaf) 'Changes page source is missing.'
Assert-LayoutRule (Test-Path -LiteralPath $markdownPath -PathType Leaf) 'shared Markdown renderer is missing.'
Assert-LayoutRule (Test-Path -LiteralPath $windowPath -PathType Leaf) 'shared Workbench window source is missing.'
Assert-LayoutRule (Test-Path -LiteralPath $manifestPath -PathType Leaf) 'package manifest is missing.'
Assert-LayoutRule (Test-Path -LiteralPath $changesTemplatePath -PathType Leaf) 'portable Changes template is missing.'
Assert-LayoutRule (Test-Path -LiteralPath $markdownTemplatePath -PathType Leaf) 'portable Markdown template is missing.'
Assert-LayoutRule (Test-Path -LiteralPath $windowTemplatePath -PathType Leaf) 'portable Workbench window template is missing.'

$changes = Get-Content -Raw -LiteralPath $changesPath -Encoding utf8
$markdown = Get-Content -Raw -LiteralPath $markdownPath -Encoding utf8
$window = Get-Content -Raw -LiteralPath $windowPath -Encoding utf8
$manifest = Get-Content -Raw -LiteralPath $manifestPath -Encoding utf8
$changesTemplate = Get-Content -Raw -LiteralPath $changesTemplatePath -Encoding utf8
$markdownTemplate = Get-Content -Raw -LiteralPath $markdownTemplatePath -Encoding utf8
$windowTemplate = Get-Content -Raw -LiteralPath $windowTemplatePath -Encoding utf8
$layoutSources = @(Get-ChildItem -LiteralPath $workbenchRoot -Filter 'AgentWorkbenchWindow*.cs' -File) +
    @(Get-ChildItem -LiteralPath $templateRoot -Filter 'AgentWorkbenchWindow*.cs.template' -File)
$globalViewWidthOffenders = @($layoutSources | Where-Object {
    (Get-Content -Raw -LiteralPath $_.FullName -Encoding utf8) -match 'EditorGUIUtility\.currentViewWidth'
})
$forcedWindowWidthOffenders = @($layoutSources | Where-Object {
    (Get-Content -Raw -LiteralPath $_.FullName -Encoding utf8) -match 'Mathf\.Max\(\s*(?:1[8-9]\d|[2-9]\d\d)[fF]?\s*,\s*position\.width'
})

Assert-LayoutRule ($changes -match 'ChangeDetailContentWidth\(\)') 'Changes details must use a local width budget.'
Assert-LayoutRule ($window -match 'CurrentLayoutContentWidth\(') 'the shared local width budget helper is missing.'
Assert-LayoutRule ($windowTemplate -match 'CurrentLayoutContentWidth\(') 'the portable local width budget helper is missing.'
Assert-LayoutRule ($globalViewWidthOffenders.Count -eq 0) "nested Workbench content reads global Editor view width: $(@($globalViewWidthOffenders | ForEach-Object Name) -join ', ')."
Assert-LayoutRule ($forcedWindowWidthOffenders.Count -eq 0) "Workbench content forces a window-derived visual minimum: $(@($forcedWindowWidthOffenders | ForEach-Object Name) -join ', ')."
Assert-LayoutRule ($markdown -notmatch 'Mathf\.Max\(40[^\r\n]*\(width[^\r\n]*/\s*columns') 'Markdown table columns can still force their parent wider.'
Assert-LayoutRule ($changesTemplate.Replace("`r`n", "`n") -eq $changes.Replace("`r`n", "`n")) 'portable Changes template differs from the installed Editor page.'
Assert-LayoutRule ($markdownTemplate.Replace("`r`n", "`n") -eq $markdown.Replace("`r`n", "`n")) 'portable Markdown template differs from the installed shared renderer.'
Assert-LayoutRule ($manifest -notmatch 'zworkflow-ui-maintenance') 'the project-only skill was added to the migration manifest.'
Assert-LayoutRule (-not (Test-Path -LiteralPath $packagedSkillPath)) 'the project-only skill was copied into the portable zWorkFlow package.'

[pscustomobject]@{
    passed = $true
    pages = $layoutSources.Count
    minimumWindow = '900x600'
    checks = 12
    packaged = $false
} | ConvertTo-Json -Compress
