using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Small square panel with text above an important object during Environment Learning.
/// Assign your own panel prefab (Image + TMP child) or leave empty for a built-in default.
/// </summary>
[DisallowMultipleComponent]
public class WorldItemLabel : MonoBehaviour
{
    [TextArea]
    public string labelText = "פריט";

    [Tooltip("Optional child Transform — drag in Scene to place the panel exactly (overrides World Offset).")]
    public Transform labelAnchor;

    [Tooltip("Offset from this object's pivot when Label Anchor is empty.")]
    public Vector3 worldOffset = new Vector3(0f, 2.5f, 0f);

    [Tooltip("Your designed square panel prefab (Image + TextMeshProUGUI).")]
    public GameObject labelPanelPrefab;

    [Tooltip("Optional per-item size (e.g. Mamad: 96×40). Zero = use EnvironmentLearning global size.")]
    public Vector2 labelPanelSizeOverride;

    public bool useRightToLeftText;

    [Header("Default panel (when prefab is empty)")]
    public Vector2 defaultPanelSize = new Vector2(140f, 44f);
    public Color defaultPanelColor = Color.white;
    public Color defaultTextColor = Color.white;
    public float defaultFontSize = 18f;
    public float worldCanvasScale = 0.006f;

    Transform _anchor;
    TextMeshProUGUI _labelTmp;
    bool _visible;

    void Awake()
    {
        EnsureLabelBuilt();
        SetVisible(false);
    }

    void OnEnable()
    {
        if (EnvironmentLearningController.Instance != null)
            EnvironmentLearningController.Instance.Register(this);
    }

    void OnDisable()
    {
        if (EnvironmentLearningController.Instance != null)
            EnvironmentLearningController.Instance.Unregister(this);
    }

    public void SetLabelText(string text)
    {
        labelText = text;
        ApplyText();
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;
        if (_anchor != null)
            _anchor.gameObject.SetActive(visible);
    }

    public void EnsureLabelBuilt()
    {
        if (_anchor == null)
            BuildLabel();
        else
            SyncAnchorPosition();
    }

    public void ApplyAppearanceFromController()
    {
        if (_anchor == null)
            return;

        var panelRoot = _anchor.childCount > 0 ? _anchor.GetChild(0).gameObject : null;
        if (panelRoot != null)
            ApplyTourPanelAppearance(panelRoot);
    }

    void BuildLabel()
    {
        if (_anchor != null)
            return;

        _anchor = new GameObject($"{name}_LabelAnchor_Runtime").transform;
        _anchor.SetParent(transform, false);
        SyncAnchorPosition();

        if (labelPanelPrefab != null)
        {
            var instance = Instantiate(labelPanelPrefab, _anchor);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            ApplyTourPanelAppearance(instance);
            _labelTmp = instance.GetComponentInChildren<TextMeshProUGUI>(true);
            if (instance.GetComponent<WorldLabelBillboard>() == null)
                instance.AddComponent<WorldLabelBillboard>();
            ApplyText();
            return;
        }

        BuildDefaultPanel();
    }

