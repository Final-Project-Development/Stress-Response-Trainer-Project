using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Environment Learning sidebar navigation. Design the panel yourself in the Canvas;
/// assign Sidebar Panel and add EnvironmentLearningTourNavButton to each button.
/// </summary>
public class EnvironmentLearningTourGuide : MonoBehaviour
{
    public const string SidebarObjectName = "EnvironmentLearningTourSidebar";

    [Header("UI (design manually in scene)")]
    [Tooltip("Your sidebar root GameObject, e.g. EnvironmentLearningTourSidebar.")]
    public GameObject sidebarPanel;

    [Tooltip("Optional explicit RectTransform for cursor hit area. Empty = use Sidebar Panel RectTransform.")]
    public RectTransform sidebarRoot;

    [Header("Teleport")]
    public float defaultStandoffMeters = 2.5f;
    public float eyeHeightMeters = 1.6f;

    Transform _playerRoot;
    SimpleFPSController _playerController;
    bool _active;
    readonly List<(Button button, UnityAction action)> _wiredButtons = new List<(Button, UnityAction)>();

    void Awake()
    {
        ResolveSidebarReferences();
        EnsureSidebarHidden();
    }

    public void ResolveSidebarReferences()
    {
        if (sidebarPanel == null)
            sidebarPanel = FindSidebarObject();

        if (sidebarRoot == null && sidebarPanel != null)
        {
            sidebarRoot = sidebarPanel.GetComponent<RectTransform>();
            if (sidebarRoot == null)
            {
                var background = sidebarPanel.transform.Find("Background");
                if (background != null)
                    sidebarRoot = background.GetComponent<RectTransform>();
            }

            if (sidebarRoot == null)
                sidebarRoot = sidebarPanel.GetComponentInChildren<RectTransform>(true);
        }
    }

    public static GameObject FindSidebarObject()
    {
        foreach (var go in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go != null && go.name.Trim() == SidebarObjectName)
                return go;
        }

