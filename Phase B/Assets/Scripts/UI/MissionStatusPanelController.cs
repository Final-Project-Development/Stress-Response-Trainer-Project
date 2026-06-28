using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Persistent mission HUD during Simulation 1 and 2 (completed step + next objective + hint button).
/// Design the panel manually on Canvas; assign TMP fields in the Inspector.
/// </summary>
public class MissionStatusPanelController : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Outermost MissionStatus_Panel root — shown/hidden during Sim 1 & 2.")]
    public GameObject panelRoot;

    [Tooltip("Screen area that unlocks the mouse for Hint clicks (usually the Info child).")]
    public RectTransform panelScreenRegion;

    [Header("Text")]
    [Tooltip("Optional static title, e.g. Simulation 1 / Simulation 2.")]
    public TextMeshProUGUI missionTitleText;

    [Tooltip("Last completed action, e.g. \"Radio collected.\"")]
    public TextMeshProUGUI completedText;

    [Tooltip("Current objective / next step.")]
    public TextMeshProUGUI objectiveText;

    [Header("Hint")]
    public Button hintButton;
    public TextMeshProUGUI hintButtonLabel;
    public MissionHintService hintService;

    [Header("Copy (English)")]
    public string simulation1Title = "Simulation 1";
    public string simulation2Title = "Simulation 2";
    public string hintButtonDefaultLabel = "Hint";
    public string noHintAvailableMessage = "No hint available for this step.";

    GameManager _gameManager;
    TrainingFlowController _flow;
    bool _lastHintActive;

    public bool IsConfigured =>
        panelRoot != null && completedText != null && objectiveText != null;

    void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (hintService == null)
            hintService = GetComponent<MissionHintService>();

        if (hintService == null)
            hintService = gameObject.AddComponent<MissionHintService>();

        if (hintButton != null)
            hintButton.onClick.AddListener(OnHintButtonClicked);

        ResolveMissionTitleReference();
        ConfigureNonBlockingText(missionTitleText);
        ConfigureNonBlockingText(completedText);
        ConfigureNonBlockingText(objectiveText);
        if (hintButtonLabel != null)
            hintButtonLabel.raycastTarget = false;
    }

    void Start()
    {
        _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        _flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        if (hintButtonLabel != null)
            hintButtonLabel.text = hintButtonDefaultLabel;

        WirePlayerCursorRegion();
        SetPanelVisible(false);
        ClearLines();
    }

    void Update()
    {
        bool active = hintService != null && hintService.IsHintActive;
        if (active == _lastHintActive)
            return;

        _lastHintActive = active;
        UpdateHintButtonLabel();
    }

    public RectTransform GetPanelScreenRegion()
    {
        if (panelScreenRegion != null)
            return panelScreenRegion;

        if (panelRoot != null)
        {
            var info = panelRoot.transform.Find("Info");
            if (info != null)
                panelScreenRegion = info.GetComponent<RectTransform>();
            if (panelScreenRegion == null)
                panelScreenRegion = panelRoot.GetComponent<RectTransform>();
        }

        return panelScreenRegion;
    }

    void WirePlayerCursorRegion()
    {
        var region = GetPanelScreenRegion();
        if (region == null)
            return;

        var fps = FindFirstObjectByType<SimpleFPSController>(FindObjectsInactive.Include);
        fps?.SetMissionStatusPanelRegion(region);
    }

    void OnDestroy()
    {
        if (hintButton != null)
            hintButton.onClick.RemoveListener(OnHintButtonClicked);
    }

    public void SetPanelVisible(bool visible)
    {
        if (panelRoot != null)
            panelRoot.SetActive(visible);

        if (!visible)
            hintService?.HideActiveHint();
    }

    public void SetMissionTitle(string title)
    {
        ResolveMissionTitleReference();
        if (missionTitleText == null || string.IsNullOrWhiteSpace(title))
            return;

        missionTitleText.text = title;
    }

    public void SetSimulation1Title() => SetMissionTitle(simulation1Title);

    public void SetSimulation2Title() => SetMissionTitle(simulation2Title);

    void ResolveMissionTitleReference()
    {
        if (missionTitleText != null)
            return;

        var tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < tmps.Length; i++)
        {
            if (tmps[i] != null && tmps[i].name.Trim().StartsWith("MissionTitle", System.StringComparison.OrdinalIgnoreCase))
            {
                missionTitleText = tmps[i];
                return;
            }
        }
    }

    public void ClearLines()
    {
        SetCompletedLine(string.Empty);
        SetObjectiveLine(string.Empty);
    }

    public void SetCompletedLine(string text)
    {
        if (completedText == null)
            return;

        completedText.text = string.IsNullOrWhiteSpace(text) ? string.Empty : VrInputPrompts.Localize(text);
    }

    public void SetObjectiveLine(string text)
    {
        if (objectiveText == null)
            return;

        objectiveText.text = string.IsNullOrWhiteSpace(text) ? string.Empty : VrInputPrompts.Localize(text);
    }

    public void OnHintButtonClicked()
    {
        // Toggle: if a hint is already showing, the button closes it.
        if (hintService != null && hintService.IsHintActive)
        {
            hintService.HideActiveHint();
            UpdateHintButtonLabel();
            return;
        }

        if (_gameManager == null)
            _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

        if (_flow == null)
            _flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        if (TryShowSimulation1PickupHints())
        {
            UpdateHintButtonLabel();
            return;
        }

        IReadOnlyList<string> targets = MissionHintResolver.ResolveCurrentHintObjectNames(_gameManager, _flow);
        if (targets == null || targets.Count == 0)
        {
            if (_gameManager != null)
                _gameManager.ShowTransientMissionNote(noHintAvailableMessage, 3f);
            return;
        }

        hintService?.ShowHintsForObjects(targets);
        UpdateHintButtonLabel();
    }

    void UpdateHintButtonLabel()
    {
        if (hintButtonLabel == null)
            return;

        bool active = hintService != null && hintService.IsHintActive;
        hintButtonLabel.text = active ? "Hide Hint" : hintButtonDefaultLabel;
    }

    bool TryShowSimulation1PickupHints()
    {
        if (_flow == null || _gameManager == null || hintService == null)
            return false;

        if (_flow.CurrentPhase != TrainingFlowController.Phase.Simulation1Active)
            return false;

        if (_gameManager.GetSim1Phase() != GameManager.Sim1MissionPhase.CollectItems)
            return false;

        var bootstrap = _gameManager.GetMissionBootstrap();
        if (bootstrap == null)
            return false;

        var pickups = bootstrap.GetRemainingSimulation1Pickups();
        if (pickups == null || pickups.Length == 0)
            return false;

        hintService.ShowHintsForPickups(pickups);
        return true;
    }

    static void ConfigureNonBlockingText(TextMeshProUGUI tmp)
    {
        if (tmp != null)
            tmp.raycastTarget = false;
    }
}

