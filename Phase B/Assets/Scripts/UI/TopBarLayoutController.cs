using TMPro;
using UnityEngine;

/// <summary>
/// Simple runtime layout for top bar elements to avoid overlap.
/// Places progress container on the left and main buttons on the top-right.
/// </summary>
public class TopBarLayoutController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform topBarRoot;
    [SerializeField] private RectTransform leftContainer;       // Experience_simulation_Container
    [SerializeField] private RectTransform pauseButton;
    [SerializeField] private RectTransform backButton;
    [SerializeField] private RectTransform helpButton;
    [SerializeField] private RectTransform profileButton;
    [SerializeField] private RectTransform watchStatusContainer;
    [SerializeField] private RectTransform extraCenterObject;   // Optional legacy alias
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private TrainingFlowController flow;

    [Header("Layout")]
    [SerializeField] private float topY = -18f;
    [SerializeField] private float leftPadding = 20f;
    [SerializeField] private float rightPadding = 26f;
    [SerializeField] private float rightSpacing = 92f;
    [SerializeField] private float watchStatusGapFromButtons = 28f;
    [Tooltip("When enabled, BtnProfile keeps its RectTransform from the Editor.")]
    [SerializeField] private bool preserveManualProfileLayout = true;
    [Tooltip("When enabled, status_watch keeps its RectTransform from the Editor.")]
    [SerializeField] private bool preserveManualWatchStatusLayout = true;
    [SerializeField] private bool applyInStart = true;
    [SerializeField] private bool applyInLateUpdateOnce = true;

    public bool PreserveManualProfileLayout => preserveManualProfileLayout;
    public bool PreserveManualWatchStatusLayout => preserveManualWatchStatusLayout;

    bool _appliedLate;
    string _lastDisplayedName;

    private void Awake()
    {
        if (flow == null)
            flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        ResolveWatchStatusContainer();
        ResolveCharacterNameText();
    }

    private void Start()
    {
        if (applyInStart)
            ApplyLayout();
    }

    private void LateUpdate()
    {
        if (!applyInLateUpdateOnce || _appliedLate)
            return;

        _appliedLate = true;
        ApplyLayout();
    }

    private void Update()
    {
        RefreshCharacterName();
    }

    [ContextMenu("Apply TopBar Layout")]
    public void ApplyLayout()
    {
        if (topBarRoot == null)
            topBarRoot = GetComponent<RectTransform>();
        if (topBarRoot == null)
            return;

        ResolveWatchStatusContainer();

        if (leftContainer == null)
        {
            Transform left = topBarRoot.Find("Experience_simulation_Container");
            if (left != null)
                leftContainer = left as RectTransform;
        }

        if (leftContainer != null)
        {
            leftContainer.anchorMin = new Vector2(0f, 1f);
            leftContainer.anchorMax = new Vector2(0f, 1f);
            leftContainer.pivot = new Vector2(0f, 1f);
            leftContainer.anchoredPosition = new Vector2(leftPadding, topY);
        }

        // Right row: Help (rightmost), Back, Pause, Profile — then watch status further left.
        PlaceRightButton(helpButton, 0f);
        PlaceRightButton(backButton, rightSpacing);
        PlaceRightButton(pauseButton, rightSpacing * 2f);
        if (!preserveManualProfileLayout)
            PlaceRightButton(profileButton, rightSpacing * 3f);
        if (!preserveManualWatchStatusLayout)
            PlaceWatchStatusLeftOfButtons();
    }

    void ResolveCharacterNameText()
    {
        if (characterNameText != null)
            return;

        if (topBarRoot == null)
            topBarRoot = GetComponent<RectTransform>();

        Transform found = topBarRoot != null ? topBarRoot.Find("Character_Name_Text") : null;
        if (found != null)
            characterNameText = found.GetComponent<TextMeshProUGUI>();
    }

    void RefreshCharacterName()
    {
        if (characterNameText == null)
            return;

        string email = LocalAuthStore.GetCurrentLoggedInEmail();
        bool loggedInWithPassword = !string.IsNullOrEmpty(email);
        bool hideOnGateOrLogin = flow != null
            && (flow.CurrentPhase == TrainingFlowController.Phase.Gate
                || flow.CurrentPhase == TrainingFlowController.Phase.Login);

        bool visible = loggedInWithPassword && !hideOnGateOrLogin;
        if (characterNameText.gameObject.activeSelf != visible)
            characterNameText.gameObject.SetActive(visible);

        if (!visible)
        {
            _lastDisplayedName = null;
            return;
        }

        if (email == _lastDisplayedName)
            return;

        _lastDisplayedName = email;
        characterNameText.text = email;
    }

    void ResolveWatchStatusContainer()
    {
        if (watchStatusContainer != null || extraCenterObject != null)
            return;

        if (topBarRoot == null)
            topBarRoot = GetComponent<RectTransform>();

        Transform found = topBarRoot != null ? topBarRoot.Find("status_watch") : null;
        if (found != null)
            watchStatusContainer = found as RectTransform;
    }

    void PlaceRightButton(RectTransform rt, float offsetFromRight)
    {
        if (rt == null)
            return;

        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-(rightPadding + offsetFromRight), topY);
    }

    void PlaceWatchStatusLeftOfButtons()
    {
        RectTransform watchSlot = watchStatusContainer != null ? watchStatusContainer : extraCenterObject;
        if (watchSlot == null)
            return;

        // Anchor on the right; pivot on the right edge so the pill grows left, away from buttons.
        // Keep the watch pill left of the rightmost toolbar button.
        float rightmostButtonOffset = profileButton != null
            ? rightSpacing * 3f
            : rightSpacing * 2f;
        float offsetFromRight = rightmostButtonOffset + rightSpacing + watchStatusGapFromButtons;

        watchSlot.anchorMin = new Vector2(1f, 1f);
        watchSlot.anchorMax = new Vector2(1f, 1f);
        watchSlot.pivot = new Vector2(1f, 0.5f);
        watchSlot.anchoredPosition = new Vector2(-(rightPadding + offsetFromRight), topY);
    }
}
