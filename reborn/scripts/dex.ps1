# Builds build\dist\engine.jar : the engine + dexlib2 converted to dex so it runs on the phone via app_process.
# Needs: JAVA_HOME (JDK 17+), ANDROID_HOME (SDK with build-tools and a platform), and reborn\libs (scripts\fetch-libs.ps1).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $env:JAVA_HOME) { throw 'Set JAVA_HOME to a JDK 17+.' }
$sdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } elseif ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } else { throw 'Set ANDROID_HOME to your Android SDK.' }
$bt   = (Get-ChildItem (Join-Path $sdk 'build-tools') -Directory | Sort-Object Name | Select-Object -Last 1).FullName
$plat = (Get-ChildItem (Join-Path $sdk 'platforms') -Directory | Sort-Object Name | Select-Object -Last 1).FullName
& "$PSScriptRoot\build.ps1" | Out-Null
New-Item -ItemType Directory -Force "$root\build\dist" | Out-Null
& "$env:JAVA_HOME\bin\jar.exe" cf "$root\build\dist\reborn-classes.jar" -C "$root\build\classes" .
$libs = 'failureaccess-1.0.1', 'guava-31.1-android', 'jsr305-3.0.2', 'smali-dexlib2-3.0.10', 'smali-util-3.0.10' | ForEach-Object { "$root\libs\$_.jar" }
Remove-Item "$root\build\dist\engine.jar" -ErrorAction SilentlyContinue
& "$bt\d8.bat" --release --min-api 29 --lib "$plat\android.jar" --output "$root\build\dist\engine.jar" "$root\build\dist\reborn-classes.jar" @libs 2>&1 | Select-Object -Last 15
"{0:N0} bytes" -f (Get-Item "$root\build\dist\engine.jar").Length
