# Building & Running on an Android Phone

This guide gets `imr-labs` running on your Android device instead of the PC webcam.
Do steps 1-6 once; from then on it's just **Connect → Build → Run**.

---

## Step 0 - What you need

| Thing | Where to get it |
|---|---|
| Android phone with ARCore support | Check your model at <https://developers.google.com/ar/devices> (any mid-range phone from ~2018+ usually works) |
| USB cable (data, not charge-only) | - |
| Unity 6000.3.25f1 with **Android Build Support** | Unity Hub → Installs → 6000.3.25f1 → **Add modules** → tick **Android Build Support** (+ *OpenJDK* and *Android SDK & NDK Tools* sub-items) |
| The two marker images printed | `Assets/Markers/export-carton-box-494777482.jpg` and `Assets/Markers/target_PNG66-22885455.jpeg` |

> If the modules are already installed, Unity Hub shows a checkmark instead of "Add modules" — skip it.

---

## Step 1 - Switch the build target to Android

1. `File → Build Profiles` (in older menus: `File → Build Settings`)
2. Select **Android → Switch Platform** (takes 1-5 min; Unity re-imports for a different backend)

---

## Step 2 - Player Settings (the exact values that matter)

`Edit → Project Settings → Player` (Android tab / robot icon):

| Setting | Value |
|---|---|
| Company / Product Name | anything you like |
| **Other Settings → Identification → Minimum API Level** | **Android 8.0 (Oreo, API 26)** or higher |
| **Other Settings → Identification → Target API Level** | "Automatic (highest installed)" |
| **Other Settings → Rendering → Graphics APIs** | remove Vulkan, keep **OpenGLES3** (Vuforia 11 doesn't do Vulkan) |
| **Other Settings → Identification → Scripting Backend** | IL2CPP (recommended; Mono also works for testing) |
| **XR Plug-in Management → Android tab** | tick **Vuforia Engine AR** |

> The project already ships with `minSdk 26` defaults from your teammate — you mainly need to
> verify Graphics API = OpenGLES3 and tick Vuforia in XR Plug-in Management.

---

## Step 3 - Prepare the phone

1. **Settings → About phone → tap "Build number" 7 times** → unlocks Developer options
2. **Settings → System → Developer options → enable "USB debugging"**
3. Plug the phone in with USB → a popup appears on the phone: **"Allow USB debugging?" → Allow**
4. If Windows makes a sound but Unity's device list stays empty, install your phone maker's USB driver (Samsung/Google/Xiaomi driver page), or install the Google USB Driver via Windows Device Manager.

---

## Step 4 - Google ARCore (required by Vuforia on device)

Vuforia uses ARCore for camera tracking on Android. The project config already has
`autoImportArcore: 1`, so Unity fetches what it needs on first build (internet required).
Nothing to do manually.

---

## Step 5 - Build & Run

1. `File → Build Profiles → Android → Build And Run` (or `Build And Run` button)
2. First dialog asks where to save the APK → choose `Builds/imr-labs.apk` (folder is created automatically, and `Builds/` is gitignored)
3. Wait for the build (first time: 5-15 min because IL2CPP + shader compile; later builds are 1-3 min)
4. The app auto-installs and auto-launches on the phone.
   - The phone will ask **"Allow app to take pictures and record video?" → Allow** (camera permission - Vuforia needs it)

**If "Build And Run" device dropdown shows no device:** open a terminal and run `adb devices` (SDK adb is at
`%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe`). If your phone shows as "unauthorized", re-allow the debugging popup on the phone.

---

## Step 6 - Demo on the phone (your recording)

1. Print both markers (or show them on a tablet screen). Keep them flat.
2. Point the phone camera at both markers, ~30-50 cm away.
3. Both cacti appear **Idle**. Slide the markers closer than **25 cm** → **Attack mode**: VFX puff, whoosh sound, screen shows distance + "ATTACK!".
4. Separate them again → Idle.
5. Record: enable screen recording on the phone (**Settings → Quick Settings → add "Screen record" tile**), or film the scene with a second phone (this actually looks better as proof since you see hands + markers + phone screen).

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| "Vulkan not supported by Vuforia" build error | Player → Other Settings → Graphics APIs → remove Vulkan |
| Black camera feed | Grant camera permission; check lighting; "Google Play Services for AR" must be installed on the phone |
| App installs but crashes at start | Confirm XR Plug-in Management has **Vuforia Engine AR** ticked on the **Android** tab (not just PC tab) |
| Markers detected but jumpy | Print bigger (~10 cm wide), matte paper, good lighting |
| `SDK not found` during build | Unity Hub → Installs → Add modules → Android SDK & NDK Tools |
| Camera rotated weird in portrait | Player → Resolution → Default Orientation = **Landscape Left** (or Portrait, just be consistent) |
