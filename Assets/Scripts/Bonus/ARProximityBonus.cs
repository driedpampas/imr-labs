using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Vuforia;

// Bonus layer for Lab 1: on-screen distance readout (IMGUI, build-safe),
// attack whoosh SFX and impact puff VFX when the cacti enter attack mode.
//
// Drop-in design: NO scene edits, NO prefabs, NO font assets.
// Everything self-wires at runtime; assets load from Assets/Resources/.
public class ARProximityBonus : MonoBehaviour
{
    [Header("Behavior")]
    [Tooltip("Fallback threshold if the trigger's own value cannot be read")]
    [SerializeField] private float attackDistance = 0.25f;
    [Tooltip("Attack turns off only past attackDistance + hysteresis (prevents flicker)")]
    [SerializeField] private float hysteresis = 0.03f;
    [SerializeField] private float vfxRetriggerInterval = 0.85f;

    private readonly List<ObserverBehaviour> cachedObservers = new List<ObserverBehaviour>();
    private readonly List<ObserverBehaviour> trackedTargets = new List<ObserverBehaviour>();
    private readonly Dictionary<Transform, Coroutine> punches = new Dictionary<Transform, Coroutine>();

    private ARProximityTrigger proximityTrigger;
    private AudioSource attackAudio;
    private bool attacking;
    private bool spawnImmediatePuff;
    private float lastVfxTime = -999f;
    private float nextScanTime;
    private string hudText = "Starting AR tracking...";
    private string distanceSource = "targets";

    // Current combat state, for other scripts (e.g. CharacterSwapDemo).
    public bool IsAttacking => attacking;

