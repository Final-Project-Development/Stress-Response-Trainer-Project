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

    [Header("Tour options menu (Background (1))")]
    public string optionsMenuObjectName = "Background (1)";
    public string optionsMenuTitleObjectName = "learnMenue";
    public string optionsMenuTitleText = "Tour options";
    public string toggleAlarmButtonName = "addAlarm";
    public string startSim1ButtonName = "startsim1";
    public string startSim2ButtonName = "startsim2";
    public string alarmOnButtonLabel = "Alarm on";
    public string alarmOffButtonLabel = "Alarm off";
    public string startSim1ButtonLabel = "Simulation 1";
    public string startSim2ButtonLabel = "Simulation 2";

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
        WireOptionsMenu();

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
        UnwireOptionsMenu();
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

            if (IsOptionsMenuControl(nav.transform))
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

    public void RefreshOptionsMenuPresentation()
    {
        ResolveOptionsMenu()?.ApplyPresentation();
    }

    void WireOptionsMenu()
    {
        UnwireOptionsMenu();
        EnvironmentLearningTourOptionsMenu optionsMenu = ResolveOptionsMenu();
        if (optionsMenu == null)
        {
            Debug.LogWarning(
                $"EnvironmentLearningTourGuide: options menu '{optionsMenuObjectName}' was not found under the sidebar.");
            return;
        }

        optionsMenu.titleObjectName = optionsMenuTitleObjectName;
        optionsMenu.titleText = optionsMenuTitleText;
        optionsMenu.toggleAlarmButtonName = toggleAlarmButtonName;
        optionsMenu.startSim1ButtonName = startSim1ButtonName;
        optionsMenu.startSim2ButtonName = startSim2ButtonName;
        optionsMenu.alarmOnButtonLabel = alarmOnButtonLabel;
        optionsMenu.alarmOffButtonLabel = alarmOffButtonLabel;
        optionsMenu.startSim1ButtonLabel = startSim1ButtonLabel;
        optionsMenu.startSim2ButtonLabel = startSim2ButtonLabel;
        optionsMenu.Wire();
    }

    void UnwireOptionsMenu()
    {
        ResolveOptionsMenu()?.Unwire();
    }

    EnvironmentLearningTourOptionsMenu ResolveOptionsMenu()
    {
        Transform searchRoot = sidebarPanel != null ? sidebarPanel.transform : sidebarRoot;
        if (searchRoot == null)
            return null;

        Transform optionsRoot = FindChildByName(searchRoot, optionsMenuObjectName);
        if (optionsRoot == null)
            return null;

        var optionsMenu = optionsRoot.GetComponent<EnvironmentLearningTourOptionsMenu>();
        if (optionsMenu == null)
            optionsMenu = optionsRoot.gameObject.AddComponent<EnvironmentLearningTourOptionsMenu>();

        return optionsMenu;
    }

    bool IsOptionsMenuControl(Transform control)
    {
        if (control == null)
            return false;

        return control.GetComponentInParent<EnvironmentLearningTourOptionsMenu>(true) != null;
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

        if (!TryFindTourTarget(objectName, out GameObject target, out WorldItemLabel label))
        {
            Debug.LogWarning($"Tour guide: object '{objectName}' not found.");
            return;
        }

        if (label != null && label.TryGetViewAnchor(out Transform viewAnchor))
        {
            PlayerGroundSnap.PlacePlayerAtViewAnchor(_playerRoot, viewAnchor);
            return;
        }

        if (TryResolveViewAnchor(target.transform, out viewAnchor))
        {
            PlayerGroundSnap.PlacePlayerAtViewAnchor(_playerRoot, viewAnchor);
            return;
        }

        float standoff = standoffMeters > 0.1f ? standoffMeters : defaultStandoffMeters;
        if (!TryComputeStandFacingLabelAnchor(target.transform, label, standoff, out Vector3 stand, out Vector3 lookTarget))
        {
            Debug.LogWarning(
                $"Tour guide: '{target.name}' has no ViewAnchor or LabelAnchor. " +
                "Add a ViewAnchor (recommended) or LabelAnchor on the WorldItemLabel object.");
            return;
        }

        Quaternion look = ComputeLookRotation(stand, lookTarget);

        var fps = _playerRoot.GetComponent<SimpleFPSController>();
        if (fps != null)
            fps.TeleportTo(stand, look);
        else
            _playerRoot.SetPositionAndRotation(stand, look);

        if (!PlayerGroundSnap.TrySnapNearReferenceHeight(_playerRoot, lookTarget.y))
            PlayerGroundSnap.TrySnapToGround(_playerRoot, rayHeight: 6f, maxRayDistance: 12f);
    }

    static bool TryFindTourTarget(string objectName, out GameObject target, out WorldItemLabel label)
    {
        target = null;
        label = null;
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        string normalizedSearch = EnvironmentLearningTourObjectNames.Normalize(objectName);

        foreach (var worldLabel in FindObjectsByType<WorldItemLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (worldLabel == null || IsTourSidebarUi(worldLabel.gameObject))
                continue;

            if (!MatchesTourTarget(
                    worldLabel.gameObject.name,
                    worldLabel.labelText,
                    objectName,
                    normalizedSearch))
                continue;

            target = worldLabel.gameObject;
            label = worldLabel;
            return true;
        }

        GameObject exact = FindWorldObjectByExactName(objectName);
        if (exact != null)
        {
            target = exact;
            label = exact.GetComponent<WorldItemLabel>();
            return true;
        }

        if (string.IsNullOrEmpty(normalizedSearch))
            return false;

        foreach (var go in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go == null || IsTourSidebarUi(go))
                continue;

            if (EnvironmentLearningTourObjectNames.Normalize(go.name) == normalizedSearch)
            {
                target = go;
                label = go.GetComponent<WorldItemLabel>();
                return true;
            }
        }

        return false;
    }

    static GameObject FindWorldObjectByExactName(string objectName)
    {
        GameObject inactiveMatch = null;

        foreach (var go in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go == null || go.name != objectName || IsTourSidebarUi(go))
                continue;

            if (go.activeInHierarchy)
                return go;

            if (inactiveMatch == null)
                inactiveMatch = go;
        }

        return inactiveMatch;
    }

    static bool IsTourSidebarUi(GameObject go)
    {
        if (go == null)
            return false;

        Transform current = go.transform;
        while (current != null)
        {
            if (current.name.Trim() == SidebarObjectName.Trim())
                return true;

            current = current.parent;
        }

        return false;
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

    static bool TryComputeStandFacingLabelAnchor(
        Transform target,
        WorldItemLabel label,
        float standoff,
        out Vector3 stand,
        out Vector3 lookTarget)
    {
        stand = default;
        lookTarget = default;
        if (target == null)
            return false;

        Transform anchor = ResolveLabelAnchor(label, target);
        if (anchor == null)
            return false;

        lookTarget = anchor.position;
        Vector3 itemOrigin = GetItemViewOrigin(target);
        Vector3 viewDir = FlattenXZ(lookTarget - itemOrigin);
        if (viewDir.sqrMagnitude < 0.04f)
            viewDir = GetObjectForwardXZ(target);
        else
            viewDir.Normalize();

        float backOff = Mathf.Max(standoff, 0.75f);
        stand = lookTarget - viewDir * backOff;
        stand.y = lookTarget.y;
        return true;
    }

    static bool TryResolveViewAnchor(Transform target, out Transform viewAnchor)
    {
        viewAnchor = null;
        if (target == null)
            return false;

        var label = target.GetComponent<WorldItemLabel>();
        if (label != null && label.TryGetViewAnchor(out viewAnchor))
            return true;

        viewAnchor = target.Find(WorldItemLabel.ViewAnchorName);
        return viewAnchor != null;
    }

    static Transform ResolveLabelAnchor(WorldItemLabel label, Transform target)
    {
        if (label != null && label.TryGetLabelAnchor(out Transform anchor))
            return anchor;

        if (target == null)
            return null;

        return target.Find(WorldItemLabel.LabelAnchorName);
    }

    static Vector3 GetItemViewOrigin(Transform target)
    {
        if (TryGetRendererBoundsCenter(target, out Vector3 center))
            return new Vector3(center.x, target.position.y, center.z);

        return target.position;
    }

    static Vector3 FlattenXZ(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    static Vector3 GetObjectForwardXZ(Transform target)
    {
        Vector3 forward = FlattenXZ(target.forward);
        if (forward.sqrMagnitude < 0.01f)
            forward = FlattenXZ(target.rotation * Vector3.forward);

        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;

        return forward.normalized;
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

    static Quaternion ComputeLookRotation(Vector3 stand, Vector3 focus)
    {
        Vector3 lookDir = focus - stand;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude < 0.01f)
            return Quaternion.identity;

        return Quaternion.LookRotation(lookDir.normalized, Vector3.up);
    }
}
