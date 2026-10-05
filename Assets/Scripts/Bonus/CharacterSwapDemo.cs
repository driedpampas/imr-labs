using System.Collections.Generic;
using UnityEngine;
using Vuforia;

// In-game character swap: cycles each AR target's character between the
// original cactus and Unity-chan. Modes: 0 = both cacti, 1 = both chan,
// 2 = mixed (chan vs cactus). Bottom-left button.
//
// Also owns model VISIBILITY: Vuforia's DefaultObserverEventHandler disables
// child renderers when a target is lost - which left the cactus permanently
// invisible after any mode switch. We force the active model's renderers on
// every frame and stream a 1-second ARDebug line to logcat for remote diagnosis.
public class CharacterSwapDemo : MonoBehaviour
{
    private const float ModelYawOffset = 0f;

    private static readonly Dictionary<string, string> TextureBySlot = new Dictionary<string, string>
    {
        { "body",     "Textures/body_01" },
        { "face",     "Textures/face_00" },
        { "hair",     "Textures/hair_01" },
        { "eyebrow",  "Textures/hair_01" },
        { "skin",     "Textures/skin_01" },
        { "eye_l1",   "Textures/eye_iris_L_00" },
        { "eye_r1",   "Textures/eye_iris_R_00" },
        { "eyeline",  "Textures/eyeline_00" },
        { "eyebase",  "" },
        { "mat_cheek","Textures/cheek_00" },
        { "cheek",    "Textures/cheek_00" },
    };

    private readonly List<ObserverBehaviour> targets = new List<ObserverBehaviour>();
    private readonly Dictionary<ObserverBehaviour, GameObject> cactusByTarget = new Dictionary<ObserverBehaviour, GameObject>();
    private readonly Dictionary<ObserverBehaviour, GameObject> chanByTarget = new Dictionary<ObserverBehaviour, GameObject>();
    private readonly Dictionary<GameObject, Renderer[]> rendererCache = new Dictionary<GameObject, Renderer[]>();