/// <summary>Maps current mission state to scene object names (WorldItemLabel hosts).</summary>
public static class MissionHintResolver
{
    public static IReadOnlyList<string> ResolveCurrentHintObjectNames(
        GameManager gameManager,
        TrainingFlowController flow)
    {
        if (gameManager == null || flow == null)
            return null;

        if (flow.CurrentPhase == TrainingFlowController.Phase.Simulation1Active)
        {
            switch (gameManager.GetSim1Phase())
            {
                case GameManager.Sim1MissionPhase.CollectItems:
                {
                    var bootstrap = gameManager.GetComponent<SimulationMissionBootstrap>();
                    return bootstrap != null
                        ? bootstrap.GetRemainingSim1PickupObjectNames()
                        : new[] { "Home" };
                }
                case GameManager.Sim1MissionPhase.TurnOffLights:
                    return new[] { "PFB_Lightswitch (1)" };
                case GameManager.Sim1MissionPhase.CloseDoor:
                    return new[] { "PFB_DoorDouble" };
                case GameManager.Sim1MissionPhase.RunToShelter:
                    return new[] { "mamad" };
            }
        }

        if (flow.CurrentPhase == TrainingFlowController.Phase.Simulation2Active)
        {
            if (!gameManager.HasFirstAidKit())
                return new[] { "firstaid" };
            if (!gameManager.HasContactedCasualty())
                return new[] { "WoundedCharacter_TPose" };
            if (!gameManager.HasReportedEmergency())
                return new[] { "PhoneBox" };
            if (!gameManager.IsSim2TreatmentComplete())
                return new[] { "WoundedCharacter_TPose" };
        }

        return null;
    }
}