    // Optional override: CharacterSwapDemo supplies RENDERED-CHARACTER positions
    // (marker transforms can be unreliable, e.g. one sitting at world origin).
    public static System.Func<Vector3[]> ModelPositionProvider;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<ARProximityBonus>() == null)
        {
            var go = new GameObject("ARProximityBonus");
            DontDestroyOnLoad(go);
            go.AddComponent<ARProximityBonus>();
        }
    }

    private void Awake()
    {
        proximityTrigger = FindFirstObjectByType<ARProximityTrigger>();
        attackDistance = ResolveAttackDistance();
    }

    private void Start()
    {
        attackAudio = FindFirstObjectByType<AudioSource>();
        if (attackAudio == null)
        {
            var go = new GameObject("AttackSFX");
            go.transform.SetParent(transform, false);
            attackAudio = go.AddComponent<AudioSource>();
        }

        attackAudio.playOnAwake = false;
        attackAudio.loop = false;
        if (attackAudio.clip == null)
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/attack_whoosh");
            if (clip != null) attackAudio.clip = clip;
        }
    }

    private void Update()
    {
        CollectTrackedTargets();

        // Late-bind the trigger: it lives in the scene, this object bootstrapped
        // before scene load. Without this, attackDistance stays the fallback.
        if (proximityTrigger == null)
        {
            proximityTrigger = FindFirstObjectByType<ARProximityTrigger>();
            if (proximityTrigger != null) attackDistance = ResolveAttackDistance();
        }

        if (trackedTargets.Count < 2)
        {
            SetAttacking(false);
            hudText = "Markers tracked: " + trackedTargets.Count + "/2 - show both prints";
            return;
        }

        // Prefer real character positions; fall back to target transforms.
        Vector3[] modelPositions = ModelPositionProvider != null ? ModelPositionProvider() : null;
        Vector3 posA, posB;
        if (modelPositions != null)
        {
            posA = modelPositions[0];
            posB = modelPositions[1];
            distanceSource = "chars";
        }
        else
        {
            posA = trackedTargets[0].transform.position;
            posB = trackedTargets[1].transform.position;
            distanceSource = "targets";
        }
        float distance = Vector3.Distance(posA, posB);

        hudText = string.Format("Markers: {0}/2  [{1}]  Dist: {2:0.00} m\nFight at {3:0.00} m  -  {4}",
            trackedTargets.Count, distanceSource, distance, attackDistance, attacking ? "ATTACK!" : "idle");

        // Hysteresis: attack at <= attackDistance, calm only past attackDistance + hysteresis.
        if (!attacking && distance <= attackDistance)
            SetAttacking(true);
        else if (attacking && distance > attackDistance + hysteresis)
            SetAttacking(false);

        bool retrigger = Time.time - lastVfxTime >= vfxRetriggerInterval;
        if (attacking && (spawnImmediatePuff || retrigger))
        {
            spawnImmediatePuff = false;
            lastVfxTime = Time.time;
            SpawnImpact((posA + posB) * 0.5f);

            if (attackAudio != null && attackAudio.clip != null)
            {
                attackAudio.pitch = Random.Range(0.95f, 1.1f);
                attackAudio.volume = 0.45f; // quieter echo swings while locked in combat
                attackAudio.Play();
            }
        }
    }

    private void CollectTrackedTargets()
    {
        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + 0.5f;
            cachedObservers.Clear();
            cachedObservers.AddRange(FindObjectsByType<ObserverBehaviour>(FindObjectsSortMode.None));
        }

        trackedTargets.Clear();
        foreach (var observer in cachedObservers)
        {
            if (observer == null) continue;
            Status status = observer.TargetStatus.Status;
            if (status == Status.TRACKED || status == Status.EXTENDED_TRACKED)
                trackedTargets.Add(observer);
        }
    }

    private void SetAttacking(bool value)
    {
        if (attacking == value) return;
        attacking = value;

        if (value)
        {
            spawnImmediatePuff = true;
            if (attackAudio != null && attackAudio.clip != null)
            {
                attackAudio.pitch = Random.Range(0.95f, 1.1f);
                attackAudio.volume = 1f; // full-volume whoosh on the initial clash
                attackAudio.Play();
            }
            PunchCacti();
        }
    }

    private void PunchCacti()
    {
        foreach (var observer in cachedObservers)
        {
            if (observer == null) continue;
            Animator animator = observer.GetComponentInChildren<Animator>();
            if (animator == null) continue;

            Transform root = animator.transform;
            if (punches.TryGetValue(root, out Coroutine running) && running != null)
                StopCoroutine(running);
            punches[root] = StartCoroutine(PunchScale(root));
        }
    }

    private IEnumerator PunchScale(Transform root)
    {
        Vector3 baseScale = root.localScale;
        const float duration = 0.28f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
            root.localScale = baseScale * (1f + 0.12f * k);
            yield return null;
        }
        root.localScale = baseScale;
    }

    private void SpawnImpact(Vector3 position)
    {
        var go = new GameObject("ImpactPuff", typeof(ParticleSystem), typeof(ImpactVFX));
        go.transform.position = position;
    }

    // Reads the attackDistance tuned on the original trigger so both scripts stay in sync.
    private float ResolveAttackDistance()
    {
        if (proximityTrigger != null)
        {
            FieldInfo field = typeof(ARProximityTrigger).GetField("attackDistance",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                object value = field.GetValue(proximityTrigger);
                if (value is float floatValue) return floatValue;
            }
        }
        return attackDistance;
    }

    // IMGUI debug panel: markers tracked, live distance, state + threshold.
    private void OnGUI()
    {
        float scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / 420f);
        var boxStyle = new GUIStyle(GUI.skin.box);
        var labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = Mathf.RoundToInt(20 * scale),
            fontStyle = FontStyle.Bold,
        };
        labelStyle.normal.textColor = Color.white;

        float w = 560 * scale;
        float h = 118 * scale;
        var rect = new Rect((Screen.width - w) * 0.5f, 10 * scale, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = attacking ? Color.yellow : Color.white;
        GUI.Label(new Rect(rect.x + 16 * scale, rect.y + 10 * scale, w - 32 * scale, h - 20 * scale),
            hudText, labelStyle);
        GUI.color = Color.white;
    }
}
