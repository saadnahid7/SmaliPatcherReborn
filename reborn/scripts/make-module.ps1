# Packs module\ + build\dist\engine.jar into build\SmaliPatcherReborn-<version>.zip (forward-slash entries, Magisk/KSU/APatch layout)
# -Wallets <file> builds with other wallet addresses (tests only); that zip goes to build/test so it can never be mistaken for a release.
param([string]$Wallets = '')
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
# WebUI donate card: wallets are inlined only when every address looks real; otherwise the card stays hidden
$testWallets = $Wallets -ne ''
if (-not $testWallets) { $Wallets = Join-Path (Split-Path -Parent $root) 'donate\wallets.json' }
$list = @(); try { $list = @((Get-Content $Wallets -Raw | ConvertFrom-Json).wallets) } catch { }
$walletsOk = $list.Count -gt 0 -and -not ($list | ? { $_.address -notmatch '^[A-Za-z0-9]{20,100}$' })
$json = if ($walletsOk) { ConvertTo-Json -InputObject @($list | % { [ordered]@{ coin = $_.coin; ticker = $_.ticker; network = $_.network; address = $_.address } }) -Compress } else { '[]' }
$idx = "$stage\webroot\index.html"
$html = [IO.File]::ReadAllText($idx)
if (-not $html.Contains('/*WALLETS*/[]')) { throw 'index.html lost its wallet marker' }
[IO.File]::WriteAllText($idx, $html.Replace('/*WALLETS*/[]', $json), (New-Object Text.UTF8Encoding $false))
$ver = ((Get-Content "$root\module\module.prop" | ? { $_ -like 'version=*' }) -replace 'version=', '')
if ($testWallets) { New-Item -ItemType Directory -Force "$root\build\test" | Out-Null; $zip = Join-Path $root "build\test\SmaliPatcherReborn-TESTWALLETS-$ver.zip" }
else { $zip = Join-Path $root "build\SmaliPatcherReborn-$ver.zip" }
Remove-Item $zip -EA 0
& "$jdk\jar.exe" cfM $zip -C $stage .
"built $zip ({0:N0} bytes){1}" -f (Get-Item $zip).Length, $(if ($walletsOk) { "  [donate card: $($list.Count) wallets]" } else { "  [donate card hidden: no wallet addresses]" })
