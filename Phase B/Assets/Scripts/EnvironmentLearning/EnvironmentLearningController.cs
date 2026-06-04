using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Free-roam city learning: shows world labels, hides mission gameplay.
/// </summary>
public class EnvironmentLearningController : MonoBehaviour
{
    public static EnvironmentLearningController Instance { get; private set; }

    [Header("HUD (screen-space)")]
    public GameObject learningHudRoot;
    public TextMeshProUGUI learningHudBodyText;

    [TextArea]
    public string learningHudDefaultText =
        "סיור היכרות עם העיר\n\n" +
        "הסתובבי והסתכלי על השמות מעל הפריטים החשובים.\n" +
        "בשלב זה אין משימה, אין איסוף פריטים ואין סירנה.\n\n" +
        "כשסיימת — לחצי Back או Esc כדי לחזור לבחירת הסימולציות.";

    [Header("Labels")]
    [Tooltip("Background for world item name panels (inventory-highlight-large 1_0).")]
    public Sprite worldLabelPanelSprite;

    public Color worldLabelPanelColor = Color.white;

    [Tooltip("Optional parent of all WorldItemLabel objects.")]
    public Transform labelsRoot;

    readonly List<WorldItemLabel> _labels = new List<WorldItemLabel>();
    bool _active;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Register(WorldItemLabel label)
    {
        if (label == null || _labels.Contains(label))
            return;
        _labels.Add(label);
        label.SetVisible(_active);
    }

    public void Unregister(WorldItemLabel label)
    {
        if (label != null)
            _labels.Remove(label);
    }

    public void BeginLearning()
    {
        _active = true;
        if (learningHudBodyText != null)
            learningHudBodyText.text = learningHudDefaultText;
        if (learningHudRoot != null)
            learningHudRoot.SetActive(true);
        SetAllLabelsVisible(true);
    }

    public void EndLearning()
    {
        _active = false;
        if (learningHudRoot != null)
            learningHudRoot.SetActive(false);
        SetAllLabelsVisible(false);
    }

    void SetAllLabelsVisible(bool on)
    {
        if (labelsRoot != null)
        {
            var labels = labelsRoot.GetComponentsInChildren<WorldItemLabel>(true);
            for (int i = 0; i < labels.Length; i++)
                labels[i].SetVisible(on);
            return;
        }

        _labels.RemoveAll(l => l == null);
        if (_labels.Count == 0)
        {
            var found = FindObjectsByType<WorldItemLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
                _labels.Add(found[i]);
        }

        for (int i = 0; i < _labels.Count; i++)
            _labels[i].SetVisible(on);
    }
}
