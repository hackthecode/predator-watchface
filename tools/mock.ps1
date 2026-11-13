# Composites a local test frame from the rendered layers (no watch needed). -Thermal shows the tap view.
param([string]$Digits = "1842", [int]$Battery = 76, [int]$StepPct = 84, [switch]$Thermal, [string]$Out)
Add-Type -AssemblyName System.Drawing
$d = (Resolve-Path (Join-Path $PSScriptRoot "..\res\drawable")).Path
$c = New-Object System.Drawing.Bitmap 466, 466; $g = [System.Drawing.Graphics]::FromImage($c); $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
function Put($n, $x, $y) { $im = [System.Drawing.Image]::FromFile("$d\$n.png"); $g.DrawImage($im, $x, $y, $im.Width, $im.Height); $im.Dispose() }
$pre = if ($Thermal) { "pr_t" } else { "pr_g" }
Put ($(if ($Thermal) { "pr_thermal" } else { "pr_bg" })) 0 0
$cx = @(66, 148, 250, 332); for ($i = 0; $i -lt 4; $i++) { Put "$pre$($Digits[$i])" ($cx[$i] - 14) (146 - 14) }
$f = New-Object System.Drawing.Font "Consolas", 20, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$sf = New-Object System.Drawing.StringFormat; $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
$col = if ($Thermal) { [System.Drawing.Color]::White } else { [System.Drawing.Color]::FromArgb(255, 255, 60, 45) }
$br = New-Object System.Drawing.SolidBrush $col
$g.DrawString("$($Digits.Substring(0,2)):$($Digits.Substring(2,2))", $f, $br, (New-Object System.Drawing.RectangleF 163, 290, 140, 30), $sf)
$g.DrawString("FRI 13 NOV", $f, $br, (New-Object System.Drawing.RectangleF 163, 82, 140, 27), $sf)
$g.DrawString("72", $f, $br, (New-Object System.Drawing.RectangleF 190, 382, 86, 32), $sf)
if (-not $Thermal) {
  $bar = [System.Drawing.Image]::FromFile("$d\pr_bar.png")
  foreach ($p in @(@(108, $Battery), @(248, $StepPct))) { $bw = [int](1.1 * $p[1]); $g.DrawImage($bar, (New-Object System.Drawing.Rectangle $p[0], 352, $bw, 12), 0, 0, $bw, 12, 'Pixel') }
  $bar.Dispose(); Put pr_tri 300 150; Put pr_hl 0 0
}
$c.Save($Out); $g.Dispose()
