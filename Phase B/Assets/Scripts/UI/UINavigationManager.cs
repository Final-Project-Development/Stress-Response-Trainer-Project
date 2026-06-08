using TMPro;
using UnityEngine;

/// <summary>
/// Global overlay controls for always-available Pause / Back / Help buttons.
/// Works alongside TrainingFlowController phases.
/// </summary>
public class UINavigationManager : MonoBehaviour
{
    [Header("Flow")]
    public TrainingFlowController flow;
    public GameManager gameManager;
    public SimpleFPSController playerController;

    [Header("Panels")]
    public GameObject topBar;
    public GameObject helpPanel;
    public GameObject confirmBackPanel;

    [Header("Help")]
    public TextMeshProUGUI helpBodyText;
    [TextArea] public string helpDefault =
        "Use Pause to stop safely.\nUse Back to return to hub.\nUse Help anytime for current task instructions.";
    [TextArea] public string helpSimulation1 =
        "Simulation 1:\n1) Enter the home and collect 5 items (E): water bottle, flash light, radio, compass, map.\n2) Turn off the lights — PFB_Lightswitch (1).\n3) Close the door — PFB_DoorDouble.\n4) Run to the Mamad outside.";
    [TextArea] public string helpSimulation2 =
        "Simulation 2:\n1) First aid kit — E\n2) Wounded — E (go call for help)\n3) Phone — E door once, E coin, E Receiver, dial 1, 0, 1\n4) Wounded — E, then 1, 2, 3 treatment";
    [TextArea] public string helpEnvironmentLearning =
        "Environment Learning:\nWalk around and read item names in the world.\n" +
        "Use the left sidebar list — click a name to jump to that location.\n" +
        "Move the mouse over the left panel to click buttons.\n" +
        "Back or Esc — return to simulation selection.";

    [Header("Keys")]
    public KeyCode pauseKey = KeyCode.Escape;
    public KeyCode helpKey = KeyCode.H;

    private bool _helpOpen;

    public bool IsHelpOpen => _helpOpen;

    public bool IsOverlayUiOpen =>
        _helpOpen
        || (confirmBackPanel != null && confirmBackPanel.activeSelf)
        || (flow != null && flow.IsPaused);

    void Start()
    {
        if (flow == null)
            flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

        SetActiveSafe(topBar, true);
        SetActiveSafe(helpPanel, false);
        SetActiveSafe(confirmBackPanel, false);
        ApplyPlayerCursorMode();
    }

    void Update()
    {
        if (Input.GetKeyDown(helpKey))
            ToggleHelp();
    }

    /// <summary>
    /// Returns true when ESC should only dismiss Help / back-confirm (not pause).
    /// Uses the tracked Help flag only — not raw panel active state (Settings may share the panel).
    /// </summary>
    public bool ConsumeEscapeForOverlay()
    {
        if (_helpOpen)
        {
            CloseHelp();
            return true;
        }

        if (confirmBackPanel != null && confirmBackPanel.activeSelf)
        {
            ConfirmBackNo();
            return true;
        }

        return false;
    }

    void LateUpdate()
    {
        ApplyPlayerCursorMode();
    }

    public void TogglePause()
    {
        if (flow == null)
            return;

        flow.UI_SetPause(!flow.IsPaused);
        ApplyPlayerCursorMode();
    }

    public void ResumeFromPause()
    {
        if (flow == null)
            return;

        flow.UI_Resume();
        ApplyPlayerCursorMode();
    }

    public void ToggleHelp()
    {
        _helpOpen = !_helpOpen;
        SetActiveSafe(helpPanel, _helpOpen);
        if (_helpOpen)
            RefreshHelpText();
        ApplyPlayerCursorMode();
    }

    public void CloseHelp()
    {
        _helpOpen = false;
        SetActiveSafe(helpPanel, false);
        ApplyPlayerCursorMode();
    }

    public void GoBack()
    {
        if (flow == null)
            return;

        if (RequiresBackConfirmation())
        {
            SetActiveSafe(confirmBackPanel, true);
            if (flow.IsPaused)
                flow.UI_SetPause(false);
            ApplyPlayerCursorMode();
            return;
        }

        ReturnToHub();
    }

    public void ConfirmBackYes()
    {
        SetActiveSafe(confirmBackPanel, false);
        ReturnToHub();
    }

    public void ConfirmBackNo()
    {
        SetActiveSafe(confirmBackPanel, false);
        ApplyPlayerCursorMode();
    }

    public void ExitApplication()
    {
        if (flow != null)
            flow.UI_QuitApplication();
    }

