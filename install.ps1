$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $env:LOCALAPPDATA "AudioSourceTray"
$csproj = Join-Path $root "AudioSourceTray.csproj"
$packagedExe = Join-Path $root "AudioSourceTray.exe"

function New-AudioSourceShortcut {
    param(
        [Parameter(Mandatory = $true)][string]$ShortcutPath,
        [Parameter(Mandatory = $true)][string]$TargetPath
    )

    $dir = Split-Path -Parent $ShortcutPath
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.WorkingDirectory = Split-Path -Parent $TargetPath
    $shortcut.WindowStyle = 7
    $shortcut.Description = "Shows which app and device are playing audio"
    $shortcut.IconLocation = "$TargetPath,0"
    $shortcut.Save()
}

Get-Process -Name "AudioSourceTray" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

if (-not (Test-Path $out)) {
    New-Item -ItemType Directory -Path $out | Out-Null
}

$exe = Join-Path $out "AudioSourceTray.exe"

if (Test-Path $csproj) {
    Write-Host "Publishing Audio Source to $out"
    $publishDir = Join-Path $root "dist"
    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }
    dotnet publish $csproj -c Release -r win-x64 --self-contained true -o $publishDir /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed. Install the .NET 10 SDK or use a GitHub release zip."
    }
    Copy-Item (Join-Path $publishDir "AudioSourceTray.exe") $exe -Force
}
elseif (Test-Path $packagedExe) {
    Write-Host "Installing packaged Audio Source to $out"
    Copy-Item $packagedExe $exe -Force
}
else {
    throw "Nothing to install. Run this from a cloned repo (with the .NET 10 SDK) or from a release zip."
}

Copy-Item (Join-Path $root "uninstall.ps1") (Join-Path $out "uninstall.ps1") -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root "icon.ico") (Join-Path $out "icon.ico") -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root "LICENSE") (Join-Path $out "LICENSE") -Force -ErrorAction SilentlyContinue

$desktop = Join-Path ([Environment]::GetFolderPath("Desktop")) "Audio Source.lnk"
$startMenu = Join-Path ([Environment]::GetFolderPath("Programs")) "Audio Source.lnk"
New-AudioSourceShortcut -ShortcutPath $desktop -TargetPath $exe
New-AudioSourceShortcut -ShortcutPath $startMenu -TargetPath $exe

$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AudioSourceTray"
New-Item -Path $uninstallKey -Force | Out-Null
$uninstallCommand = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $out 'uninstall.ps1')`""
New-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value "Audio Source" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "DisplayIcon" -Value $exe -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "Publisher" -Value "Audio Source" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $out -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value $uninstallCommand -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value "1.1.0" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "URLInfoAbout" -Value "https://github.com/nerdfreakuser/audio-source-tray" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "NoModify" -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "NoRepair" -Value 1 -PropertyType DWord -Force | Out-Null
$exeSizeKb = [math]::Round((Get-Item $exe).Length / 1KB)
New-ItemProperty -Path $uninstallKey -Name "EstimatedSize" -Value $exeSizeKb -PropertyType DWord -Force | Out-Null

$started = $false
for ($i = 0; $i -lt 6; $i++) {
    Start-Process $exe
    Start-Sleep -Milliseconds 400
    if (Get-Process -Name "AudioSourceTray" -ErrorAction SilentlyContinue) {
        $started = $true
        break
    }
}

if (-not $started) {
    Write-Warning "The app did not stay running. Try launching $exe yourself."
}

$iconRoot = "HKCU:\Control Panel\NotifyIconSettings"
if (Test-Path $iconRoot) {
    Get-ChildItem $iconRoot | ForEach-Object {
        $props = Get-ItemProperty $_.PSPath
        if ($props.ExecutablePath -like "*AudioSourceTray.exe") {
            New-ItemProperty -Path $_.PSPath -Name "IsPromoted" -Value 1 -PropertyType DWord -Force | Out-Null
        }
    }
}

$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
New-ItemProperty -Path $runKey -Name "AudioSourceTray" -Value "`"$exe`"" -PropertyType String -Force | Out-Null
New-Item -Path "HKCU:\Software\AudioSourceTray" -Force | Out-Null
New-ItemProperty -Path "HKCU:\Software\AudioSourceTray" -Name "Initialized" -Value 1 -PropertyType DWord -Force | Out-Null

Write-Host "Installed Audio Source."
Write-Host "Desktop shortcut: $desktop"
Write-Host "Start menu: $startMenu"
Write-Host "Hover the equalizer tray icon to see what is playing."
