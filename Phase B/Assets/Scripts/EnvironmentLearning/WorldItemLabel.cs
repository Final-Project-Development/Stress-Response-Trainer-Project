using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Small square panel with text above an important object during Environment Learning.
/// Uses a single scene LabelAnchor (position) and optional ViewAnchor (tour stand/look).
/// </summary>
[DisallowMultipleComponent]
public class WorldItemLabel : MonoBehaviour
{
    public const string LabelAnchorName = "LabelAnchor";
    public const string ViewAnchorName = "ViewAnchor";
    public const string LabelCanvasName = "LabelCanvas";

    [TextArea]
    public string labelText = "Item";

    [Tooltip("Child Transform for label position. One per object.")]
    public Transform labelAnchor;

    [Tooltip("Child Transform for tour stand position. Blue axis = look direction.")]
    public Transform viewAnchor;

    [Tooltip("Offset from this object's pivot when Label Anchor is empty.")]
    public Vector3 worldOffset = new Vector3(0f, 2.5f, 0f);

    [Tooltip("Stand offset when View Anchor is empty.")]
    public Vector3 viewOffset = new Vector3(0f, 0f, -2f);

    [Tooltip("Your designed square panel prefab (Image + TextMeshProUGUI).")]
    public GameObject labelPanelPrefab;

    [Tooltip("Optional per-item size. Zero = use EnvironmentLearning global size.")]
    public Vector2 labelPanelSizeOverride;

    public bool useRightToLeftText;

    [Header("Default panel (when prefab is empty)")]
    public Vector2 defaultPanelSize = new Vector2(140f, 44f);
    public Color defaultPanelColor = Color.white;
    public Color defaultTextColor = Color.white;
    public float defaultFontSize = 18f;
    public float worldCanvasScale = 0.006f;

    static Transform _sharedLabelsRoot;

    GameObject _labelCanvas;
    TextMeshProUGUI _labelTmp;
    bool _visible;

    void Awake()
    {
        ResolveLabelAnchorReference();
        ResolveViewAnchorReference();
        CleanupLegacyRuntimeLabelObjects();
        EnsureLabelBuilt();
        SetVisible(false);
    }

    public bool TryGetViewAnchor(out Transform anchor)
    {
        anchor = ResolveViewAnchorReference();
        return anchor != null;
    }

    public bool TryGetLabelAnchor(out Transform anchor)
    {
        anchor = ResolveLabelAnchorReference();
        return anchor != null;
    }

    Transform ResolveLabelAnchorReference()
    {
        if (labelAnchor != null)
            return labelAnchor;

        labelAnchor = FindOwnedAnchorRecursive(transform, LabelAnchorName);
        return labelAnchor;
    }

    Transform ResolveViewAnchorReference()
    {
        if (viewAnchor != null)
            return viewAnchor;

        viewAnchor = FindOwnedAnchorRecursive(transform, ViewAnchorName);
        return viewAnchor;
    }

    Transform FindOwnedAnchorRecursive(Transform root, string anchorName)
    {
        if (root == null || string.IsNullOrWhiteSpace(anchorName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == anchorName)
                return child;

            var otherLabel = child.GetComponent<WorldItemLabel>();
            if (otherLabel != null && otherLabel != this)
                continue;

            Transform nested = FindOwnedAnchorRecursive(child, anchorName);
            if (nested != null)
                return nested;
        }

        return null;
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
        if (_labelCanvas != null)
            _labelCanvas.SetActive(visible);
    }

    public void EnsureLabelBuilt()
    {
        if (_labelCanvas == null)
            BuildLabel();
        else
        {
            SyncLabelCanvasPosition();
            ApplyAppearanceFromController();
        }
    }

    public void ApplyAppearanceFromController()
    {
        if (_labelCanvas == null)
            return;

        SyncLabelCanvasPosition();
        ApplyTourPanelAppearance(_labelCanvas);
    }

    void BuildLabel()
    {
        if (ResolveLabelAnchorReference() == null)
            CreateDefaultLabelAnchor();

        CleanupLegacyRuntimeLabelObjects();

        if (TryAdoptExistingLabelCanvas())
            return;

        Transform labelsRoot = ResolveLabelsParent();

        if (labelPanelPrefab != null)
        {
            var instance = Instantiate(labelPanelPrefab, labelsRoot);
            instance.name = GetLabelCanvasObjectName();
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            _labelCanvas = instance;
            SyncLabelCanvasPosition();
            ApplyTourPanelAppearance(instance);
            _labelTmp = instance.GetComponentInChildren<TextMeshProUGUI>(true);
            if (instance.GetComponent<WorldLabelBillboard>() == null)
                instance.AddComponent<WorldLabelBillboard>();
            ApplyText();
            return;
        }

        BuildDefaultPanel(labelsRoot);
    }

