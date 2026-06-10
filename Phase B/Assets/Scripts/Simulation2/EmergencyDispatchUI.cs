using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Simulation 2 — simple English dispatch screen (buttons). Open from <see cref="EmergencyDispatchStation"/> (E).
/// </summary>
public class EmergencyDispatchUI : MonoBehaviour
{
    public static EmergencyDispatchUI Instance { get; private set; }

    [Header("Optional — leave empty to build a simple panel at runtime")]
    [SerializeField] GameObject panelRoot;

    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI statusText;
    [SerializeField] Button reportButton;
    [SerializeField] Button closeButton;

    [Header("Audio (optional)")]
    [SerializeField] AudioSource uiAudioSource;
    [SerializeField] AudioClip dispatchConfirmClip;

    private GameManager _gameManager;
    private SimpleFPSController _player;
    private bool _runtimeBuilt;
    private bool _buttonsWired;

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        HidePanelImmediate();
    }

    void OnDestroy()
    {
        CancelInvoke();
        SetPlayerOverlay(false);
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        WireButtonCallbacks();
    }

    public void OpenPanel()
    {
        if (_gameManager == null)
            _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

        var flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        if (_gameManager != null && !_gameManager.HasFirstAidKit())
        {
            string msg = flow != null
                ? flow.sim2NeedKitHint
                : "Collect the first aid kit first.";
            _gameManager.ShowTransientMissionNote(msg, 4f);
            return;
        }

        if (_gameManager != null && !_gameManager.HasContactedCasualty())
        {
            string msg = flow != null
                ? flow.sim2NeedContactCasualtyBeforePhoneHint
                : "Find the wounded person first and press E on the casualty.";
            _gameManager.ShowTransientMissionNote(msg, 4f);
            return;
        }

        if (_gameManager != null && _gameManager.HasReportedEmergency())
        {
            string msg = flow != null
                ? flow.sim2AlreadyReportedHint
                : "You already called dispatch. Return to the wounded person.";
            _gameManager.ShowTransientMissionNote(msg, 3f);
            return;
        }

        EnsureRuntimePanel();
        if (statusText != null)
            statusText.text = "Press the button below to report the emergency to dispatch (101).";
        if (reportButton != null)
            reportButton.interactable = true;

        panelRoot.SetActive(true);
        SetPlayerOverlay(true);
    }

    public void ClosePanel()
    {
        if (panelRoot == null)
            return;

        panelRoot.SetActive(false);
        SetPlayerOverlay(false);
    }

    private void HidePanelImmediate()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void SetPlayerOverlay(bool open)
    {
        if (_player == null)
            _player = FindFirstObjectByType<SimpleFPSController>();

        if (_player != null)
            _player.SetOverlayUiOpen(open);
    }

    private void WireButtonCallbacks()
    {
        if (_buttonsWired)
            return;
        _buttonsWired = true;

        if (reportButton != null)
            reportButton.onClick.AddListener(OnReportClicked);
        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);
    }

    private void OnReportClicked()
    {
        if (_gameManager == null)
            _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

        _gameManager?.OnEmergencyReported();

        if (uiAudioSource != null && dispatchConfirmClip != null)
            uiAudioSource.PlayOneShot(dispatchConfirmClip);

        if (statusText != null)
            statusText.text = "Report sent. Ambulance dispatched — return to the wounded person.";

        if (reportButton != null)
            reportButton.interactable = false;

        Invoke(nameof(ClosePanel), 1.4f);
    }

    private void EnsureRuntimePanel()
    {
        if (panelRoot != null && titleText != null && reportButton != null)
            return;

        if (_runtimeBuilt && panelRoot != null)
            return;

        _runtimeBuilt = true;

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.transform.SetParent(transform, false);
        }

        var canvasGo = new GameObject("EmergencyDispatchCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        panelRoot = CreatePanel(canvasGo.transform, "DispatchPanel", new Vector2(560, 320));
        panelRoot.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.94f);

        titleText = CreateLabel(panelRoot.transform, "Title", "Emergency dispatch (101)", 30, new Vector2(0, 100), new Vector2(520, 50));
        statusText = CreateLabel(panelRoot.transform, "Status",
            "Press the button below to report the emergency to dispatch (101).",
            20, new Vector2(0, 35), new Vector2(500, 70));
        statusText.alignment = TextAlignmentOptions.Center;

        reportButton = CreateActionButton(panelRoot.transform, "ReportBtn", "Report to dispatch", new Vector2(0, -55));
        reportButton.GetComponent<Image>().color = new Color(0.75f, 0.22f, 0.18f);
        closeButton = CreateActionButton(panelRoot.transform, "CloseBtn", "Close", new Vector2(0, -125));
        closeButton.GetComponent<Image>().color = new Color(0.35f, 0.35f, 0.4f);

        canvasGo.transform.SetParent(transform, false);
        WireButtonCallbacks();
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float fontSize, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    private static Button CreateActionButton(Transform parent, string name, string label, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(460, 52);
        rt.anchoredPosition = pos;
        go.GetComponent<Image>().color = new Color(0.22f, 0.28f, 0.38f);

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;

        return go.GetComponent<Button>();
    }
}
