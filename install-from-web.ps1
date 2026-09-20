# Install the latest Audio Source release from GitHub.
# irm https://raw.githubusercontent.com/nerdfreakuser/audio-source-tray/main/install-from-web.ps1 | iex

$ErrorActionPreference = "Stop"
$repo = "nerdfreakuser/audio-source-tray"
$zipName = "AudioSource-win-x64.zip"

Write-Host "Fetching latest Audio Source release..."
$release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" -Headers @{
    "User-Agent" = "AudioSource-Installer"
}
$asset = $release.assets | Where-Object { $_.name -eq $zipName } | Select-Object -First 1
if (-not $asset) {
    throw "The latest GitHub release does not include $zipName."
}

$temp = Join-Path $env:TEMP "AudioSource-install"
if (Test-Path $temp) {
    Remove-Item $temp -Recurse -Force
}
New-Item -ItemType Directory -Path $temp | Out-Null
$zip = Join-Path $temp $zipName

Write-Host "Downloading $($release.tag_name)..."
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip -UseBasicParsing
Expand-Archive -Path $zip -DestinationPath $temp -Force

$installer = Join-Path $temp "install.ps1"
if (-not (Test-Path $installer)) {
    throw "The release zip did not contain install.ps1."
}

& $installer
