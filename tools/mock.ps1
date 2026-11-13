# Composites a local test frame from the rendered layers (no watch needed). -Run N shows frame N of the running display, -Thermal the tap view.
param([string]$Digits = "1842", [int]$Run = -1, [switch]$Thermal, [string]$Out)   # -Thermal is obsolete since v2.1
Add-Type -AssemblyName System.Drawing
$d = (Resolve-Path (Join-Path $PSScriptRoot "..\res\drawable")).Path
$c = New-Object System.Drawing.Bitmap 466, 466; $g = [System.Drawing.Graphics]::FromImage($c); $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
function Put($n, $x, $y) { $im = [System.Drawing.Image]::FromFile("$d\$n.png"); $g.DrawImage($im, $x, $y, $im.Width, $im.Height); $im.Dispose() }
$pre = if ($Thermal) { "pr_t" } else { "pr_g" }
Put ($(if ($Thermal) { "pr_thermal" } else { "pr_bg" })) 0 0
$cx = @(37, 127, 259, 349); for ($i = 0; $i -lt 4; $i++) { Put "$pre$($Digits[$i])" ($cx[$i] - 14) (158 - 14) }
if ($Run -ge 0) { Put ("pr_r{0:00}" -f $Run) 23 144 }
$f = New-Object System.Drawing.Font "Consolas", 18, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$sf = New-Object System.Drawing.StringFormat; $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
$br = New-Object System.Drawing.SolidBrush ($(if ($Thermal) { [System.Drawing.Color]::White } else { [System.Drawing.Color]::FromArgb(255, 200, 30, 30) }))
$g.DrawString("FRI 13 NOV", $f, $br, (New-Object System.Drawing.RectangleF 133, 96, 200, 24), $sf)
$g.DrawString("76%", $f, $br, (New-Object System.Drawing.RectangleF 88, 346, 90, 24), $sf)
$g.DrawString("8426", $f, $br, (New-Object System.Drawing.RectangleF 183, 346, 100, 24), $sf)
$g.DrawString("72", $f, $br, (New-Object System.Drawing.RectangleF 288, 346, 90, 24), $sf)
$c.Save($Out); $g.Dispose()
