$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $env:JAVA_HOME) { throw 'Set JAVA_HOME to a JDK 17+ (Android Studio ships one in its jbr folder).' }
$jdk  = Join-Path $env:JAVA_HOME 'bin'
$out  = Join-Path $root 'build\classes'
Remove-Item $out -Recurse -Force -EA 0
New-Item -ItemType Directory -Force $out | Out-Null
$src = Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.java | % FullName
& "$jdk\javac.exe" --release 17 -XDsuppressNotes -encoding UTF-8 -cp "$root\libs\*" -d $out $src
if ($LASTEXITCODE) { throw 'javac failed' }
"built -> $out"
