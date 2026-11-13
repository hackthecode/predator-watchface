# Compiles PredatorRender.cs and renders every bitmap into res/drawable.
Add-Type -AssemblyName System.Drawing
$src = Get-Content (Join-Path $PSScriptRoot "PredatorRender.cs") -Raw
Add-Type -TypeDefinition $src -ReferencedAssemblies System.Drawing, System.Drawing.Common, System.Drawing.Primitives, System.Runtime.InteropServices, System.Collections, System.Runtime, System.Private.Windows.GdiPlus, System.Private.Windows.Core, System.ComponentModel.Primitives, System.Runtime.Extensions -IgnoreWarnings
$out = (Resolve-Path (Join-Path $PSScriptRoot "..\res\drawable")).Path
[PredatorRender]::RenderAll($out)
Write-Host "rendered to $out"