    bool TryAdoptExistingLabelCanvas()
    {
        Transform labelsRoot = ResolveLabelsParent();
        Transform existing = labelsRoot.Find(GetLabelCanvasObjectName());
        if (existing == null)
            return false;

        _labelCanvas = existing.gameObject;
        _labelTmp = _labelCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
        SyncLabelCanvasPosition();
        ApplyTourPanelAppearance(_labelCanvas);
        ApplyText();
        return true;
    }

    string GetLabelCanvasObjectName() => $"{name}_{LabelCanvasName}";

    void SyncLabelCanvasPosition()
    {
        if (_labelCanvas == null)
            return;

        Transform anchor = ResolveLabelAnchorReference();
        if (anchor != null)
            _labelCanvas.transform.position = anchor.position;
        else
            _labelCanvas.transform.position = transform.TransformPoint(worldOffset);
    }

    static Transform ResolveLabelsParent()
    {
        var ctrl = EnvironmentLearningController.Instance;
        if (ctrl != null && ctrl.labelsRoot != null)
            return ctrl.labelsRoot;

        if (_sharedLabelsRoot == null)
        {
            var existing = GameObject.Find("WorldItemLabels_Runtime");
            if (existing == null)
            {
                existing = new GameObject("WorldItemLabels_Runtime");
                existing.hideFlags = HideFlags.DontSave;
            }

            _sharedLabelsRoot = existing.transform;
        }

        return _sharedLabelsRoot;
    }

