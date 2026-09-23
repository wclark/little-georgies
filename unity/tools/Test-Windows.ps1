param(
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe',
    [string]$Output = 'Builds/PipelineCheck/Windows/LittleGeorgies.exe'
)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$repo = Split-Path $project -Parent
$dotnet = Join-Path $project '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$oldTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
$oldHome = $env:DOTNET_CLI_HOME
$oldRevision = $env:GIT_COMMIT
Push-Location $repo
try {
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_CLI_HOME = Join-Path $project '.tools/dotnet-home'
    $env:GIT_COMMIT = (& git rev-parse HEAD)
    if ($LASTEXITCODE -ne 0) { throw 'Could not identify the git revision.' }
    if (& git status --porcelain) { $env:GIT_COMMIT += '-dirty' }
    & $dotnet run --project unity/tests/CoreChecks --configuration Release -- unity/Artifacts/core-checks.json
    if ($LASTEXITCODE -ne 0) { throw 'Core checks failed. Install .NET SDK 10 if the SDK is missing.' }
    & (Join-Path $PSScriptRoot 'Build-Windows.ps1') -Unity $Unity -Output $Output
    $exe = [IO.Path]::GetFullPath((Join-Path $project $Output))
    & (Join-Path $PSScriptRoot 'Smoke-Windows.ps1') -Exe $exe
    $root = Split-Path $exe -Parent
    $native = Get-ChildItem -LiteralPath $root -Recurse -File |
        Where-Object { $_.Name -match '(?i)(ortools|Google\.Protobuf)' }
    if ($native) { throw 'The player unexpectedly contains editor-only solver dependencies.' }
    $assembly = Join-Path $root 'LittleGeorgies_Data/Managed/Assembly-CSharp.dll'
    $manifest = [ordered]@{
        passed = $true
        completedUtc = [DateTime]::UtcNow.ToString('O')
        revision = $env:GIT_COMMIT
        executable = $exe
        assemblySha256 = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
        modelChecks = Get-Content -LiteralPath (Join-Path $project 'Artifacts/core-checks.json') -Raw | ConvertFrom-Json
        build = Get-Content -LiteralPath (Join-Path $root 'build-info.json') -Raw | ConvertFrom-Json
        smoke = @('Desktop', 'TabletAspect', 'OpeningDesktop', 'OpeningTablet', 'AuctionDesktop', 'AuctionTablet', 'DossierDesktop', 'DossierTablet', 'DossierWide') |
            ForEach-Object { Get-Content -LiteralPath (Join-Path $project "Artifacts/$_/smoke.json") -Raw | ConvertFrom-Json }
    }
    $manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $project 'Artifacts/pipeline.json') -Encoding utf8
    Write-Host 'Pipeline passed. Evidence: unity/Artifacts/pipeline.json'
} finally {
    Pop-Location
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = $oldTelemetry
    $env:DOTNET_CLI_HOME = $oldHome
    $env:GIT_COMMIT = $oldRevision
}
