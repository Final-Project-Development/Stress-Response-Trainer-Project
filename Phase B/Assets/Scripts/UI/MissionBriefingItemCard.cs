using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One briefing grid cell: Container (icon + location) and label text below.
/// Hierarchy: ItemRoot / Container / Icon + LocationHint, ItemRoot / Label (or Text (TMP)).
/// </summary>
[DisallowMultipleComponent]
public class MissionBriefingItemCard : MonoBehaviour
{
    [Header("Icon")]
    [Tooltip("Optional: drag a sprite here. You can also set Source Image on Container/Icon directly.")]
    public Sprite iconOverride;

    [Tooltip("When on, the script never replaces the Icon image. Drag sprites in the Editor yourself.")]
    public bool useManualIcon = true;

    [Header("Optional text overrides (leave empty to use catalog by object name)")]
    public string itemKeyOverride;
    public string displayNameOverride;
    public string locationHintOverride;

    [Header("References (auto-wired)")]
    public RectTransform containerRect;
    public Image iconImage;
    public TextMeshProUGUI locationHintText;
    public TextMeshProUGUI itemLabelText;

    MissionBriefingCatalog.Simulation _simulation = MissionBriefingCatalog.Simulation.Simulation1;

    public void ConfigureSimulation(MissionBriefingCatalog.Simulation simulation) => _simulation = simulation;

#if UNITY_EDITOR
    public void EnsureReferencesForEditor() => EnsureReferences();
#endif

    public void ApplyContent()
    {
        EnsureReferences();
        EnsureLocationHintUi();

        var itemKey = ResolveItemKey();
        if (MissionBriefingCatalog.TryGet(_simulation, itemKey, out var entry))
            ApplyText(itemKey, entry);
        else
            ApplyTextWithoutCatalog();

        if (useManualIcon)
            ApplyManualIconIfNeeded();
        else
            ApplyIcon(itemKey, entry);
    }

    void ApplyText(string itemKey, MissionBriefingCatalog.Entry entry)
    {
        var displayName = !string.IsNullOrWhiteSpace(displayNameOverride)
            ? displayNameOverride.Trim()
            : entry.DisplayName;

        var location = !string.IsNullOrWhiteSpace(locationHintOverride)
            ? locationHintOverride.Trim()
            : entry.LocationHint;

        if (itemLabelText != null)
            itemLabelText.text = displayName;

        if (locationHintText != null)
            locationHintText.text = location;
    }

    void ApplyTextWithoutCatalog()
    {
        if (!string.IsNullOrWhiteSpace(displayNameOverride) && itemLabelText != null)
            itemLabelText.text = displayNameOverride.Trim();

        if (!string.IsNullOrWhiteSpace(locationHintOverride) && locationHintText != null)
            locationHintText.text = locationHintOverride.Trim();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        var panel = GetComponentInParent<SimulationBriefingPanelController>();
        if (panel != null)
            _simulation = panel.simulation;

        if (Application.isPlaying)
            return;

        EnsureReferences();
        CaptureManualIconFromImage();
    }
#endif

    void CaptureManualIconFromImage()
    {
        if (!useManualIcon || iconImage == null || iconImage.sprite == null)
            return;

        if (iconOverride != iconImage.sprite)
            iconOverride = iconImage.sprite;
    }

    void ApplyManualIconIfNeeded()
    {
        if (iconImage == null)
            return;

        var sprite = iconOverride ?? iconImage.sprite;
        if (sprite == null)
            sprite = MissionBriefingCatalog.LoadThumbnail(_simulation, ResolveItemKey());

        if (sprite == null)
            return;

        iconImage.sprite = sprite;
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;
        iconImage.enabled = true;
    }

    string ResolveItemKey() =>
        !string.IsNullOrWhiteSpace(itemKeyOverride) ? itemKeyOverride.Trim() : gameObject.name.Trim();

    void EnsureReferences()
    {
        if (containerRect == null)
        {
            var container = transform.Find("Container");
            if (container != null)
                containerRect = container as RectTransform;
        }

        if (containerRect != null && iconImage == null)
        {
            var icon = containerRect.Find("Icon");
            if (icon != null)
                iconImage = icon.GetComponent<Image>();
        }

        if (containerRect != null && locationHintText == null)
        {
            var hint = containerRect.Find("LocationHint");
            if (hint != null)
                locationHintText = hint.GetComponent<TextMeshProUGUI>();
        }

        if (itemLabelText == null)
        {
            var label = transform.Find("Label");
            if (label != null)
                itemLabelText = label.GetComponent<TextMeshProUGUI>();
        }

        if (itemLabelText == null)
        {
            var legacy = transform.Find("Text (TMP)");
            if (legacy != null)
                itemLabelText = legacy.GetComponent<TextMeshProUGUI>();
        }
    }

    void EnsureLocationHintUi()
    {
        if (containerRect == null)
            return;

        if (locationHintText != null)
            return;

        var hintGo = new GameObject("LocationHint", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintGo.transform.SetParent(containerRect, false);

        var hintRect = hintGo.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 8f);
        hintRect.sizeDelta = new Vector2(-16f, 48f);

        locationHintText = hintGo.GetComponent<TextMeshProUGUI>();
        locationHintText.raycastTarget = false;
        locationHintText.fontSize = 20f;
        locationHintText.alignment = TextAlignmentOptions.Center;
        locationHintText.textWrappingMode = TextWrappingModes.Normal;
        locationHintText.color = new Color(0.92f, 0.92f, 0.92f, 1f);

        if (itemLabelText != null && itemLabelText.font != null)
            locationHintText.font = itemLabelText.font;
    }

    void ApplyIcon(string itemKey, MissionBriefingCatalog.Entry entry)
    {
        if (iconImage == null)
            return;

        Sprite sprite = iconOverride;

        if (sprite == null && MissionBriefingIconLibrary.Instance != null)
            sprite = MissionBriefingIconLibrary.Instance.GetIcon(_simulation, itemKey);

        if (sprite == null)
            sprite = MissionBriefingCatalog.LoadThumbnail(_simulation, itemKey);

#if UNITY_EDITOR
        if (sprite == null &&
            (!string.IsNullOrWhiteSpace(entry.PrefabAssetPath) || !string.IsNullOrWhiteSpace(entry.SceneObjectName)))
            sprite = MissionBriefingEditorIconLoader.GetSpriteForEntry(entry);
#endif

        if (sprite != null)
        {
            iconImage.sprite = sprite;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;
            iconImage.enabled = true;
            return;
        }

        iconImage.sprite = null;
        iconImage.enabled = false;
    }
}