    private void ReturnToHub()
    {
        if (flow == null)
            return;

        if (flow.CurrentPhase == TrainingFlowController.Phase.EnvironmentLearningBriefing)
        {
            _helpOpen = false;
            SetActiveSafe(helpPanel, false);
            SetActiveSafe(confirmBackPanel, false);
            flow.UI_CancelLearnBriefing();
            ApplyPlayerCursorMode();
            return;
        }

        if (flow.CurrentPhase == TrainingFlowController.Phase.EnvironmentLearning)
        {
            _helpOpen = false;
            SetActiveSafe(helpPanel, false);
            SetActiveSafe(confirmBackPanel, false);
            flow.UI_EndEnvironmentLearning();
            ApplyPlayerCursorMode();
            return;
        }

        _helpOpen = false;
        SetActiveSafe(helpPanel, false);
        SetActiveSafe(confirmBackPanel, false);
        flow.UI_SetPause(false);
        flow.UI_BackToHub();
        ApplyPlayerCursorMode();
    }

    private bool RequiresBackConfirmation()
    {
        if (flow == null)
            return false;

        var p = flow.CurrentPhase;
        return p == TrainingFlowController.Phase.Simulation1Active
            || p == TrainingFlowController.Phase.Simulation2Active;
    }

    private void RefreshHelpText()
    {
        if (helpBodyText == null)
            return;

        string baseHelp = GetBaseHelpForPhase();
        string currentTask = GetCurrentTaskLine();
        if (!string.IsNullOrWhiteSpace(currentTask))
            helpBodyText.text = baseHelp + "\n\n— Current task —\n" + currentTask;
        else
            helpBodyText.text = baseHelp;
    }

    private string GetBaseHelpForPhase()
    {
        if (flow == null)
            return helpDefault;

        switch (flow.CurrentPhase)
        {
            case TrainingFlowController.Phase.SimulationPick:
                return helpDefault;

            case TrainingFlowController.Phase.EnvironmentLearningBriefing:
            case TrainingFlowController.Phase.EnvironmentLearning:
                return helpEnvironmentLearning;

            case TrainingFlowController.Phase.Simulation1Calibration:
            case TrainingFlowController.Phase.Simulation1MissionBriefing:
            case TrainingFlowController.Phase.Simulation1Active:
            case TrainingFlowController.Phase.Simulation1Results:
                return helpSimulation1;

            case TrainingFlowController.Phase.Simulation2Briefing:
            case TrainingFlowController.Phase.Simulation2Active:
            case TrainingFlowController.Phase.Simulation2Results:
                return helpSimulation2;

            default:
                return helpDefault;
        }
    }

    private string GetCurrentTaskLine()
    {
        if (flow == null)
            return null;

        var phase = flow.CurrentPhase;
        if (phase != TrainingFlowController.Phase.Simulation1Active
            && phase != TrainingFlowController.Phase.Simulation2Active)
            return null;

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

        return gameManager != null ? gameManager.GetCurrentObjectiveText() : null;
    }

    public void ApplyPlayerCursorMode()
    {
        SetActiveSafe(topBar, true);

        if (playerController == null)
            return;

        bool activeSimulation = flow != null
            && (flow.CurrentPhase == TrainingFlowController.Phase.Simulation1Active
                || flow.CurrentPhase == TrainingFlowController.Phase.Simulation2Active);

        bool environmentLearning = flow != null
            && flow.CurrentPhase == TrainingFlowController.Phase.EnvironmentLearning;

        if (environmentLearning)
        {
            playerController.SetOverlayUiOpen(false);
            playerController.SetUiMenuMode(false);
            playerController.SetSimulationToolbarMode(false);
            if (topBar != null)
                playerController.SetToolbarScreenRegion(topBar.GetComponent<RectTransform>());
            return;
        }

        if (IsOverlayUiOpen)
        {
            playerController.SetOverlayUiOpen(true);
            playerController.SetUiMenuMode(false);
            return;
        }

        playerController.SetOverlayUiOpen(false);
        playerController.SetUiMenuMode(!activeSimulation);
        playerController.SetSimulationToolbarMode(activeSimulation);

        if (topBar != null)
            playerController.SetToolbarScreenRegion(topBar.GetComponent<RectTransform>());

        WireMissionStatusPanelRegion();
    }

    void WireMissionStatusPanelRegion()
    {
        if (playerController == null)
            return;

        MissionStatusPanelController panel = flow != null ? flow.missionStatusPanel : null;
        if (panel == null)
            panel = FindFirstObjectByType<MissionStatusPanelController>(FindObjectsInactive.Include);

        var region = panel != null ? panel.GetPanelScreenRegion() : null;
        if (region != null)
            playerController.SetMissionStatusPanelRegion(region);
    }

    private static void SetActiveSafe(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on)
            go.SetActive(on);
    }
}
