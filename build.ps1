# Builds Predator watch face APK (resource-only, no code) and optionally installs it.
# Usage: .\build.ps1            -> build only
#        .\build.ps1 -Install   -> build + adb install to the connected device
param([switch]$Install, [string]$Serial)
$ErrorActionPreference = "Stop"
if (-not $env:JAVA_HOME) { $env:JAVA_HOME = [Environment]::GetEnvironmentVariable("JAVA_HOME","User") }
$root = $PSScriptRoot
$sdk  = "$env:LOCALAPPDATA\Android\Sdk"
$bt   = "$sdk\build-tools\37.0.0"
$jar  = "$sdk\platforms\android-34\android.jar"
$out  = "$root\build"
Remove-Item -Recurse -Force $out -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "$out\compiled" | Out-Null

& "$bt\aapt2.exe" compile --dir "$root\res" -o "$out\compiled"
if ($LASTEXITCODE) { throw "aapt2 compile failed" }
$flats = Get-ChildItem "$out\compiled\*.flat" | ForEach-Object FullName
& "$bt\aapt2.exe" link -o "$out\app-unsigned.apk" -I $jar --manifest "$root\AndroidManifest.xml" @flats
if ($LASTEXITCODE) { throw "aapt2 link failed" }
& "$bt\zipalign.exe" -f -p 4 "$out\app-unsigned.apk" "$out\app-aligned.apk"

$ks = "$env:USERPROFILE\.android\debug.keystore"
if (-not (Test-Path $ks)) {
  New-Item -ItemType Directory -Force (Split-Path $ks) | Out-Null
  & "$env:JAVA_HOME\bin\keytool.exe" -genkeypair -keystore $ks -storepass android -keypass android -alias androiddebugkey `
    -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=Android Debug,O=Android,C=US"
}
& "$bt\apksigner.bat" sign --ks $ks --ks-pass pass:android --key-pass pass:android --out "$out\predator.apk" "$out\app-aligned.apk"
if ($LASTEXITCODE) { throw "apksigner failed" }
Write-Host "Built $out\predator.apk"

if ($Install) {
  $adb = "$sdk\platform-tools\adb.exe"
  $s = if ($Serial) { @("-s", $Serial) } else { @() }
  & $adb @s install -r "$out\predator.apk"
}
