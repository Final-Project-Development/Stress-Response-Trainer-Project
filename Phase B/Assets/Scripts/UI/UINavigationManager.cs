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
        "Simulation 1:\n1) Collect all required items.\n2) After collecting, run to the Mamad outside.\n3) Complete the objective to view results.";
    [TextArea] public string helpSimulation2 =
        "Simulation 2:\n1) Approach the wounded person.\n2) Start treatment and follow step order.\n3) Complete treatment to view results.";

    [Header("Keys")]
    public KeyCode pauseKey = KeyCode.Escape;

    private bool _helpOpen;

    void Start()
    {
        SetActiveSafe(topBar, true);
        SetActiveSafe(helpPanel, false);
        SetActiveSafe(confirmBackPanel, false);
        ApplyInteractionMode();
    }

    void Update()
    {
        if (Input.GetKeyDown(pauseKey))
            TogglePause();
    }

    public void TogglePause()
    {
        if (flow == null)
            return;

        flow.UI_SetPause(!flow.IsPaused);
        ApplyInteractionMode();
    }

    public void ResumeFromPause()
    {
        if (flow == null)
            return;

        flow.UI_Resume();
        ApplyInteractionMode();
    }

    public void ToggleHelp()
    {
        _helpOpen = !_helpOpen;
        SetActiveSafe(helpPanel, _helpOpen);
        if (_helpOpen)
            RefreshHelpText();
        ApplyInteractionMode();
    }

    public void CloseHelp()
    {
        _helpOpen = false;
        SetActiveSafe(helpPanel, false);
        ApplyInteractionMode();
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
            ApplyInteractionMode();
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
        ApplyInteractionMode();
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

        _helpOpen = false;
        SetActiveSafe(helpPanel, false);
        SetActiveSafe(confirmBackPanel, false);
        flow.UI_SetPause(false);
        flow.UI_BackToHub();
        ApplyInteractionMode();
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
        if (helpBodyText == null || flow == null)
            return;

        switch (flow.CurrentPhase)
        {
            case TrainingFlowController.Phase.Simulation1Calibration:
            case TrainingFlowController.Phase.Simulation1MissionBriefing:
            case TrainingFlowController.Phase.Simulation1Active:
            case TrainingFlowController.Phase.Simulation1Results:
                helpBodyText.text = helpSimulation1;
                break;

            case TrainingFlowController.Phase.Simulation2Briefing:
            case TrainingFlowController.Phase.Simulation2Active:
            case TrainingFlowController.Phase.Simulation2Results:
                helpBodyText.text = helpSimulation2;
                break;

            default:
                helpBodyText.text = helpDefault;
                break;
        }
    }

    private void ApplyInteractionMode()
    {
        if (playerController == null)
            return;

        bool menuOpen = _helpOpen || (confirmBackPanel != null && confirmBackPanel.activeSelf) || (flow != null && flow.IsPaused);
        if (menuOpen)
        {
            playerController.SetUiMenuMode(true);
            return;
        }

        bool activeSimulation = flow != null
            && (flow.CurrentPhase == TrainingFlowController.Phase.Simulation1Active
                || flow.CurrentPhase == TrainingFlowController.Phase.Simulation2Active);
        playerController.SetUiMenuMode(!activeSimulation);
    }

    private static void SetActiveSafe(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on)
            go.SetActive(on);
    }
}
