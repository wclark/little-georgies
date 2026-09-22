param([switch]$AuctionOnly, [string]$Exe)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
if (-not $Exe) { $Exe = Join-Path $project 'Builds\Windows\LittleGeorgies.exe' }
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build the Windows player first.' }
$sizes = @(@{Name='Desktop'; W=1600; H=900; Mode='-lg-village -lg-smoke'}, @{Name='TabletAspect'; W=1280; H=960; Mode='-lg-village -lg-smoke'}, @{Name='OpeningDesktop'; W=1600; H=900; Mode='-lg-opening-smoke'}, @{Name='OpeningTablet'; W=1280; H=960; Mode='-lg-opening-smoke'}, @{Name='AuctionDesktop'; W=1600; H=900; Mode='-lg-auction-smoke'}, @{Name='AuctionTablet'; W=1280; H=960; Mode='-lg-auction-smoke'})
if ($AuctionOnly) { $sizes = @($sizes | Where-Object { $_.Name -like 'Auction*' }) }
foreach ($size in $sizes) {
    $folder = Join-Path $project "Artifacts\$($size.Name)"
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $arguments = "$($size.Mode) -screen-width $($size.W) -screen-height $($size.H) -screen-fullscreen 0 -lg-capture `"$folder`" -logFile `"$folder\player.log`""
    $started = [DateTime]::UtcNow
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(60000)) { $process.Kill(); $process.WaitForExit(); throw "Smoke runner exceeded 60 seconds. PID: $($process.Id)" }
    if ((Get-Item -LiteralPath (Join-Path $folder 'smoke.json')).LastWriteTimeUtc -lt $started) { throw "No fresh report was produced: $folder" }
    $report = Get-Content -LiteralPath (Join-Path $folder 'smoke.json') -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $report.Passed) { throw "Runtime check failed. See $folder" }
    Write-Host "$($size.Name): passed at $($report.Width)x$($report.Height)."
}
