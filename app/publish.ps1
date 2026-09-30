# Builds single-file self-contained executables. Usage: .\publish.ps1 [-Rids win-x64,linux-x64,osx-arm64]
param([string[]]$Rids = @('win-x64', 'linux-x64'))
$ErrorActionPreference = 'Stop'
$a = $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
$map = @{ 'win' = 'windows'; 'linux' = 'linux'; 'osx' = 'darwin' }
foreach ($rid in $Rids) {
    $os = $rid.Split('-')[0]
    $out = "$a\dist\$rid"
    dotnet publish "$a\SmaliPatcherReborn.csproj" -c Release -r $rid --self-contained true -o $out -nodeReuse:false `
        -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PtOs=$($map[$os]) -p:DebugType=none -p:DebugSymbols=false | Select-Object -Last 3
    Get-ChildItem $out | % { "{0}  {1:N1} MB" -f $_.Name, ($_.Length / 1MB) }
}