    void BuildDefaultPanel(Transform labelsRoot)
    {
        var canvasGo = new GameObject(GetLabelCanvasObjectName());
        canvasGo.transform.SetParent(labelsRoot, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

        _labelCanvas = canvasGo;
        SyncLabelCanvasPosition();
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

    Transform CreateDefaultLabelAnchor()
    {
        var go = new GameObject(LabelAnchorName);
        var created = go.transform;
        created.SetParent(transform, false);
        created.localPosition = worldOffset.sqrMagnitude > 0.001f ? worldOffset : new Vector3(0f, 2.5f, 0f);
        labelAnchor = created;
        return created;
    }

    void CleanupLegacyRuntimeLabelObjects()
    {
        Transform host = ResolveLabelAnchorReference();
        Transform labelsRoot = ResolveLabelsParent();

        if (host != null)
        {
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                Transform child = host.GetChild(i);
                if (child.name.EndsWith("_LabelAnchor_Runtime"))
                {
                    DestroyObjectSafe(child.gameObject);
                    continue;
                }

                if (child.name == LabelCanvasName || child.name == GetLabelCanvasObjectName())
                    ReparentLabelCanvas(child, labelsRoot);
            }
        }

        var legacyRoot = GameObject.Find("WorldItemLabels_Runtime");
        if (legacyRoot == null)
            return;

        for (int i = legacyRoot.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = legacyRoot.transform.GetChild(i);
            if (child.name.StartsWith(name + "_LabelAnchor_Runtime"))
                DestroyObjectSafe(child.gameObject);
        }
    }

    void ReparentLabelCanvas(Transform canvas, Transform labelsRoot)
    {
        if (canvas == null || labelsRoot == null)
            return;

        canvas.SetParent(labelsRoot, true);
        canvas.name = GetLabelCanvasObjectName();
        _labelCanvas = canvas.gameObject;
        _labelTmp = _labelCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    static void DestroyObjectSafe(Object obj)
    {
        if (obj == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Object.DestroyImmediate(obj);
        else
#endif
            Object.Destroy(obj);
    }

    void ApplyText()
    {
        if (_labelTmp == null)
            return;

        _labelTmp.isRightToLeftText = false;
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
            tmp.isRightToLeftText = false;
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;

            if (!string.IsNullOrEmpty(labelText))
                tmp.text = labelText;

            if (labelPanelSizeOverride.sqrMagnitude <= 0.01f)
                size.x = ResolveAutoPanelWidth(size.x, fontSize, tmp);
        }

        var image = panelRoot.GetComponentInChildren<Image>(true);
        if (image != null)
        {
            image.sprite = ResolvePanelSprite();
            image.color = ResolvePanelColor();
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

#if UNITY_EDITOR
    public void EnsureLabelAnchor()
    {
        PruneDuplicateAnchors(LabelAnchorName, ref labelAnchor);
        if (labelAnchor == null)
            labelAnchor = CreateDefaultLabelAnchor();

        CleanupLegacyRuntimeLabelObjects();
        EnsureLabelBuilt();
        Selection.activeTransform = labelAnchor;
        EditorGUIUtility.PingObject(labelAnchor.gameObject);
        EditorUtility.SetDirty(this);
    }

    public void EnsureViewAnchor()
    {
        PruneDuplicateAnchors(ViewAnchorName, ref viewAnchor);
        if (viewAnchor == null)
        {
            var go = new GameObject(ViewAnchorName);
            viewAnchor = go.transform;
            viewAnchor.SetParent(transform, false);
            viewAnchor.localPosition = viewOffset.sqrMagnitude > 0.001f ? viewOffset : new Vector3(0f, 0f, -2f);
            viewAnchor.localRotation = Quaternion.identity;
        }

        Selection.activeTransform = viewAnchor;
        EditorGUIUtility.PingObject(viewAnchor.gameObject);
        EditorUtility.SetDirty(this);
    }

    public void PruneDuplicateAnchorsForItem()
    {
        PruneDuplicateAnchors(LabelAnchorName, ref labelAnchor);
        PruneDuplicateAnchors(ViewAnchorName, ref viewAnchor);
        CleanupLegacyRuntimeLabelObjects();
        EditorUtility.SetDirty(this);
    }

    void PruneDuplicateAnchors(string anchorName, ref Transform keep)
    {
        var matches = new List<Transform>();
        CollectOwnedAnchors(transform, anchorName, matches);
        if (matches.Count == 0)
            return;

        if (keep == null || !matches.Contains(keep))
            keep = matches[0];

        for (int i = 0; i < matches.Count; i++)
        {
            if (matches[i] == keep)
                continue;

            Undo.DestroyObjectImmediate(matches[i].gameObject);
        }
    }

    void CollectOwnedAnchors(Transform root, string anchorName, List<Transform> results)
    {
        if (root == null)
            return;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == anchorName)
                results.Add(child);

            var otherLabel = child.GetComponent<WorldItemLabel>();
            if (otherLabel != null && otherLabel != this)
                continue;

            CollectOwnedAnchors(child, anchorName, results);
        }
    }

    public void DrawSceneHandles()
    {
        DrawLabelAnchorSceneHandles();
        DrawViewAnchorSceneHandles();
    }

    void DrawLabelAnchorSceneHandles()
    {
        Transform handle = labelAnchor != null ? labelAnchor : transform;
        Handles.color = new Color(0.35f, 0.85f, 1f, 1f);

        EditorGUI.BeginChangeCheck();
        Vector3 world = Handles.PositionHandle(handle.position, Quaternion.identity);
        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(handle, "Move Label Anchor");
        if (labelAnchor != null)
            labelAnchor.position = world;
        else
            worldOffset = transform.InverseTransformPoint(world);

        SyncLabelCanvasPosition();
        EditorUtility.SetDirty(this);
    }

    void DrawViewAnchorSceneHandles()
    {
        if (viewAnchor == null)
            return;

        Handles.color = new Color(0.3f, 1f, 0.45f, 1f);

        EditorGUI.BeginChangeCheck();
        Vector3 worldPos = Handles.PositionHandle(viewAnchor.position, viewAnchor.rotation);
        Quaternion worldRot = Handles.RotationHandle(viewAnchor.rotation, viewAnchor.position);
        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(viewAnchor, "Move View Anchor");
        viewAnchor.SetPositionAndRotation(worldPos, worldRot);
        EditorUtility.SetDirty(this);
    }

    void OnDrawGizmosSelected()
    {
        if (viewAnchor == null)
            return;

        Gizmos.color = new Color(0.3f, 1f, 0.45f, 0.9f);
        Gizmos.DrawSphere(viewAnchor.position, 0.12f);
        Gizmos.DrawRay(viewAnchor.position, viewAnchor.forward * 0.9f);
    }

    void OnValidate()
    {
        if (labelAnchor == null)
            ResolveLabelAnchorReference();
        if (viewAnchor == null)
            ResolveViewAnchorReference();

        if (_labelCanvas != null)
            SyncLabelCanvasPosition();

        if (_labelTmp != null)
            ApplyText();
    }
#endif
}
