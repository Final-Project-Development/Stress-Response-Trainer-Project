using TMPro;
using UnityEngine;

public class BioMetricsUI : MonoBehaviour
{
    public BioMetricsEstimator bio;
    public TextMeshProUGUI text;

    void Update()
    {
        if (bio == null || text == null) return;

        text.text =
            $"HR: {bio.heartRateFiltered:F0} bpm (raw {bio.heartRateRaw})\n" +
            $"Rest: {bio.restingHR:F0} | Max: {bio.maxHR}\n" +
            $"Zone: {bio.zoneName}\n" +
            $"Exertion: {bio.exertionScore:F0}/100 (intensity {bio.intensity * 100f:F0}%)\n" +
            $"Stress (proxy): {bio.stressScore:F0}/100 (activity {bio.activityLevel:F2})\n" +
            $"Calories: {bio.totalCalories:F1} kcal ({bio.kcalPerMin:F1} kcal/min)";
    }
}