param(
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$appProject = Join-Path $repositoryRoot 'src\ScriptureSync.App\ScriptureSync.App.csproj'
$artifactsDirectory = Join-Path $repositoryRoot 'artifacts'
$projectXml = [xml](Get-Content -LiteralPath $appProject -Raw)
$version = [string]$projectXml.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'Could not determine the ScriptureSync version.'
}

$packageName = "ScriptureSync-$version-$Runtime"
$stagingDirectory = Join-Path $artifactsDirectory $packageName
$appDirectory = Join-Path $stagingDirectory 'app'
$zipPath = Join-Path $artifactsDirectory "$packageName.zip"

$resolvedRepositoryRoot = [IO.Path]::GetFullPath($repositoryRoot).TrimEnd('\') + '\'
$resolvedStagingDirectory = [IO.Path]::GetFullPath($stagingDirectory)
if (-not $resolvedStagingDirectory.StartsWith($resolvedRepositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to prepare a staging directory outside the repository: $resolvedStagingDirectory"
}

if (Test-Path -LiteralPath $stagingDirectory) {
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $appDirectory -Force | Out-Null

& dotnet publish $appProject `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --output $appDirectory
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'packaging\Install ScriptureSync.cmd') `
    -Destination $stagingDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'packaging\README.txt') `
    -Destination $stagingDirectory
New-Item -ItemType Directory -Path (Join-Path $stagingDirectory 'scripts') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\install-package.ps1') `
    -Destination (Join-Path $stagingDirectory 'scripts\install-package.ps1')
New-Item -ItemType Directory -Path (Join-Path $stagingDirectory 'openlp-plugin') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'openlp-plugin\scripturesync') `
    -Destination (Join-Path $stagingDirectory 'openlp-plugin\scripturesync') -Recurse

Compress-Archive -LiteralPath $stagingDirectory -DestinationPath $zipPath -CompressionLevel Optimal

$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
Set-Content -LiteralPath "$zipPath.sha256" -Value "$hash  $packageName.zip"

Write-Host ''
Write-Host 'Self-contained release package created:' -ForegroundColor Green
Write-Host $zipPath
Write-Host "SHA-256: $hash"
