# Predator — watch face for Wear OS

An alien hunter's wrist computer for round Wear OS watches (built for the OnePlus Watch 3, 466×466). The time is written in an invented numeral system on a red display set into scarred dark alloy; a tap switches the whole face to heat vision.

<p align="center">
  <img src="res/drawable/preview.png" width="330" alt="Predator watch face">
  <img src="docs/thermal.png" width="330" alt="Predator watch face, thermal view">
</p>

Both pictures are screenshots from the watch.

## Reading the numerals

Each of the four cells is one digit of HH:MM, read like a tally:

| Mark | Value |
| --- | --- |
| slanted bars, stacked from the bottom | one each, up to four |
| hooked spine on the left | five |
| lone diamond | zero |

So spine + two bars is 7, four bars is 4, the spine alone is 5. The time is repeated in ordinary digits underneath.

## Features

- Start-up: the cells flick through random numerals for half a second each time the watch wakes
- Tap the display for four seconds of thermal view: white-hot numerals, the pulse as a hot spot, battery and steps as text
- A three-dot ranging mark wanders over the display with the tilt of the wrist, and the reflection on the glass slides with it
- Date, heart rate, and two gauges: PWR (battery) and TRK (steps toward the daily goal)
- Always-on mode: thin dim numerals and the readable time only

## How it is built

Pure [Watch Face Format v2](https://developer.android.com/training/wearables/wff) — the APK contains resources only, no code. Every bitmap is rendered procedurally.

- `tools/PredatorRender.cs` draws the housing, numerals, thermal view and animation frames; `tools/render.ps1` compiles and runs it into `res/drawable`.
- `tools/gen-xml.ps1` writes `res/raw/watchface.xml`.
- `tools/mock.ps1` composites a test frame locally, without a watch.
- `build.ps1` builds the APK without Gradle (aapt2 → zipalign → apksigner, debug key).

```powershell
.\tools\render.ps1
.\tools\gen-xml.ps1
.\build.ps1 -Install
```

Requires PowerShell 7, the Android SDK (build-tools 37, platform android-34) and a JDK.

## Install

Grab the APK from [Releases](../../releases) and sideload it with `adb install predator.apk`, then long-press the current watch face and pick **Predator**. Needs Wear OS 4 or newer.

The design, the numeral system and all artwork are original to this project. Fonts: a bold monospace under the SIL Open Font License.
