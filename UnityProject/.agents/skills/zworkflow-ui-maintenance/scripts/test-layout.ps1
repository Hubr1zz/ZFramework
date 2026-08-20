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
$changesPath = Join-Path $projectRoot 'Assets/Editor/zWorkFlow/AgentWorkbenchWindow.Changes.cs'
$markdownPath = Join-Path $projectRoot 'Assets/Editor/zWorkFlow/AgentWorkbenchWindow.ImportReports.cs'
$manifestPath = Join-Path $projectRoot 'zWorkFlow/setup/PACKAGE_MANIFEST.json'
$packagedSkillPath = Join-Path $projectRoot 'zWorkFlow/.agents/skills/zworkflow-ui-maintenance'
$changesTemplatePath = Join-Path $projectRoot 'zWorkFlow/setup/assets/AgentWorkbenchWindow.Changes.cs.template'
$markdownTemplatePath = Join-Path $projectRoot 'zWorkFlow/setup/assets/AgentWorkbenchWindow.ImportReports.cs.template'

Assert-LayoutRule (Test-Path -LiteralPath $changesPath -PathType Leaf) 'Changes page source is missing.'
Assert-LayoutRule (Test-Path -LiteralPath $markdownPath -PathType Leaf) 'shared Markdown renderer is missing.'
Assert-LayoutRule (Test-Path -LiteralPath $manifestPath -PathType Leaf) 'package manifest is missing.'
Assert-LayoutRule (Test-Path -LiteralPath $changesTemplatePath -PathType Leaf) 'portable Changes template is missing.'
Assert-LayoutRule (Test-Path -LiteralPath $markdownTemplatePath -PathType Leaf) 'portable Markdown template is missing.'

$changes = Get-Content -Raw -LiteralPath $changesPath -Encoding utf8
$markdown = Get-Content -Raw -LiteralPath $markdownPath -Encoding utf8
$manifest = Get-Content -Raw -LiteralPath $manifestPath -Encoding utf8
$changesTemplate = Get-Content -Raw -LiteralPath $changesTemplatePath -Encoding utf8
$markdownTemplate = Get-Content -Raw -LiteralPath $markdownTemplatePath -Encoding utf8

Assert-LayoutRule ($changes -match 'ChangeDetailContentWidth\(\)') 'Changes details must use a local width budget.'
Assert-LayoutRule ($changes -notmatch 'Mathf\.Max\(320[^\r\n]*position\.width') 'Changes details still force a window-derived 320px minimum.'
Assert-LayoutRule ($changes -notmatch 'EditorGUIUtility\.currentViewWidth') 'nested Changes content still reads the global Editor view width.'
Assert-LayoutRule ($markdown -notmatch 'Mathf\.Max\(40[^\r\n]*\(width[^\r\n]*/\s*columns') 'Markdown table columns can still force their parent wider.'
Assert-LayoutRule ($changesTemplate.Replace("`r`n", "`n") -eq $changes.Replace("`r`n", "`n")) 'portable Changes template differs from the installed Editor page.'
Assert-LayoutRule ($markdownTemplate.Replace("`r`n", "`n") -eq $markdown.Replace("`r`n", "`n")) 'portable Markdown template differs from the installed shared renderer.'
Assert-LayoutRule ($manifest -notmatch 'zworkflow-ui-maintenance') 'the project-only skill was added to the migration manifest.'
Assert-LayoutRule (-not (Test-Path -LiteralPath $packagedSkillPath)) 'the project-only skill was copied into the portable zWorkFlow package.'

[pscustomobject]@{
    passed = $true
    page = 'OpenSpec/Changes'
    minimumWindow = '900x600'
    checks = 8
    packaged = $false
} | ConvertTo-Json -Compress
