param(
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe',
    [string]$Output = 'Builds/Windows/LittleGeorgies.exe'
)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path -LiteralPath $Unity)) { throw "Unity editor not found: $Unity" }
$destination = [IO.Path]::GetFullPath((Join-Path $project $Output))
$buildRoot = [IO.Path]::GetFullPath((Join-Path $project 'Builds')) + [IO.Path]::DirectorySeparatorChar
if (-not $destination.StartsWith($buildRoot, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($destination) -ne '.exe') {
    throw 'Output must be an .exe inside this project Builds folder.'
}
if (Get-Process LittleGeorgies -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $destination }) {
    throw 'That game build is running. Close it or select a separate -Output path.'
}
& (Join-Path $PSScriptRoot 'Install-AuctionSolver.ps1')
$artifacts = Join-Path $project 'Artifacts'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$log = Join-Path $artifacts 'build.log'
$arguments = "-batchmode -nographics -quit -projectPath `"$project`" -executeMethod PrototypeBuild.Build -lg-build-output `"$destination`" -logFile `"$log`""
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Unity build failed ($($process.ExitCode)). See $log" }
if (-not (Select-String -LiteralPath $log -SimpleMatch 'LITTLE_GEORGIES_BUILD:')) {
    throw "Unity did not report a completed build. See $log"
}
Write-Host "Built: $destination"
