$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$cache = Join-Path $root 'Artifacts\NuGet'
$plugins = Join-Path $root 'Assets\Plugins\AuctionSolver'
New-Item -ItemType Directory -Force -Path $cache, $plugins, "$plugins\x86_64" | Out-Null
$packages = @(
    @('google.ortools', '9.15.6755', '690E9EDFD5CD531787DA72EBA4283E674F893B66CFCA22F0A8BFC9B83A9AD78C'),
    @('google.ortools.runtime.win-x64', '9.15.6755', 'F2B4D1BEF2A02FA1C615E4BBA5E5CD567ABD174A8E611D50B69AA2C3E1979B81'),
    @('google.protobuf', '3.33.1', '18A8E2A83815DB52DB560B9A109FFF49236E0C8EF3BBB9F4A2D11B628259531F')
)
foreach ($package in $packages) {
    $id, $version, $hash = $package
    $zip = Join-Path $cache "$id.$version.zip"
    if (!(Test-Path -LiteralPath $zip)) {
        Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$id/$version/$id.$version.nupkg" -OutFile $zip
    }
    if ((Get-FileHash -LiteralPath $zip).Hash -ne $hash) { throw "Package hash mismatch: $id" }
    Expand-Archive -LiteralPath $zip -DestinationPath "$cache\$id" -Force
}
Copy-Item -LiteralPath "$cache\google.ortools\lib\net462\Google.OrTools.dll" -Destination $plugins -Force
Copy-Item -LiteralPath "$cache\google.protobuf\lib\net45\Google.Protobuf.dll" -Destination $plugins -Force
Get-ChildItem -LiteralPath "$cache\google.ortools.runtime.win-x64\runtimes\win-x64\native" -Filter *.dll |
    Copy-Item -Destination "$plugins\x86_64" -Force
Write-Host 'Pinned Windows x64 auction solver installed.'
