<#
  Baut "Emulator PC Hub.exe" (Release, self-contained inkl. Windows App SDK) nach .\App
  und legt im Stammordner eine Verknüpfung "Emulator PC Hub.lnk" an.

  Aufruf:  powershell -ExecutionPolicy Bypass -File .\build.ps1 [-SkipTests]
#>
param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out = Join-Path $root 'App'

if (-not $SkipTests) {
    Write-Host '== Tests ==' -ForegroundColor Cyan
    dotnet test --project (Join-Path $root 'tests\EmulatorPCHub.Tests\EmulatorPCHub.Tests.csproj')
    if ($LASTEXITCODE -ne 0) { throw 'Tests fehlgeschlagen' }
}

Get-Process 'Emulator PC Hub' -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host '== Publish ==' -ForegroundColor Cyan
dotnet publish (Join-Path $root 'src\EmulatorPCHub.App\EmulatorPCHub.App.csproj') `
    -c Release -r win-x64 --self-contained true -p:Platform=x64 -o $out
if ($LASTEXITCODE -ne 0) { throw 'Publish fehlgeschlagen' }

$exe = Join-Path $out 'Emulator PC Hub.exe'
$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut((Join-Path $root 'Emulator PC Hub.lnk'))
$lnk.TargetPath = $exe
$lnk.WorkingDirectory = $out
$lnk.IconLocation = Join-Path $out 'Assets\hub.ico'
$lnk.Save()

Write-Host "Fertig: $exe" -ForegroundColor Green
