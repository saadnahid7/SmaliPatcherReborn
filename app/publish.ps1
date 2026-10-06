# Builds single-file self-contained executables. Usage: .\publish.ps1 [-Rids win-x64,linux-x64,osx-arm64]
param([string[]]$Rids = @('win-x64', 'linux-x64'))
$ErrorActionPreference = 'Stop'
$a = $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
$map = @{ 'win' = 'windows'; 'linux' = 'linux'; 'osx' = 'darwin' }

# The wallets inside a release must be the ones in donate\wallets.json. A leftover test build in obj\ could carry
# other addresses, so every publish starts from a clean obj\ and then checks what actually got embedded.
$wallets = @((Get-Content "$a\..\donate\wallets.json" -Raw | ConvertFrom-Json).wallets)
$configured = $wallets.Count -gt 0 -and -not ($wallets | ? { $_.address -notmatch '^[A-Za-z0-9]{20,100}$' })
if ($configured) { "donate: embedding $($wallets.Count) wallets" } else { Write-Warning 'donate\wallets.json has no real addresses: this build will have no donation UI' }

foreach ($rid in $Rids) {
    $os = $rid.Split('-')[0]
    $out = "$a\dist\$rid"
    Remove-Item "$a\obj", "$a\bin" -Recurse -Force -ErrorAction SilentlyContinue
    dotnet publish "$a\SmaliPatcherReborn.csproj" -c Release -r $rid --self-contained true -o $out -nodeReuse:false `
        -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PtOs=$($map[$os]) -p:DebugType=none -p:DebugSymbols=false | Select-Object -Last 3
    if ($LASTEXITCODE) { throw "publish failed for $rid" }

    # the compiled assembly (before it is bundled) must hold exactly the configured addresses
    $dll = Get-ChildItem "$a\obj\Release" -Recurse -Filter SmaliPatcherReborn.dll | Select-Object -First 1
    if (-not $dll) { throw 'cannot find the compiled assembly to check the wallets' }
    $text = [Text.Encoding]::Latin1.GetString([IO.File]::ReadAllBytes($dll.FullName))
    foreach ($w in $wallets) {
        $has = $text.Contains([string]$w.address)
        if ($configured -and -not $has) { throw "wallet address for $($w.ticker) is missing from the build" }
    }
    foreach ($t in 'bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4', '0x0000000000000000000000000000000000000001', 'T9yD14Nj9j7xAB4dbGeiX9h8unkKHxuWwb') {   # the addresses lab\wallets.test.json uses
        if ($text.Contains($t)) { throw "the build contains a TEST wallet address ($t): stale test build" }
    }
    Get-ChildItem $out | % { "{0}  {1:N1} MB" -f $_.Name, ($_.Length / 1MB) }
}
