# Fetches what the app embeds: the module zip and the PC engine (both built in ..\reborn) and Google's platform-tools (fallback adb).
$ErrorActionPreference = 'Stop'
$a = $PSScriptRoot
New-Item -ItemType Directory -Force "$a\Assets", "$a\cache" | Out-Null
foreach ($p in 'windows', 'linux', 'darwin') {
    $f = "$a\cache\platform-tools-$p.zip"
    if (-not (Test-Path $f)) { Invoke-WebRequest "https://dl.google.com/android/repository/platform-tools-latest-$p.zip" -OutFile $f -UseBasicParsing }
}
$m = Get-ChildItem "$a\..\reborn\build\SmaliPatcherReborn-*.zip" | Sort-Object LastWriteTime | Select-Object -Last 1
if (-not $m) { throw 'Build the module first: reborn\scripts\fetch-libs.ps1, dex.ps1, make-module.ps1' }
Copy-Item $m.FullName "$a\Assets\module.zip" -Force
$e = "$a\..\reborn\build\dist\engine-pc.jar"
if (-not (Test-Path $e)) { throw 'Build the PC engine first: reborn\scripts\pcjar.ps1' }
Copy-Item $e "$a\Assets\engine-pc.jar" -Force
"assets ready"
