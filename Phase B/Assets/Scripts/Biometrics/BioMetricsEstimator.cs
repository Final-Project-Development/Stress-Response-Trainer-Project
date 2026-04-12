using UnityEngine;

public class BioMetricsEstimator : MonoBehaviour
{
    public enum Sex { Male, Female }

    [Header("Input")]
    public UDPReceiver receiver;

    [Tooltip("0 = not moving (sitting/aiming), 1 = full physical activity.\n" +
             "If you don't have this yet, leave at 0 and stress will behave like 'arousal'.")]
    [Range(0f, 1f)] public float activityLevel = 0f;

    [Header("User Info (needed for calories + HR max estimate)")]
    public Sex sex = Sex.Male;
    public int age = 25;
    public float weightKg = 70f;

    [Header("Resting / Max HR")]
    public bool autoEstimateMaxHR = true;
    public int maxHR = 190;

    public bool autoCalibrateRestingHR = true;
    public float calibrateSeconds = 30f;
    public float restingHR = 70f;

    [Header("Smoothing")]
    [Tooltip("Higher = faster response, lower = smoother.")]
    public float hrSmoothing = 6f;
    public float scoreSmoothing = 4f;

    [Header("Outputs (read-only in play mode)")]
    public int heartRateRaw;
    public float heartRateFiltered;

    [Range(0f, 1f)] public float intensity;         // 0..1
    [Range(0f, 100f)] public float exertionScore;    // 0..100
    [Range(0f, 100f)] public float stressScore;      // 0..100 (proxy)

    public int zone = 0;          // 1..5 (0 = unknown)
    public string zoneName = "-";

    public float kcalPerMin;
    public float totalCalories;

    private float _timeAlive;
    private float _prevHrFiltered;

    void Start()
    {
        _timeAlive = 0f;
        _prevHrFiltered = 0f;

        // If auto-calibrating, start high so we can "min()" down.
        if (autoCalibrateRestingHR)
            restingHR = 999f;
    }

    void Update()
    {
        if (receiver == null) return;

        heartRateRaw = receiver.heartRate;
        if (heartRateRaw <= 0) return;

        float dt = Time.deltaTime;
        _timeAlive += dt;

        // --- Filter HR (EMA-like) ---
        float hrTarget = heartRateRaw;
        float hrAlpha = 1f - Mathf.Exp(-hrSmoothing * dt);
        heartRateFiltered = Mathf.Lerp(heartRateFiltered, hrTarget, hrAlpha);

        // --- Resting HR calibration (first N seconds) ---
        if (autoCalibrateRestingHR && _timeAlive <= calibrateSeconds)
        {
            restingHR = Mathf.Min(restingHR, heartRateFiltered);
        }
        else if (autoCalibrateRestingHR && restingHR > 300f)
        {
            // In case no samples during calibration
            restingHR = heartRateFiltered;
        }

        // --- Estimate max HR if wanted ---
        // Tanaka et al: HRmax ≈ 208 - 0.7 * age :contentReference[oaicite:2]{index=2}
        if (autoEstimateMaxHR)
            maxHR = Mathf.Clamp(Mathf.RoundToInt(208f - 0.7f * age), 120, 230);

        float hrr = Mathf.Max(1f, maxHR - restingHR);

        // --- Exertion / Intensity (Karvonen HR reserve method) ---
        // intensity = (HR - HRrest) / (HRmax - HRrest) :contentReference[oaicite:3]{index=3}
        intensity = Mathf.Clamp01((heartRateFiltered - restingHR) / hrr);

        // Map intensity to a nicer 0..100 curve (gamma makes high effort feel more “expensive”)
        float exertionGamma = 1.35f;
        float exertionTarget = Mathf.Pow(intensity, exertionGamma) * 100f;
        float scoreAlpha = 1f - Mathf.Exp(-scoreSmoothing * dt);
        exertionScore = Mathf.Lerp(exertionScore, exertionTarget, scoreAlpha);

        // --- HR Zones (simple % of max HR) ---
        float pctMax = (maxHR > 0) ? heartRateFiltered / maxHR : 0f;
        zone = ComputeZone(pctMax, out zoneName);

        // --- Stress (PROXY) ---
        // Real stress should use HRV (RMSSD etc.) not just BPM :contentReference[oaicite:4]{index=4}
        // Prototype proxy:
        //   - "Arousal" = elevated HR above rest
        //   - Reduce stress when activityLevel is high (because high HR could be exercise)
        //   - Add a little from sudden HR changes (spikes)
        float dHr = (dt > 0f) ? (heartRateFiltered - _prevHrFiltered) / dt : 0f;
        _prevHrFiltered = heartRateFiltered;

        float arousal = Mathf.Clamp01((heartRateFiltered - restingHR) / (0.35f * hrr)); // more sensitive
        float spike = Mathf.Clamp01(Mathf.Abs(dHr) / 10f) * 0.35f;

        float stressProxy01 = Mathf.Clamp01(arousal * (1f - activityLevel) + spike);
        float stressTarget = stressProxy01 * 100f;
        stressScore = Mathf.Lerp(stressScore, stressTarget, scoreAlpha);

        // --- Calories (Keytel et al. HR-based energy expenditure equation) ---
        // EE (kJ/min) depends on sex, HR, weight, age :contentReference[oaicite:5]{index=5}
        float eeKjPerMin = ComputeEnergyExpenditureKjPerMin(sex, heartRateFiltered, weightKg, age);
        kcalPerMin = Mathf.Max(0f, eeKjPerMin / 4.184f); // convert kJ -> kcal :contentReference[oaicite:6]{index=6}

        // Integrate kcal/min over time
        totalCalories += kcalPerMin * (dt / 60f);
    }

    private static int ComputeZone(float pctMax, out string name)
    {
        // Common 5-zone split by %HRmax:
        // Z1 50–60%, Z2 60–70%, Z3 70–80%, Z4 80–90%, Z5 90–100%
        // (If below 50%, treat as 0 / very easy)
        if (pctMax < 0.50f) { name = "Very Easy"; return 0; }
        if (pctMax < 0.60f) { name = "Zone 1"; return 1; }
        if (pctMax < 0.70f) { name = "Zone 2"; return 2; }
        if (pctMax < 0.80f) { name = "Zone 3"; return 3; }
        if (pctMax < 0.90f) { name = "Zone 4"; return 4; }
        name = "Zone 5"; return 5;
    }

    private static float ComputeEnergyExpenditureKjPerMin(Sex sex, float hr, float weightKg, int age)
    {
        // Keytel et al. (model without VO2max):
        // Male:   EE = -55.0969 + 0.6309*HR + 0.1988*W + 0.2017*Age
        // Female: EE = -20.4022 + 0.4472*HR - 0.1263*W + 0.074*Age
        // Where EE is in kJ/min :contentReference[oaicite:7]{index=7}
        if (sex == Sex.Male)
            return -55.0969f + 0.6309f * hr + 0.1988f * weightKg + 0.2017f * age;
        else
            return -20.4022f + 0.4472f * hr - 0.1263f * weightKg + 0.0740f * age;
    }
}