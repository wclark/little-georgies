$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $project 'Builds\Windows\LittleGeorgies.exe'
$portrait = Join-Path $project 'Assets\Resources\Art\GeorgieIcon.png'
$iconFolder = Join-Path $project 'Icons'
$icon = Join-Path $iconFolder 'LittleGeorgie.ico'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build the game before installing its shortcut.' }
New-Item -ItemType Directory -Path $iconFolder -Force | Out-Null
Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Image]::FromFile($portrait)
try { $width = $source.Width; $height = $source.Height } finally { $source.Dispose() }
if ($width -gt 256 -or $height -gt 256) { throw 'The shortcut portrait must be no larger than 256 pixels.' }
$png = [System.IO.File]::ReadAllBytes($portrait)
$writer = [System.IO.BinaryWriter]::new([System.IO.File]::Create($icon))
try {
    # Wrap the existing PNG in an ICO container without altering its image pixels.
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
    $writer.Write([byte]($width % 256)); $writer.Write([byte]($height % 256))
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$png.Length); $writer.Write([uint32]22)
    $writer.Write($png)
} finally { $writer.Dispose() }
$decoded = [System.Drawing.Icon]::new($icon)
try {
    if ($decoded.Width -ne $width -or $decoded.Height -ne $height) { throw 'Icon validation failed.' }
} finally { $decoded.Dispose() }
$link = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Little Georgies.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($link)
if ((Test-Path -LiteralPath $link) -and $shortcut.TargetPath -ne $exe) {
    throw 'The existing shortcut points elsewhere and was not replaced.'
}
$shortcut.TargetPath = $exe
$shortcut.Arguments = '-screen-fullscreen 0'
$shortcut.WorkingDirectory = Split-Path $exe -Parent
$shortcut.IconLocation = "$icon,0"
$shortcut.Description = 'Begin with one Little Georgie in an orchard and open field.'
$shortcut.WindowStyle = 1
$shortcut.Save()
$verified = $shell.CreateShortcut($link)
if ($verified.Arguments -match '-lg-village' -or $verified.IconLocation -ne "$icon,0") { throw 'Shortcut validation failed.' }
Write-Host "Updated: $link"
Write-Host "Icon: $icon"
Write-Host 'Starts with one gatherer, not the specialist sandbox.'
