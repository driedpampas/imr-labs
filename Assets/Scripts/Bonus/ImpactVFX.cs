using UnityEngine;

// Self-contained impact puff: builds and configures its own ParticleSystem in
// Awake, sizes it for the AR scene (meters), plays once, then destroys itself.
public class ImpactVFX : MonoBehaviour
{
    [Header("Tuning (meters)")]
    [SerializeField] private float duration = 0.9f;
    [SerializeField] private float particleSize = 0.07f;

    private ParticleSystem particles;

    private void Awake()
    {
        particles = GetComponent<ParticleSystem>();
        Configure(particles);

        var renderer = GetComponent<ParticleSystemRenderer>();
        Material mat = Resources.Load<Material>("VFX/PuffMaterial");
        if (mat != null) renderer.material = mat;

        particles.Play();
    }

    private static void Configure(ParticleSystem ps)
    {
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.9f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
        main.startSpeed = 1.1f; // matches particleSize scale; exposed via header if needed
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
        main.startColor = new Color(1f, 0.6f, 0.15f, 1f);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;

        var emission = ps.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.03f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.separateAxes = false;
        var curve = new ParticleSystem.MinMaxCurve
        {
            mode = ParticleSystemCurveMode.Curve,
            curveMultiplier = 1f,
            curve = AnimationCurve.Linear(0f, 0.2f, 1f, 1f)
        };
        size.size = curve;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.35f), new GradientAlphaKey(0f, 1f) });
        color.color = grad;
    }

    private void Update()
    {
        if (particles != null && !particles.IsAlive())
        {
            Destroy(gameObject);
        }
    }
}