        return null;
    }

    public static void HideSidebarInScene()
    {
        var sidebar = FindSidebarObject();
        if (sidebar != null && sidebar.activeSelf)
            sidebar.SetActive(false);
    }

    public void BeginGuide()
    {
        _active = true;

        if (sidebarPanel == null && sidebarRoot == null)
        {
            Debug.LogWarning(
                "EnvironmentLearningTourGuide: assign Sidebar Panel in the Inspector " +
                "(your EnvironmentLearningTourSidebar object).");
            return;
        }

        ApplySectionHeaderTexts();
        WireNavButtons();

        if (sidebarPanel != null)
            sidebarPanel.SetActive(true);
        else if (sidebarRoot != null)
            sidebarRoot.gameObject.SetActive(true);

        WirePlayerCursorMode(true);
    }

    public void EndGuide()
    {
        _active = false;
        UnwireNavButtons();
        EnsureSidebarHidden();
        WirePlayerCursorMode(false);
    }

    public void EnsureSidebarHidden()
    {
        ResolveSidebarReferences();

        if (sidebarPanel != null)
            sidebarPanel.SetActive(false);
        else if (sidebarRoot != null)
            sidebarRoot.gameObject.SetActive(false);
        else
            HideSidebarInScene();
    }

    void WireNavButtons()
    {
        UnwireNavButtons();

        Transform searchRoot = sidebarPanel != null ? sidebarPanel.transform : sidebarRoot;
        if (searchRoot == null)
            return;

        var wiredButtons = new HashSet<Button>();
        var navButtons = searchRoot.GetComponentsInChildren<EnvironmentLearningTourNavButton>(true);
        for (int i = 0; i < navButtons.Length; i++)
        {
            var nav = navButtons[i];
            if (nav == null || string.IsNullOrWhiteSpace(nav.sceneObjectName))
                continue;

            var button = nav.GetComponent<Button>();
            if (button == null || !wiredButtons.Add(button))
                continue;

            EnvironmentLearningTourNavButton captured = nav;
            UnityAction action = () => GoToNavButton(captured);
            button.onClick.AddListener(action);
            _wiredButtons.Add((button, action));
        }
    }

    void ApplySectionHeaderTexts()
    {
        Transform searchRoot = sidebarPanel != null ? sidebarPanel.transform : sidebarRoot;
        if (searchRoot == null)
            return;

        ApplySectionHeader(searchRoot, "sim1_TXT", "Simulation 1");
        ApplySectionHeader(searchRoot, "sim2_TXT", "Simulation 2");
    }

    static void ApplySectionHeader(Transform searchRoot, string headerName, string label)
    {
        if (searchRoot == null || string.IsNullOrWhiteSpace(headerName))
            return;

        Transform header = FindChildByName(searchRoot, headerName);
        if (header == null)
            return;

        var tmp = header.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            tmp = header.gameObject.AddComponent<TextMeshProUGUI>();

        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 20f;
        tmp.enableAutoSizing = false;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.92f, 0.9f, 1f, 1f);
        tmp.raycastTarget = false;
    }

    static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        if (root.name.Trim() == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    void UnwireNavButtons()
    {
        for (int i = 0; i < _wiredButtons.Count; i++)
        {
            if (_wiredButtons[i].button != null)
                _wiredButtons[i].button.onClick.RemoveListener(_wiredButtons[i].action);
        }

        _wiredButtons.Clear();
    }

    void WirePlayerCursorMode(bool tourActive)
    {
        if (_playerController == null)
            ResolvePlayer();

        if (_playerController != null)
            _playerController.SetLearningTourSidebar(ResolveSidebarRect(), tourActive);
    }

    RectTransform ResolveSidebarRect()
    {
        if (sidebarRoot != null)
            return sidebarRoot;

        if (sidebarPanel == null)
            return null;

        var rect = sidebarPanel.GetComponent<RectTransform>();
        if (rect != null)
            return rect;

        var background = sidebarPanel.transform.Find("Background");
        if (background != null)
        {
            rect = background.GetComponent<RectTransform>();
            if (rect != null)
                return rect;
        }

        return sidebarPanel.GetComponentInChildren<RectTransform>(true);
    }

    void ResolvePlayer()
    {
        if (_playerRoot != null)
            return;

        var fps = FindFirstObjectByType<SimpleFPSController>(FindObjectsInactive.Include);
        if (fps == null)
            return;

        _playerRoot = fps.transform;
        _playerController = fps;
    }

    public void GoToNavButton(EnvironmentLearningTourNavButton nav)
    {
        if (nav == null || string.IsNullOrWhiteSpace(nav.sceneObjectName))
            return;

        float standoff = nav.standoffMeters > 0.1f ? nav.standoffMeters : defaultStandoffMeters;
        GoToSceneObject(nav.sceneObjectName, standoff);
    }

    public void GoToSceneObject(string objectName, float standoffMeters = 0f)
    {
        if (!_active)
            return;

        ResolvePlayer();
        if (_playerRoot == null)
            return;

        GameObject target = FindSceneObject(objectName);
        if (target == null)
        {
            Debug.LogWarning($"Tour guide: object '{objectName}' not found.");
            return;
        }

        float standoff = standoffMeters > 0.1f ? standoffMeters : defaultStandoffMeters;
        Vector3 focus = GetFocusWorldPoint(target);
        Vector3 stand = ComputeStandPosition(focus, standoff);
        Quaternion look = ComputeLookRotation(stand, focus);

        var fps = _playerRoot.GetComponent<SimpleFPSController>();
        if (fps != null)
            fps.TeleportTo(stand, look);
        else
            _playerRoot.SetPositionAndRotation(stand, look);

        PlayerGroundSnap.TrySnapToGround(_playerRoot);
    }

    static GameObject FindSceneObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        GameObject exact = FindByExactName(objectName);
        if (exact != null)
            return exact;

        string normalizedSearch = EnvironmentLearningTourObjectNames.Normalize(objectName);
        if (string.IsNullOrEmpty(normalizedSearch))
            return null;

        foreach (var label in FindObjectsByType<WorldItemLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (label == null)
                continue;

            if (MatchesTourTarget(label.gameObject.name, label.labelText, objectName, normalizedSearch))
                return label.gameObject;
        }

        foreach (var go in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go == null)
                continue;

            if (EnvironmentLearningTourObjectNames.Normalize(go.name) == normalizedSearch)
                return go;
        }

        return null;
    }

    static GameObject FindByExactName(string objectName)
    {
        GameObject inactiveMatch = null;

        foreach (var go in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go == null || go.name != objectName)
                continue;

            if (go.activeInHierarchy)
                return go;

            if (inactiveMatch == null)
                inactiveMatch = go;
        }

        return inactiveMatch;
    }

    static bool MatchesTourTarget(string hostName, string labelText, string objectName, string normalizedSearch)
    {
        if (EnvironmentLearningTourObjectNames.Normalize(hostName) == normalizedSearch)
            return true;

        if (!string.IsNullOrWhiteSpace(labelText) &&
            EnvironmentLearningTourObjectNames.Normalize(labelText) == normalizedSearch)
            return true;

        foreach (var entry in EnvironmentLearningTourCatalog.Items)
        {
            if (entry.ObjectName != objectName &&
                EnvironmentLearningTourObjectNames.Normalize(entry.ObjectName) != normalizedSearch &&
                EnvironmentLearningTourObjectNames.Normalize(entry.DisplayName) != normalizedSearch)
                continue;

            if (EnvironmentLearningTourObjectNames.Normalize(hostName) ==
                EnvironmentLearningTourObjectNames.Normalize(entry.ObjectName))
                return true;

            if (!string.IsNullOrWhiteSpace(labelText) &&
                EnvironmentLearningTourObjectNames.Normalize(labelText) ==
                EnvironmentLearningTourObjectNames.Normalize(entry.DisplayName))
                return true;
        }

        return false;
    }

    static Vector3 GetFocusWorldPoint(GameObject target)
    {
        var label = target.GetComponent<WorldItemLabel>();
        if (label != null && label.labelAnchor != null)
            return label.labelAnchor.position;

        if (label != null && label.worldOffset.sqrMagnitude > 0.001f)
            return target.transform.TransformPoint(label.worldOffset);

        if (TryGetRendererBoundsCenter(target.transform, out Vector3 center))
            return center + Vector3.up * 0.8f;

        return target.transform.position + Vector3.up * 1.2f;
    }

    static bool TryGetRendererBoundsCenter(Transform root, out Vector3 center)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            center = default;
            return false;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        center = bounds.center;
        return true;
    }

    Vector3 ComputeStandPosition(Vector3 focus, float standoff)
    {
        Vector3 flatOffset = focus - _playerRoot.position;
        flatOffset.y = 0f;
        if (flatOffset.sqrMagnitude < 0.25f)
            flatOffset = _playerRoot.forward;

        flatOffset.Normalize();
        Vector3 stand = focus - flatOffset * standoff;
        stand.y = focus.y - 0.8f + eyeHeightMeters;
        return stand;
    }

    static Quaternion ComputeLookRotation(Vector3 stand, Vector3 focus)
    {
        Vector3 lookDir = focus - stand;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude < 0.01f)
            return Quaternion.identity;

        return Quaternion.LookRotation(lookDir.normalized, Vector3.up);
    }
}
