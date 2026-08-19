$ErrorActionPreference = 'Stop'

$source = Join-Path $PSScriptRoot '..\openlp-plugin\scripturesync'
$destination = Join-Path $env:APPDATA 'openlp\data\contrib\plugins\scripturesync'

if (-not (Test-Path -LiteralPath $source)) {
    throw "Plugin source was not found at $source"
}

New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $destination -Recurse -Force

Write-Host "Installed ScriptureSync plugin to $destination"
Write-Host 'Restart OpenLP, then activate ScriptureSync under Settings > Manage Plugins.'
