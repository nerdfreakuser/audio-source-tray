$ErrorActionPreference = "Stop"

Get-Process -Name "AudioSourceTray" -ErrorAction SilentlyContinue | Stop-Process -Force

$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
Remove-ItemProperty -Path $runKey -Name "AudioSourceTray" -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\Software\AudioSourceTray" -Recurse -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AudioSourceTray" -Recurse -ErrorAction SilentlyContinue

$desktop = Join-Path ([Environment]::GetFolderPath("Desktop")) "Audio Source.lnk"
$startMenu = Join-Path ([Environment]::GetFolderPath("Programs")) "Audio Source.lnk"
Remove-Item $desktop -Force -ErrorAction SilentlyContinue
Remove-Item $startMenu -Force -ErrorAction SilentlyContinue

$out = Join-Path $env:LOCALAPPDATA "AudioSourceTray"
if (Test-Path $out) {
    $self = $MyInvocation.MyCommand.Path
    if ($self -and $self.StartsWith($out, [System.StringComparison]::OrdinalIgnoreCase)) {
        Start-Process cmd.exe -WindowStyle Hidden -ArgumentList "/c timeout /t 1 /nobreak >nul & rd /s /q `"$out`""
    }
    else {
        Remove-Item $out -Recurse -Force
    }
}

Write-Host "Audio Source has been removed."
