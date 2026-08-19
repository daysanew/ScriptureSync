$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$appProject = Join-Path $repositoryRoot 'src\ScriptureSync.App\ScriptureSync.App.csproj'
$appInstallDirectory = Join-Path $env:LOCALAPPDATA 'ScriptureSync\App'
$pluginSourceDirectory = Join-Path $repositoryRoot 'openlp-plugin\scripturesync'
$pluginInstallDirectory = Join-Path $env:APPDATA 'openlp\data\contrib\plugins\scripturesync'
$desktopDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
$shortcutPath = Join-Path $desktopDirectory 'ScriptureSync.lnk'

if (Get-Process -Name 'ScriptureSync.App' -ErrorAction SilentlyContinue) {
    throw 'Close ScriptureSync before installing or updating it.'
}

New-Item -ItemType Directory -Path $appInstallDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $pluginInstallDirectory -Force | Out-Null

& dotnet publish $appProject `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output $appInstallDirectory
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

foreach ($pluginFile in @('__init__.py', 'bridge.py', 'scripturesyncplugin.py')) {
    Copy-Item `
        -LiteralPath (Join-Path $pluginSourceDirectory $pluginFile) `
        -Destination (Join-Path $pluginInstallDirectory $pluginFile) `
        -Force
}

$appExecutable = Join-Path $appInstallDirectory 'ScriptureSync.App.exe'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $appExecutable
$shortcut.WorkingDirectory = $appInstallDirectory
$shortcut.Description = 'Prepare and sync scripture passages to OpenLP'
$shortcut.Save()

Write-Host ''
Write-Host 'ScriptureSync was installed successfully.' -ForegroundColor Green
Write-Host "Application: $appExecutable"
Write-Host "Desktop shortcut: $shortcutPath"
Write-Host "OpenLP plugin: $pluginInstallDirectory"
Write-Host ''
Write-Host 'Restart OpenLP after a plugin update.' -ForegroundColor Yellow
Write-Host 'On first installation, activate ScriptureSync under Settings > Manage Plugins.'
