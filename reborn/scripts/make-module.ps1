# Packs module\ + build\dist\engine.jar into build\SmaliPatcherReborn-<version>.zip (forward-slash entries, Magisk/KSU/APatch layout)
$ErrorActionPreference = 'Stop'
$root  = Split-Path -Parent $PSScriptRoot
$stage = Join-Path $root 'build\module-stage'
if (-not $env:JAVA_HOME) { throw 'Set JAVA_HOME to a JDK 17+.' }
$jdk   = Join-Path $env:JAVA_HOME 'bin'
Remove-Item $stage -Recurse -Force -EA 0
New-Item -ItemType Directory -Force "$stage\bin","$stage\META-INF\com\google\android" | Out-Null
Copy-Item "$root\module\*" $stage -Recurse -Force
Copy-Item "$root\build\dist\engine.jar" "$stage\bin\engine.jar" -Force
# Magisk installer entry points
"#MAGISK`n" | Set-Content "$stage\META-INF\com\google\android\updater-script" -NoNewline -Encoding ascii
$ub = Join-Path $root 'module-installer\update-binary'
if (Test-Path $ub) { Copy-Item $ub "$stage\META-INF\com\google\android\update-binary" }
else { throw "missing $ub (pinned copy of Magisk module_installer.sh)" }
# LF endings for scripts
Get-ChildItem $stage -Recurse -Include *.sh,update-binary,module.prop | % {
    $t = [IO.File]::ReadAllText($_.FullName) -replace "`r`n", "`n"
    [IO.File]::WriteAllText($_.FullName, $t, (New-Object Text.UTF8Encoding $false))
}
$ver = ((Get-Content "$root\module\module.prop" | ? { $_ -like 'version=*' }) -replace 'version=', '')
$zip = Join-Path $root "build\SmaliPatcherReborn-$ver.zip"
Remove-Item $zip -EA 0
& "$jdk\jar.exe" cfM $zip -C $stage .
"built $zip ({0:N0} bytes)" -f (Get-Item $zip).Length
