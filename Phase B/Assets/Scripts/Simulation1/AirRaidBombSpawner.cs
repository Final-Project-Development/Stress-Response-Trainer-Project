using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Atmospheric air-raid effect for Simulation 1: rockets fall from the sky and explode on open ground
/// around the player (never on them). Self-bootstrapping, no scene wiring or imported assets required.
/// All visuals are built at runtime; the boom sound is loaded from Resources/Audio/missile-boom.
///
/// Intensity escalates with the mission sub-phase and peaks during the run to the mamad shelter.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class AirRaidBombSpawner : MonoBehaviour
{
    private static AirRaidBombSpawner instance;

    // Geometry / pacing
    private const float DropHeight = 70f;
    private const float FallSpeed = 40f;
    private const float MinImpactDistanceFromPlayer = 7f;
    private const float GroundRayHeight = 80f;
    private const float FirstBombGrace = 2.5f;

    private TrainingFlowController flow;
    private GameManager gameManager;
    private AudioClip boomClip;

    private bool active;
    private float nextSpawnTime;

    private static Texture2D softCircle;
    private static Material fireMaterial;
    private static Material smokeMaterial;
    private static Material sparkMaterial;
    private static Material trailMaterial;
    private static Material bombMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        var go = new GameObject("Air Raid Bomb Spawner");
        instance = go.AddComponent<AirRaidBombSpawner>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        boomClip = Resources.Load<AudioClip>("Audio/missile-boom");
    }

    private void Update()
    {
        if (!ResolveFlow())
        {
            active = false;
            return;
        }

        bool activePhase = flow.CurrentPhase == TrainingFlowController.Phase.Simulation1Active
                           || flow.CurrentPhase == TrainingFlowController.Phase.Simulation2Active;
        bool shouldRun = activePhase && flow.AllowsMissionGameplay && !flow.IsPaused;

        if (!shouldRun)
        {
            active = false;
            return;
        }

        if (!active)
        {
            active = true;
            nextSpawnTime = Time.time + FirstBombGrace;
        }

        if (Time.time >= nextSpawnTime)
        {
            SpawnBomb();
            nextSpawnTime = Time.time + GetSpawnInterval();
        }
    }

    private bool ResolveFlow()
    {
        if (flow == null)
            flow = TrainingFlowController.Instance;
        if (flow == null)
            flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
        return flow != null;
    }

    private GameManager.Sim1MissionPhase CurrentSubPhase()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

        return gameManager != null
            ? gameManager.GetSim1Phase()
            : GameManager.Sim1MissionPhase.CollectItems;
    }

    private bool IsSimulation2()
    {
        return flow != null && flow.CurrentPhase == TrainingFlowController.Phase.Simulation2Active;
    }

    private float GetSpawnInterval()
    {
        if (IsSimulation2())
            return Sim2Intense() ? Random.Range(3.0f, 4.5f) : Random.Range(5f, 8f);

        switch (CurrentSubPhase())
        {
            case GameManager.Sim1MissionPhase.RunToShelter: return Random.Range(2.0f, 3.5f);
            case GameManager.Sim1MissionPhase.CloseDoor: return Random.Range(4f, 6f);
            case GameManager.Sim1MissionPhase.TurnOffLights: return Random.Range(5f, 8f);
            default: return Random.Range(6f, 10f);
        }
    }

    private float GetImpactRadius()
    {
        if (IsSimulation2())
            return Sim2Intense() ? Random.Range(10f, 18f) : Random.Range(16f, 26f);

        return CurrentSubPhase() == GameManager.Sim1MissionPhase.RunToShelter
            ? Random.Range(8f, 16f)
            : Random.Range(14f, 26f);
    }

    /// <summary>Sim 2 ramps up once dispatch has been called (player is treating / moving in the open).</summary>
    private bool Sim2Intense()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

        return gameManager != null && gameManager.HasReportedEmergency();
    }

    private void SpawnBomb()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 playerPos = cam.transform.position;
        Vector3 impact = PickImpactPoint(playerPos);
        StartCoroutine(BombRoutine(impact, playerPos));
    }

    private Vector3 PickImpactPoint(Vector3 playerPos)
    {
        float radius = GetImpactRadius();
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

        if (offset.magnitude < MinImpactDistanceFromPlayer)
            offset = offset.normalized * MinImpactDistanceFromPlayer;

        Vector3 xz = playerPos + offset;

        float groundY = playerPos.y;
        Vector3 rayStart = new Vector3(xz.x, playerPos.y + GroundRayHeight, xz.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, GroundRayHeight * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            groundY = hit.point.y;

        return new Vector3(xz.x, groundY, xz.z);
    }

    private IEnumerator BombRoutine(Vector3 impact, Vector3 playerPos)
    {
        GameObject bomb = CreateBombVisual();
        bomb.transform.position = impact + Vector3.up * DropHeight;
        // A capsule's length runs along local Y, so keep it upright to read as a falling projectile.
        bomb.transform.rotation = Quaternion.identity;

        while (bomb != null)
        {
            bool stillActive = flow != null
                               && (flow.CurrentPhase == TrainingFlowController.Phase.Simulation1Active
                                   || flow.CurrentPhase == TrainingFlowController.Phase.Simulation2Active);
            if (!stillActive)
            {
                Destroy(bomb);
                yield break;
            }

            if (flow.IsPaused)
            {
                yield return null;
                continue;
            }

            float step = FallSpeed * Time.deltaTime;
            bomb.transform.position = Vector3.MoveTowards(bomb.transform.position, impact, step);

            if ((bomb.transform.position - impact).sqrMagnitude < 0.05f)
                break;

            yield return null;
        }

        if (bomb != null)
            Destroy(bomb);

        Explode(impact, playerPos);
    }

    private void Explode(Vector3 impact, Vector3 playerPos)
    {
        PlayBoom(impact, playerPos);

        var fx = new GameObject("Air Raid Explosion");
        fx.transform.position = impact + Vector3.up * 0.2f;

        BuildFireBurst(fx.transform);
        BuildSmoke(fx.transform);
        BuildSparks(fx.transform);

        var lightGo = new GameObject("Flash");
        lightGo.transform.SetParent(fx.transform, false);
        var flash = lightGo.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = new Color(1f, 0.62f, 0.28f);
        flash.range = 28f;
        flash.intensity = 9f;
        StartCoroutine(FadeLight(flash));

        Destroy(fx, 3.5f);

        TriggerHaptics(impact, playerPos);
    }

    // Number of overlapping voices stacked per blast. More layers = louder, fuller boom.
    private const int BoomLayers = 4;

    private void PlayBoom(Vector3 impact, Vector3 playerPos)
    {
        if (boomClip == null)
            return;

        float dist = Vector3.Distance(impact, playerPos);
        float volume = Mathf.Lerp(1f, 0.7f, Mathf.Clamp01(dist / 60f));

        var go = new GameObject("Air Raid Boom");
        go.transform.position = impact;

        for (int i = 0; i < BoomLayers; i++)
        {
            var src = go.AddComponent<AudioSource>();
            src.clip = boomClip;
            src.volume = volume;
            src.spatialBlend = 0f;
            src.dopplerLevel = 0f;
            src.pitch = Random.Range(0.9f, 1.02f) - i * 0.015f;
            src.Play();
        }

        Destroy(go, boomClip.length + 0.5f);
    }

    private static IEnumerator FadeLight(Light flash)
    {
        const float duration = 0.5f;
        float start = flash != null ? flash.intensity : 0f;
        float t = 0f;
        while (flash != null && t < duration)
        {
            t += Time.deltaTime;
            flash.intensity = Mathf.Lerp(start, 0f, t / duration);
            yield return null;
        }

        if (flash != null)
            flash.intensity = 0f;
    }

    private static void TriggerHaptics(Vector3 impact, Vector3 playerPos)
    {
        float dist = Vector3.Distance(impact, playerPos);
        float amplitude = Mathf.Clamp01(1f - dist / 30f);
        if (amplitude <= 0.03f)
            return;

        SendHaptic(XRNode.LeftHand, amplitude);
        SendHaptic(XRNode.RightHand, amplitude);
    }

    private static readonly List<InputDevice> hapticDevices = new List<InputDevice>(2);

    private static void SendHaptic(XRNode node, float amplitude)
    {
        hapticDevices.Clear();
        InputDevices.GetDevicesAtXRNode(node, hapticDevices);
        for (int i = 0; i < hapticDevices.Count; i++)
        {
            InputDevice device = hapticDevices[i];
            if (device.isValid &&
                device.TryGetHapticCapabilities(out HapticCapabilities caps) &&
                caps.supportsImpulse)
            {
                device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude * 0.85f), 0.25f);
            }
        }
    }

    private GameObject CreateBombVisual()
    {
        var bomb = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bomb.name = "Air Raid Bomb";

        var collider = bomb.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        bomb.transform.localScale = new Vector3(0.28f, 0.6f, 0.28f);

        var renderer = bomb.GetComponent<Renderer>();
        renderer.sharedMaterial = GetBombMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var trail = bomb.AddComponent<TrailRenderer>();
        trail.time = 0.75f;
        trail.startWidth = 0.4f;
        trail.endWidth = 0.02f;
        trail.material = GetTrailMaterial();
        trail.startColor = new Color(0.7f, 0.7f, 0.7f, 0.85f);
        trail.endColor = new Color(0.45f, 0.45f, 0.45f, 0f);
        trail.numCapVertices = 4;

        return bomb;
    }

    private static ParticleSystem CreateSystem(Transform parent, string name, Material material, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = material;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return ps;
    }

    private static void SetBurst(ParticleSystem ps, int count)
    {
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    private void BuildFireBurst(Transform parent)
    {
        var ps = CreateSystem(parent, "Fire", GetFireMaterial(), 3);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.7f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 3.0f);
        main.gravityModifier = 0f;
        main.maxParticles = 120;

        SetBurst(ps, 34);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f),
                new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.45f),
                new GradientColorKey(new Color(0.55f, 0.1f, 0.05f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.9f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.25f));

        ps.Play();
    }

    private void BuildSmoke(Transform parent)
    {
        var ps = CreateSystem(parent, "Smoke", GetSmokeMaterial(), 1);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(2.2f, 4.2f);
        main.gravityModifier = -0.05f;
        main.maxParticles = 80;

        SetBurst(ps, 22);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.25f, 0.22f, 0.2f), 0f),
                new GradientColorKey(new Color(0.4f, 0.4f, 0.4f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.55f, 0.2f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.4f));

        ps.Play();
    }

    private void BuildSparks(Transform parent)
    {
        var ps = CreateSystem(parent, "Sparks", GetSparkMaterial(), 4);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 13f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.gravityModifier = 0.8f;
        main.maxParticles = 60;

        SetBurst(ps, 28);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f),
                new GradientColorKey(new Color(1f, 0.5f, 0.1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ps.Play();
    }

    private static Material GetBombMaterial()
    {
        if (bombMaterial != null)
            return bombMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        bombMaterial = new Material(shader);
        var color = new Color(0.12f, 0.12f, 0.13f, 1f);
        if (bombMaterial.HasProperty("_BaseColor"))
            bombMaterial.SetColor("_BaseColor", color);
        if (bombMaterial.HasProperty("_Color"))
            bombMaterial.SetColor("_Color", color);
        bombMaterial.color = color;
        return bombMaterial;
    }

    private static Material GetTrailMaterial()
    {
        if (trailMaterial != null)
            return trailMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        trailMaterial = new Material(shader);
        return trailMaterial;
    }

    private static Material GetParticleMaterial(ref Material cache, Color tint, bool additive)
    {
        if (cache != null)
            return cache;

        Shader shader = Shader.Find("Sprites/Default");
        cache = new Material(shader);
        cache.mainTexture = GetSoftCircle();
        cache.color = tint;
        if (additive && cache.HasProperty("_SrcBlend"))
        {
            cache.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            cache.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        }

        return cache;
    }

    private static Material GetFireMaterial() => GetParticleMaterial(ref fireMaterial, Color.white, true);
    private static Material GetSparkMaterial() => GetParticleMaterial(ref sparkMaterial, Color.white, true);
    private static Material GetSmokeMaterial() => GetParticleMaterial(ref smokeMaterial, Color.white, false);

    private static Texture2D GetSoftCircle()
    {
        if (softCircle != null)
            return softCircle;

        const int size = 64;
        softCircle = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float center = (size - 1) * 0.5f;
        float maxDist = center;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / maxDist;
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = alpha * alpha;
                softCircle.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        softCircle.Apply();
        return softCircle;
    }
}
