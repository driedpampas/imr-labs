# Bonus Features - What Was Added & How To Tweak It

This round added three bonus features on top of the base lab (which was already working).
Everything is **drop-in**: no scene changes, no prefab changes, nothing your teammate did was touched.

---

## Files added

| File | Purpose |
|---|---|
| `Assets/Scripts/Bonus/ARProximityBonus.cs` | Distance HUD + SFX + VFX orchestration (self-wires at runtime) |
| `Assets/Scripts/Bonus/ImpactVFX.cs` | Self-contained one-shot impact puff particle effect |
| `Assets/Scripts/Bonus/SkinSwapDemo.cs` | OPTIONAL: on-screen button to cycle character color skins |
| `Assets/Resources/Audio/attack_whoosh.wav` | Synthesized attack whoosh SFX (0.9 s) |
| `Assets/Resources/VFX/PuffMaterial.mat` | URP transparent emissive material for the puff particles |
| `Assets/Editor/SceneSmokeTest.cs` | Editor-only batch-mode sanity check (harmless in normal use) |

## How it works (no scene wiring needed)

`ARProximityBonus` self-bootstraps:

1. `FindFirstObjectByType<ARProximityTrigger>()` finds the existing trigger and reads its `attackDistance`
   (via reflection, so both scripts never disagree).
2. Every frame it collects all `ObserverBehaviour`s with status `TRACKED` / `EXTENDED_TRACKED`.
   - Fewer than 2 tracked â†’ HUD says "Show both markers to the camera", force Idle.
3. Two tracked â†’ computes distance, updates the HUD, and applies **hysteresis**:
   attack turns ON at â‰¤ 0.25 m but OFF only past 0.25 + 0.03 m (stops flicker right at the threshold).
4. On attack start: plays the whoosh at full volume + spawns an impact puff between the cacti + "punch-scale"
   bounce on both cacti. While they stay in attack mode, a new puff + a quieter whoosh fire every 0.85 s.

## Tweaking (all in one place)

Open `Assets/Scripts/Bonus/ARProximityBonus.cs` â†’ the header fields:

| Field | Default | Meaning |
|---|---|---|
| `attackDistance` | 0.25 | Fallback threshold; real value is read from `ARProximityTrigger` (change it **there**) |
| `hysteresis` | 0.03 | Extra distance required to calm down (anti-flicker) |
| `vfxRetriggerInterval` | 0.85 | Seconds between repeat puffs/whooshes while attacking |

Sound file: replace `Assets/Resources/Audio/attack_whoosh.wav` with any WAV/OGG (keep the same filename) and
it just works. Want a different color puff? Open `Assets/Resources/VFX/PuffMaterial.mat` in the Inspector and
change the base/emission color.

Particle tuning lives in `ImpactVFX.cs` (`particleSize`, `startSpeed`, `duration`).

## Skin swap (optional, kept separate)

A small **"Skin"** button appears bottom-left in the built app (and `S` on PC).
Each tap cycles: original â†’ red â†’ purple â†’ gold â†’ cyan â†’ original. Each cactus keeps its own tint.
The class is self-contained - delete `SkinSwapDemo.cs` if you don't want the button; nothing else references it.

## Verification done

- `Unity -batchmode` compile: exit code 0, zero `error CS` (Unity 6000.3.25f1, same version as the project).
- Batch-mode smoke test (`SceneSmokeTest`): scene loads, both bonus types resolve, whoosh + puff material
  load from Resources, 0 log errors. Output: `SMOKETEST scene=OK bonusScript=OK vfxScript=OK whoosh=OK puffMat=OK logErrors=0`.
- Not yet verified: actual on-device / webcam runtime (needs your eyes + a camera) - that's the recording step.

---

## Unity-chan character swap (added later)

**Model:** the official Unity-chan (unitychan.fbx) with the **FightingUnity-chan Free** fighting
animations (Idle loop, Jab, Rising Punch, High Kick) - shipped under the Unity-chan License,
see [UnityChanLicense.md](UnityChanLicense.md).

**How it works (still zero scene edits):**
- Assets/Editor/UnityChanSetupBuilder.cs generates two assets at build/test time:
  Resources/UnityChan/UnityChanOverride.overrideController (the cactus animator with
  Idle->chan Idle, Attack->chan Jab clips swapped) and Resources/UnityChan/UnityChanAR.prefab
  (model + Animator + avatar, scaled to 0.4 to match the cactus in AR).
- Assets/Scripts/Bonus/CharacterSwapDemo.cs spawns one Unity-chan instance under each image
  target (hidden), remaps its legacy materials to URP Lit at spawn time, and gives you a
  bottom-left **Model** button: Cacti -> Unity-chan -> Mixed (chan vs cactus!) -> Cacti.
- Both models are driven by the same isAttacking bool and face each other when fighting,
  so every bonus feature (HUD, SFX, VFX, punch-scale) works identically for either character.
- The old color Skin button is now bottom-right (key C), model swap button bottom-left.

**Files:** Assets/UnityChan/ (model + 4 animation FBX + textures), the two generated files
under Assets/Resources/UnityChan/ (committed, so phones build without regenerating), and
CharacterSwapDemo.cs. Re-run Tools > Unity-chan > Build AR Prefab only if you change
animation mappings.