    void BuildDefaultPanel()
    {
        var canvasGo = new GameObject("LabelCanvas");
        canvasGo.transform.SetParent(_anchor, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        ApplyTourPanelAppearance(canvasGo);

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var image = panelGo.AddComponent<Image>();
        image.sprite = ResolvePanelSprite();
        image.color = ResolvePanelColor();
        image.raycastTarget = false;

        var textGo = new GameObject("LabelText");
        textGo.transform.SetParent(panelGo.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 6f);
        textRect.offsetMax = new Vector2(-10f, -6f);

        _labelTmp = textGo.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            _labelTmp.font = TMP_Settings.defaultFontAsset;
        _labelTmp.alignment = TextAlignmentOptions.Center;
        _labelTmp.fontSize = defaultFontSize;
        _labelTmp.color = defaultTextColor;
        _labelTmp.raycastTarget = false;
        _labelTmp.enableWordWrapping = false;

        canvasGo.AddComponent<WorldLabelBillboard>();
        ApplyText();
    }

    void ApplyText()
    {
        if (_labelTmp == null)
            return;

        _labelTmp.isRightToLeftText = useRightToLeftText;
        _labelTmp.text = labelText;
    }

    static Sprite ResolvePanelSprite()
    {
        var ctrl = EnvironmentLearningController.Instance;
        if (ctrl != null && ctrl.worldLabelPanelSprite != null)
            return ctrl.worldLabelPanelSprite;

        return WorldLabelAppearance.PanelSprite;
    }

    static Color ResolvePanelColor()
    {
        var ctrl = EnvironmentLearningController.Instance;
        if (ctrl != null)
            return ctrl.worldLabelPanelColor;

        return WorldLabelAppearance.PanelColor;
    }

    void ApplyTourPanelAppearance(GameObject panelRoot)
    {
        var ctrl = EnvironmentLearningController.Instance;
        float scale = ctrl != null ? ctrl.worldLabelWorldScale : worldCanvasScale;
        Vector2 size = labelPanelSizeOverride.sqrMagnitude > 0.01f
            ? labelPanelSizeOverride
            : ctrl != null
                ? ctrl.worldLabelPanelSize
                : defaultPanelSize;
        float fontSize = ctrl != null && ctrl.worldLabelFontSize > 0f
            ? ctrl.worldLabelFontSize
            : defaultFontSize;

        var tmp = panelRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            if (fontSize > 0f)
                tmp.fontSize = fontSize;
            tmp.isRightToLeftText = useRightToLeftText;
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;

            if (!string.IsNullOrEmpty(labelText))
                tmp.text = labelText;

            if (labelPanelSizeOverride.sqrMagnitude <= 0.01f)
                size.x = ResolveAutoPanelWidth(size.x, fontSize, tmp);
        }

        var rect = panelRoot.GetComponent<RectTransform>();
        if (rect != null)
        {
            if (size.sqrMagnitude > 0.01f)
                rect.sizeDelta = size;
            rect.localScale = Vector3.one * Mathf.Max(0.0001f, scale);
        }
    }

    static float ResolveAutoPanelWidth(float minWidth, float fontSize, TextMeshProUGUI tmp)
    {
        if (tmp == null || string.IsNullOrEmpty(tmp.text))
            return minWidth;

        Vector2 preferred = tmp.GetPreferredValues(tmp.text);
        float padding = 24f;
        return Mathf.Max(minWidth, Mathf.Ceil(preferred.x + padding));
    }

    void SyncAnchorPosition()
    {
        if (_anchor == null)
            return;

        if (labelAnchor != null)
            _anchor.position = labelAnchor.position;
        else
            _anchor.localPosition = worldOffset;
    }

#if UNITY_EDITOR
    public void EnsureLabelAnchor()
    {
        Transform existing = transform.Find("LabelAnchor");
        if (existing == null)
        {
            var go = new GameObject("LabelAnchor");
            existing = go.transform;
            existing.SetParent(transform, false);
            existing.localPosition = worldOffset.sqrMagnitude > 0.001f ? worldOffset : new Vector3(0f, 2.5f, 0f);
        }

        labelAnchor = existing;
        Selection.activeTransform = existing;
        EditorGUIUtility.PingObject(existing.gameObject);
        EnsureLabelBuilt();
        EditorUtility.SetDirty(this);
    }

    public void DrawSceneHandles()
    {
        Transform handle = labelAnchor != null ? labelAnchor : transform;
        EditorGUI.BeginChangeCheck();
        Vector3 world = Handles.PositionHandle(handle.position, Quaternion.identity);
        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(handle, "Move Label Anchor");
        if (labelAnchor != null)
            labelAnchor.position = world;
        else
            worldOffset = transform.InverseTransformPoint(world);

        EnsureLabelBuilt();
        EditorUtility.SetDirty(this);
    }

    void OnValidate()
    {
        if (_labelTmp != null)
            ApplyText();
        if (_anchor != null)
            SyncAnchorPosition();
    }
#endif
}