    private ARProximityBonus bonus;
    private int mode;
    private float nextScanTime;
    private float nextLogTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<CharacterSwapDemo>() == null)
        {
            var go = new GameObject("CharacterSwapDemo");
            DontDestroyOnLoad(go);
            go.AddComponent<CharacterSwapDemo>();
        }
    }

    private void Start()
    {
        bonus = FindFirstObjectByType<ARProximityBonus>();
        // Feed true rendered-character positions to the distance/fight logic.
        ARProximityBonus.ModelPositionProvider = GetModelPositions;
    }

    // Returns the world positions of the two ACTIVE models on tracked targets,
    // or null when fewer than two are valid. Distance between characters =
    // distance between what the user actually sees on the markers.
    private Vector3[] GetModelPositions()
    {
        var result = new List<Vector3>();
        foreach (var kvp in cactusByTarget)
        {
            ObserverBehaviour t = kvp.Key;
            if (t == null) continue;
            Status st = t.TargetStatus.Status;
            if (st != Status.TRACKED && st != Status.EXTENDED_TRACKED) continue;

            GameObject model = chanByTarget.TryGetValue(t, out GameObject chan) && chan != null && chan.activeSelf
                ? chan
                : kvp.Value;
            if (model == null || !model.activeInHierarchy) continue;
            result.Add(model.transform.position);
        }
        return result.Count == 2 ? result.ToArray() : null;
    }

    private void Update()
    {
        EnsureInstances();
        ApplyMode();
        MirrorCombatState();
        DebugTick();
    }

    private void OnGUI()
    {
        float scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / 420f);
        var rect = new Rect(16f * scale, Screen.height - 66f * scale, 240f * scale, 50f * scale);
        string label = mode == 0 ? "Model: Cacti" : mode == 1 ? "Model: Unity-chan" : "Model: Mixed";
        if (GUI.Button(rect, label))
            NextMode();
    }

    private void NextMode()
    {
        mode = (mode + 1) % 3;
        ApplyMode();
    }

    private void EnsureInstances()
    {
        if (Time.time < nextScanTime) return;
        nextScanTime = Time.time + 1f;

        if (targets.Count == 0)
        {
            foreach (ObserverBehaviour observer in FindObjectsByType<ObserverBehaviour>(FindObjectsSortMode.None))
            {
                if (observer is ImageTargetBehaviour) targets.Add(observer);
            }
        }

        foreach (ObserverBehaviour target in targets)
        {
            if (target == null || cactusByTarget.ContainsKey(target)) continue;

            GameObject cactus = null;
            foreach (Animator animator in target.GetComponentsInChildren<Animator>(true))
            {
                cactus = animator.gameObject;
                break;
            }
            if (cactus == null) continue;

            // Normalize cactus to ~15 cm tall measured from real render bounds.
            // Its authored size is unknown (huge CATRig export), so a fixed
            // multiplier kept failing - measure and scale instead.
            var rends = cactus.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            Bounds b = default;
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (any && b.size.y > 0.0001f)
            {
                float s = 0.15f / b.size.y;
                cactus.transform.localScale = cactus.transform.localScale * s;
                Debug.Log("[ARDebug] cactus normalized: nativeH=" + b.size.y.ToString("0.00") + "m scale x" + s.ToString("0.0000"));
            }

            cactusByTarget[target] = cactus;
            CacheRenderers(cactus);

            GameObject chanPrefab = Resources.Load<GameObject>("UnityChan/UnityChanAR");
            if (chanPrefab == null) continue;

            GameObject chan = Instantiate(chanPrefab, target.transform, false);
            chan.name = "UnityChan";
            chan.transform.localPosition = Vector3.zero;
            chan.transform.localRotation = Quaternion.Euler(0f, ModelYawOffset, 0f);
            chan.SetActive(false);
            RemapMaterials(chan);
            chanByTarget[target] = chan;
            CacheRenderers(chan);

            // Fresh spawns may also have been toggled by Vuforia before we got here.
            SetRenderersEnabled(chan, true);
        }
    }

    private void ApplyMode()
    {
        int i = 0;
        foreach (var kvp in cactusByTarget)
        {
            bool chanActive = mode == 1 || (mode == 2 && i == 0);

            GameObject cactus = kvp.Value;
            if (cactus != null)
            {
                cactus.SetActive(!chanActive);
                // Critical: Vuforia's event handler disables child renderers on
                // target-lost; GameObject.SetActive does NOT restore them.
                if (!chanActive) SetRenderersEnabled(cactus, true);
            }

            if (chanByTarget.TryGetValue(kvp.Key, out GameObject chan) && chan != null)
            {
                chan.SetActive(chanActive);
                if (chanActive) SetRenderersEnabled(chan, true);
            }
            i++;
        }
    }

    private void CacheRenderers(GameObject root)
    {
        if (root == null || rendererCache.ContainsKey(root)) return;
        rendererCache[root] = root.GetComponentsInChildren<Renderer>(true);
    }

    private void SetRenderersEnabled(GameObject root, bool on)
    {
        if (root == null) return;
        if (!rendererCache.TryGetValue(root, out Renderer[] renderers))
        {
            CacheRenderers(root);
            renderers = rendererCache[root];
        }
        if (renderers == null) return;
        foreach (Renderer r in renderers)
        {
            if (r != null && r.enabled != on) r.enabled = on;
        }
    }

    // Drives whichever model is visible with the same attack logic the cacti use.
    private void MirrorCombatState()
    {
        if (bonus == null) return;
        bool attacking = bonus.IsAttacking;

        int i = 0;
        foreach (var kvp in cactusByTarget)
        {
            if (kvp.Key == null) { i++; continue; }
            bool chanActive = mode == 1 || (mode == 2 && i == 0);

            GameObject model = chanActive
                ? (chanByTarget.TryGetValue(kvp.Key, out GameObject chan) ? chan : null)
                : kvp.Value;
            if (model == null || !model.activeInHierarchy) { i++; continue; }

            Animator animator = model.GetComponentInChildren<Animator>(true);
            if (animator != null)
                animator.SetBool("isAttacking", attacking);

            if (attacking)
            {
                ObserverBehaviour other = null;
                int j = 0;
                foreach (var otherKvp in cactusByTarget)
                {
                    if (j != i) { other = otherKvp.Key; break; }
                    j++;
                }
                if (other != null)
                {
                    Vector3 dir = other.transform.position - kvp.Key.transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.000001f)
                    {
                        Quaternion look = Quaternion.LookRotation(dir);
                        model.transform.rotation = Quaternion.Slerp(
                            model.transform.rotation, look, Time.deltaTime * 5f);
                    }
                }
            }
            i++;
        }
    }

    // 1 Hz debug line to logcat: target statuses, positions, distance, renderer health.
    private void DebugTick()
    {
        if (Time.time < nextLogTime) return;
        nextLogTime = Time.time + 1f;

        var sb = new System.Text.StringBuilder("[ARDebug] mode=").Append(mode);
        int tracked = 0;
        Vector3 posA = Vector3.zero, posB = Vector3.zero;
        string nameA = "?", nameB = "?";

        foreach (var kvp in cactusByTarget)
        {
            ObserverBehaviour t = kvp.Key;
            if (t == null) continue;
            Status st = t.TargetStatus.Status;
            bool isTracked = st == Status.TRACKED || st == Status.EXTENDED_TRACKED;
            if (isTracked)
            {
                if (tracked == 0) { posA = t.transform.position; nameA = t.gameObject.name; }
                else { posB = t.transform.position; nameB = t.gameObject.name; }
                tracked++;
            }
            sb.Append(' ').Append(t.gameObject.name).Append('=').Append(st);

            GameObject cactus = kvp.Value;
            if (cactus != null && rendererCache.TryGetValue(cactus, out Renderer[] rr))
            {
                int on = 0;
                foreach (Renderer r in rr) if (r != null && r.enabled) on++;
                sb.Append(" cactus[go=").Append(cactus.activeSelf)
                  .Append(" rend=").Append(on).Append('/').Append(rr.Length).Append(']');
            }
            else
            {
                sb.Append(" cactus[none]");
            }
        }

        if (tracked >= 2)
        {
            float dist = Vector3.Distance(posA, posB);
            sb.Append(" dist=").Append(dist.ToString("0.000")).Append("m")
              .Append(" fight=").Append(dist <= 0.25f ? "YES" : "no");
        }
        else
        {
            sb.Append(" dist=-- (").Append(tracked).Append("/2 markers tracked)");
        }
        Debug.Log(sb.ToString());
    }

    // Fallback only: skips materials already on URP (baked at edit time).
    private static void RemapMaterials(GameObject root)
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            bool changed = false;
            var mats = renderer.sharedMaterials;
            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null) continue;
                if (mat.shader != null && mat.shader.name.Contains("Universal Render Pipeline"))
                    continue;

                string slot = mat.name.Replace(" (Instance)", "").Trim().ToLowerInvariant();
                Texture2D tex = null;

                foreach (var pair in TextureBySlot)
                {
                    if (!slot.Contains(pair.Key)) continue;
                    if (string.IsNullOrEmpty(pair.Value)) break;
                    tex = Resources.Load<Texture2D>("UnityChan/" + pair.Value);
                    break;
                }

                var urp = new Material(urpLit);
                if (tex != null) urp.mainTexture = tex;
                urp.color = tex != null ? Color.white : new Color(0.85f, 0.85f, 0.85f, 1f);
                urp.SetFloat("_Smoothness", 0f);
                urp.name = mat.name + "_URP";
                mats[m] = urp;
                changed = true;
            }
            if (changed) renderer.sharedMaterials = mats;
        }
    }
}
