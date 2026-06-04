using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small square panel with text above an important object during Environment Learning.
/// Assign your own panel prefab (Image + TMP child) or leave empty for a built-in default.
/// </summary>
[DisallowMultipleComponent]
public class WorldItemLabel : MonoBehaviour
{
    [TextArea]
    public string labelText = "פריט";

    [Tooltip("Offset from this object's pivot (usually up).")]
    public Vector3 worldOffset = new Vector3(0f, 2.5f, 0f);

    [Tooltip("Your designed square panel prefab (Image + TextMeshProUGUI).")]
    public GameObject labelPanelPrefab;

    [Header("Default panel (when prefab is empty)")]
    public Vector2 defaultPanelSize = new Vector2(280f, 96f);
    public Color defaultPanelColor = Color.white;
    public Color defaultTextColor = Color.white;
    public float defaultFontSize = 24f;
    public float worldCanvasScale = 0.01f;

    Transform _anchor;
    TextMeshProUGUI _labelTmp;
    bool _visible;

    void Awake()
    {
        BuildLabel();
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

    void BuildLabel()
    {
        if (_anchor != null)
            return;

        _anchor = new GameObject($"{name}_LabelAnchor").transform;
        _anchor.SetParent(transform, false);
        _anchor.localPosition = worldOffset;

        if (labelPanelPrefab != null)
        {
            var instance = Instantiate(labelPanelPrefab, _anchor);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
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
        canvasRect.sizeDelta = defaultPanelSize.sqrMagnitude > 0f
            ? defaultPanelSize
            : WorldLabelAppearance.PanelSize;
        canvasRect.localScale = Vector3.one * worldCanvasScale;

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
        _labelTmp.alignment = TextAlignmentOptions.Center;
        _labelTmp.fontSize = defaultFontSize;
        _labelTmp.color = defaultTextColor;
        _labelTmp.raycastTarget = false;
        _labelTmp.enableWordWrapping = true;

        canvasGo.AddComponent<WorldLabelBillboard>();
        ApplyText();
    }

    void ApplyText()
    {
        if (_labelTmp != null)
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

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_labelTmp != null)
            ApplyText();
    }
#endif
}
