param(
    [string]$AppInstallDirectory,
    [string]$PluginInstallDirectory,
    [string]$ShortcutPath
)

$ErrorActionPreference = 'Stop'

$packageRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$appSourceDirectory = Join-Path $packageRoot 'app'
$pluginSourceDirectory = Join-Path $packageRoot 'openlp-plugin\scripturesync'
if ([string]::IsNullOrWhiteSpace($AppInstallDirectory)) {
    $AppInstallDirectory = Join-Path $env:LOCALAPPDATA 'ScriptureSync\App'
}
if ([string]::IsNullOrWhiteSpace($PluginInstallDirectory)) {
    $PluginInstallDirectory = Join-Path $env:APPDATA 'openlp\data\contrib\plugins\scripturesync'
}
if ([string]::IsNullOrWhiteSpace($ShortcutPath)) {
    $desktopDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
    $ShortcutPath = Join-Path $desktopDirectory 'ScriptureSync.lnk'
}
$pluginFiles = @('__init__.py', 'bridge.py', 'scripturesyncplugin.py')

if (-not (Test-Path -LiteralPath (Join-Path $appSourceDirectory 'ScriptureSync.App.exe'))) {
    throw 'The packaged ScriptureSync application was not found. Extract the entire ZIP before installing.'
}
if (-not (Test-Path -LiteralPath $pluginSourceDirectory)) {
    throw 'The packaged OpenLP plugin was not found. Extract the entire ZIP before installing.'
}
if (Get-Process -Name 'ScriptureSync.App' -ErrorAction SilentlyContinue) {
    throw 'Close ScriptureSync before installing or updating it.'
}

New-Item -ItemType Directory -Path $AppInstallDirectory -Force | Out-Null
Copy-Item -Path (Join-Path $appSourceDirectory '*') -Destination $AppInstallDirectory -Recurse -Force

$pluginIsCurrent = Test-Path -LiteralPath $PluginInstallDirectory
foreach ($pluginFile in $pluginFiles) {
    $sourceFile = Join-Path $pluginSourceDirectory $pluginFile
    $installedFile = Join-Path $PluginInstallDirectory $pluginFile
    if (-not (Test-Path -LiteralPath $installedFile) -or
        (Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $installedFile -Algorithm SHA256).Hash) {
        $pluginIsCurrent = $false
        break
    }
}

if ($pluginIsCurrent) {
    Write-Host 'The ScriptureSync OpenLP plugin is already installed and up to date.' -ForegroundColor Green
}
else {
    New-Item -ItemType Directory -Path $PluginInstallDirectory -Force | Out-Null
    foreach ($pluginFile in $pluginFiles) {
        Copy-Item -LiteralPath (Join-Path $pluginSourceDirectory $pluginFile) `
            -Destination (Join-Path $PluginInstallDirectory $pluginFile) -Force
    }
    Write-Host 'The ScriptureSync OpenLP plugin was installed or updated.' -ForegroundColor Green
}

$appExecutable = Join-Path $AppInstallDirectory 'ScriptureSync.App.exe'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($ShortcutPath)
$shortcut.TargetPath = $appExecutable
$shortcut.WorkingDirectory = $AppInstallDirectory
$shortcut.Description = 'Prepare and sync scripture passages to OpenLP'
$shortcut.Save()

Write-Host ''
Write-Host 'ScriptureSync was installed successfully.' -ForegroundColor Green
Write-Host "Application: $appExecutable"
Write-Host "Desktop shortcut: $ShortcutPath"
if (-not $pluginIsCurrent) {
    Write-Host 'Restart OpenLP after this plugin install or update.' -ForegroundColor Yellow
    Write-Host 'On first installation, activate ScriptureSync under Settings > Manage Plugins.'
}
