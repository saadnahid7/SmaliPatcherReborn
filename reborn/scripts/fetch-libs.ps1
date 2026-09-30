# Downloads the dexlib2/smali jars the engine builds against into reborn\libs (not committed to git).
$ErrorActionPreference = 'Stop'
$l = Join-Path (Split-Path -Parent $PSScriptRoot) 'libs'
New-Item -ItemType Directory -Force $l | Out-Null
$g = 'https://maven.google.com/com/android/tools/smali'; $c = 'https://repo1.maven.org/maven2'
@("$g/smali-dexlib2/3.0.10/smali-dexlib2-3.0.10.jar", "$g/smali-baksmali/3.0.10/smali-baksmali-3.0.10.jar",
  "$g/smali-util/3.0.10/smali-util-3.0.10.jar", "$g/smali/3.0.10/smali-3.0.10.jar",
  "$c/com/google/guava/guava/31.1-android/guava-31.1-android.jar", "$c/com/google/guava/failureaccess/1.0.1/failureaccess-1.0.1.jar",
  "$c/com/google/code/findbugs/jsr305/3.0.2/jsr305-3.0.2.jar", "$c/org/jcommander/jcommander/1.85/jcommander-1.85.jar",
  "$c/org/antlr/antlr-runtime/3.5.2/antlr-runtime-3.5.2.jar", "$c/org/antlr/stringtemplate/3.2.1/stringtemplate-3.2.1.jar") | ForEach-Object {
    $f = Join-Path $l (Split-Path $_ -Leaf)
    if (-not (Test-Path $f)) { Invoke-WebRequest $_ -OutFile $f -UseBasicParsing }
}
"libs ready: $l"
