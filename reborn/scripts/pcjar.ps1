# Builds build\dist\engine-pc.jar : the engine + dexlib2 + guava as one runnable JVM jar (for manual jar patching on a PC).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $env:JAVA_HOME) { throw 'Set JAVA_HOME to a JDK 17+.' }
& "$PSScriptRoot\build.ps1" | Out-Null
$tmp = Join-Path $root 'build\pcjar-stage'
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
New-Item -ItemType Directory -Force $tmp, "$root\build\dist" | Out-Null
$jar = "$env:JAVA_HOME\bin\jar.exe"
foreach ($n in 'failureaccess-1.0.1', 'guava-31.1-android', 'jsr305-3.0.2', 'smali-dexlib2-3.0.10', 'smali-util-3.0.10') {
    Push-Location $tmp; & $jar xf "$root\libs\$n.jar"; Pop-Location
}
Copy-Item "$root\build\classes\*" $tmp -Recurse -Force
Get-ChildItem "$tmp\META-INF" -Include *.SF, *.RSA, *.DSA -Recurse -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item $_.FullName -Force }
$out = "$root\build\dist\engine-pc.jar"
if (Test-Path $out) { Remove-Item $out -Force }
& $jar cfe $out reborn.Main -C $tmp .
"{0:N1} MB -> {1}" -f ((Get-Item $out).Length / 1MB), $out
