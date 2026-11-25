using UnityEngine;

[DisallowMultipleComponent]
public sealed class LaserDiscoEnvironment : MonoBehaviour
{
    [Header("Disco Ball")]
    [SerializeField] private Transform ball;
    [SerializeField, Min(.1f)] private float ballDiameter = 2.6f;
    [SerializeField, Min(0f)] private float ballHeight = 7f;

    [Header("Cookie Lights")]
    [SerializeField] private Light[] cookieLights;
    [SerializeField] private Texture2D cookie;
    [SerializeField, Min(.1f)] private float beamIntensity = 8f;
    [SerializeField, Min(1f)] private float beamRange = 48f;
    [SerializeField, Range(15f, 100f)] private float beamAngle = 78f;
    [SerializeField, Min(0f)] private float sweepSpeed = 6.5f;
    [SerializeField, Min(2f)] private float patternDuration = 7f;
    [SerializeField, Min(.1f)] private float lightBeatSpeed = 2.15f;
    [SerializeField, Range(0f, 1f)] private float darkBeatLevel = .12f;

    private float[] phases;
    private Vector3 ballBasePosition;
    private Quaternion ballBaseRotation;
    private Bounds arena;

    private void Start()
    {
        arena = CalculateArena();
        cookieLights ??= System.Array.Empty<Light>();
        ballBasePosition = ball != null
            ? ball.position
            : new Vector3(arena.center.x, arena.max.y + ballHeight, arena.center.z);
        if (ball != null) ballBaseRotation = ball.localRotation;
        phases = new float[cookieLights.Length];
        for (int i = 0; i < cookieLights.Length; i++)
        {
            phases[i] = i * (360f / Mathf.Max(1, cookieLights.Length));
            Light beam = cookieLights[i];
            if (beam == null) continue;
            beam.type = LightType.Spot;
            beam.intensity = beamIntensity;
            beam.range = Mathf.Max(beamRange, arena.size.magnitude);
            beam.spotAngle = beamAngle;
            beam.innerSpotAngle = beamAngle * .28f;
            if (cookie != null) beam.cookie = cookie;
        }
    }

    private void Update()
    {
        if (ball != null)
        {
            ball.position = ballBasePosition;
            ball.localRotation = ballBaseRotation;
        }

        float cycleTime = Mathf.Repeat(Time.time, patternDuration);
        float normalized = cycleTime / patternDuration;
        int pattern = Mathf.FloorToInt(Time.time / patternDuration) % 4;
        float beat = Mathf.Pow(.5f + Mathf.Sin(Time.time * lightBeatSpeed) * .5f, 2.4f);
        float pulsePower = Mathf.Lerp(darkBeatLevel, 1.3f, beat);
        float blackoutCycle = Mathf.Repeat(Time.time, patternDuration * 2.35f) / patternDuration;
        if (blackoutCycle > 1.72f && blackoutCycle < 1.9f)
            pulsePower *= Mathf.SmoothStep(.02f, .16f, Mathf.Abs(blackoutCycle - 1.81f) / .09f);
        for (int i = 0; i < cookieLights.Length; i++)
        {
            Light beam = cookieLights[i];
            if (beam == null) continue;
            float angle;
            float radius;
            float power;
            switch (pattern)
            {
                case 1: // Four opposing beams that snap into a rotating cross.
                    angle = Mathf.Floor(normalized * 8f) * 45f + i * (360f / Mathf.Max(1, cookieLights.Length));
                    radius = i % 2 == 0 ? .82f : .46f;
                    power = .55f + Mathf.Pow(Mathf.Abs(Mathf.Sin(normalized * Mathf.PI * 8f)), 3f) * .8f;
                    break;
                case 2: // Expanding and contracting spiral.
                    angle = phases[i] + Time.time * sweepSpeed * 2.1f * (i % 2 == 0 ? 1f : -.7f);
                    radius = Mathf.Lerp(.18f, .92f, Mathf.PingPong(normalized * 2f + i * .11f, 1f));
                    power = .72f + Mathf.Sin(normalized * Mathf.PI * 4f + i) * .28f;
                    break;
                case 3: // Bright rhythmic flashes with a short intentional blackout.
                    angle = phases[i] + Mathf.Floor(normalized * 6f) * 60f * (i % 2 == 0 ? 1f : -1f);
                    radius = .7f;
                    bool blackout = normalized > .62f && normalized < .78f;
                    power = blackout ? .015f : .22f + Mathf.Pow(Mathf.Max(0f, Mathf.Sin(normalized * Mathf.PI * 10f)), 2f) * 1.35f;
                    break;
                default: // Smooth sweep.
                    angle = phases[i] + Time.time * sweepSpeed * (i % 2 == 0 ? 1f : -.82f);
                    radius = .82f;
                    power = .72f + Mathf.Sin(Time.time * 1.7f + i) * .24f;
                    break;
            }
            float radians = angle * Mathf.Deg2Rad;
            Vector3 target = arena.center + new Vector3(
                Mathf.Sin(radians) * arena.extents.x * radius,
                0f,
                Mathf.Cos(radians * (pattern == 2 ? 1.32f : 1f)) * arena.extents.z * radius);
            beam.transform.position = ballBasePosition;
            beam.transform.rotation = Quaternion.LookRotation((target - beam.transform.position).normalized, Vector3.up);
            float alternatingBeat = i % 2 == 0 ? pulsePower : Mathf.Lerp(pulsePower, 1f, .22f);
            beam.intensity = beamIntensity * Mathf.Max(.005f, power * alternatingBeat);
            beam.spotAngle = beamAngle * (pattern == 1 ? .68f : pattern == 3 ? .82f : 1f);
        }

    }
    private Bounds CalculateArena()
    {
        TileGrid grid = FindAnyObjectByType<TileGrid>();
        if (grid == null) return new Bounds(Vector3.zero, new Vector3(22f, 2f, 28f));
        Renderer[] renderers = grid.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(grid.transform.position, new Vector3(22f, 2f, 28f));
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

#if UNITY_EDITOR
    public void ConfigureAuthoredContent(Transform authoredBall, Light[] authoredCookieLights, Texture2D authoredCookie)
    {
        ball = authoredBall;
        cookieLights = authoredCookieLights;
        cookie = authoredCookie;
    }
#endif
}
