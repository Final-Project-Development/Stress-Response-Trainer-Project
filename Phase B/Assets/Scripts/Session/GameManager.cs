using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public enum Sim1MissionPhase
    {
        CollectItems,
        TurnOffLights,
        CloseDoor,
        RunToShelter
    }

    public int itemToCollect = 5;
    [Tooltip("Legacy full-screen objective. Prefer Mission Status Panel.")]
    public TextMeshProUGUI objectiveText;
    [Tooltip("Legacy center popup. Prefer Mission Status Panel → Completed Text.")]
    public TextMeshProUGUI pickupFeedbackText;
    [Tooltip("Persistent mission panel (Sim 1 & 2) — completed line + next objective + hint.")]
    public MissionStatusPanelController missionStatusPanel;
    public float pickupFeedbackDuration = 1.4f;

    [Header("Shelter target (Simulation 1)")]
    [Tooltip("New shelter root object name in the scene.")]
    public string mamadObjectName = "mamad";
    [Tooltip("Legacy shelter object name to disable during play so only mamad is used.")]
    public string legacyOutdoorShelterObjectName = "OutdoorShelter";
    public bool disableLegacyOutdoorShelter = true;

    [Header("Voice (optional)")]
    [Tooltip("e.g. same Narration Audio Source as TrainingFlowController, or a dedicated UI voice source.")]
    public AudioSource voiceAudioSource;
    [Tooltip("Played once when all pre-shelter steps are complete — run to Mamad instruction.")]
    public AudioClip allItemsCollectedRunToMamadClip;

    /// <summary>Raised once when Simulation 1 is fully complete (items + lights + door + shelter).</summary>
    public event Action OnAllItemsCollected;
    /// <summary>Raised when item collection goal is complete.</summary>
    public event Action OnItemsCollectionComplete;

    /// <summary>Raised when <see cref="OnFirstAidFinished"/> completes the first-aid interaction.</summary>
    public event Action OnFirstAidComplete;

    private int itemCollected = 0;
    private bool firstAidDone = false;
    private bool _itemsCollectionComplete;
    private bool _lightsTurnedOff;
    private bool _exitDoorClosed;
    private bool _shelterReached;
    private bool _allItemsCollectedRaised;
    private bool _firstAidKitCollected;
    private bool _casualtyContacted;
    private bool _emergencyReported;
    private Sim1MissionPhase _sim1Phase = Sim1MissionPhase.CollectItems;
    private Coroutine _pickupFeedbackRoutine;
    private SimulationMissionBootstrap _missionBootstrap;
    private TrainingFlowController _flow;
    private Door _missionExitDoor;
    private const float Sim2HintDuration = 8f;
    private const string Sim2GoToPhoneObjective =
        "Go to the public telephone and open the door.";

    void Awake()
    {
        _missionBootstrap = GetComponent<SimulationMissionBootstrap>();
        if (_missionBootstrap == null)
            _missionBootstrap = gameObject.AddComponent<SimulationMissionBootstrap>();
    }

    void Start()
    {
        _flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
        if (missionStatusPanel == null)
            missionStatusPanel = FindFirstObjectByType<MissionStatusPanelController>(FindObjectsInactive.Include);
        ConfigureShelterTargets();
        ConfigureNonBlockingMissionHud();
        _allItemsCollectedRaised = false;
        if (pickupFeedbackText != null)
            pickupFeedbackText.gameObject.SetActive(false);
        UpdateObjectiveText();
    }

    void Update()
    {
        RefreshProximityObjectiveIfNeeded();
    }

    void RefreshProximityObjectiveIfNeeded()
    {
        if (!UsesMissionStatusPanel())
            return;

        if (_flow == null)
            _flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        if (_flow == null || !_flow.AllowsMissionGameplay)
            return;

        var phase = _flow.CurrentPhase;
        if (phase != TrainingFlowController.Phase.Simulation1Active
            && phase != TrainingFlowController.Phase.Simulation2Active)
            return;

        string objective = MissionProximityObjectiveResolver.Resolve(this, _flow, Camera.main);
        if (!string.IsNullOrWhiteSpace(objective))
            PushObjectiveToMissionPanel(objective);
    }

    /// <summary>Mission hint text must not steal mouse input (full-screen TMP raycasts block look).</summary>
    private void ConfigureNonBlockingMissionHud()
    {
        if (pickupFeedbackText != null)
            pickupFeedbackText.raycastTarget = false;

        if (objectiveText != null)
            objectiveText.raycastTarget = false;
    }

    public void ApplyEnvironmentLearningDoorLayout()
    {
        _missionBootstrap?.ApplyEnvironmentLearningDoorLayout();
    }

    public void RestoreExitDoorAfterEnvironmentLearning()
    {
        _missionBootstrap?.RestoreExitDoorAfterEnvironmentLearning();
    }

    public void PrepareSimulation1Mission()
    {
        if (_flow == null)
            _flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        ResetSimulation1State();
        _missionBootstrap?.PrepareSimulation1();
        if (UsesMissionStatusPanel())
        {
            missionStatusPanel.ClearLines();
            missionStatusPanel.SetSimulation1Title();
        }
        UpdateObjectiveText();
    }

    public void PrepareSimulation2Mission()
    {
        if (_flow == null)
            _flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        firstAidDone = false;
        _firstAidKitCollected = false;
        _casualtyContacted = false;
        _emergencyReported = false;
        _missionBootstrap?.PrepareSimulation2();
        ResetWoundedTreatment();
        if (UsesMissionStatusPanel())
        {
            missionStatusPanel.ClearLines();
            missionStatusPanel.SetSimulation2Title();
        }
        UpdateSimulation2ObjectiveText();
    }

    private void ResetWoundedTreatment()
    {
        var wounded = FindFirstObjectByType<WoundedMan>(FindObjectsInactive.Include);
        if (wounded != null)
            wounded.ResetTreatment();
    }

    public bool CanTurnOffLights() => _sim1Phase == Sim1MissionPhase.TurnOffLights;

    public Sim1MissionPhase GetSim1Phase() => _sim1Phase;

    public bool IsExitDoorClosed() => IsMissionExitDoorClosed();

    public void RegisterMissionExitDoor(Door door)
    {
        _missionExitDoor = door;
        SyncExitDoorState(door != null && !door.IsOpen);
    }

    public void SyncExitDoorState(bool isClosed)
    {
        _exitDoorClosed = isClosed;

        if (isClosed)
            TryAdvanceAfterExitDoorClosed();
    }

    private void TryAdvanceAfterExitDoorClosed()
    {
        if (_sim1Phase != Sim1MissionPhase.CloseDoor)
            return;

        _sim1Phase = Sim1MissionPhase.RunToShelter;
        PlayAllItemsCollectedVoice();
        if (UsesMissionStatusPanel())
        {
            SetMissionCompletedLine(
                _flow != null ? _flow.sim1DoorClosedCompleted : "Door closed.");
            UpdateObjectiveText();
            return;
        }

        ShowLegacyMissionHintIfNoPanel(
            _flow != null ? _flow.sim1ObjectiveRunToShelter : "Run to the Mamad shelter outside.",
            6f);
        UpdateObjectiveText();
    }

    public bool IsMissionExitDoorClosed()
    {
        if (_missionExitDoor != null)
            return !_missionExitDoor.IsOpen;

        return _exitDoorClosed;
    }

    public string GetSim1RunToShelterHint()
    {
        if (_flow == null)
            _flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        return _flow != null
            ? _flow.sim1ObjectiveRunToShelter
            : "Run to the Mamad (shelter) outside — the entrance door must be closed when you arrive.";
    }

    public string GetShelterDoorOpenHint()
    {
        if (_flow == null)
            _flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        return _flow != null
            ? _flow.sim1ShelterDoorOpenHint
            : "Close the entrance door before entering the Mamad.";
    }

    public bool HasFirstAidKit() => _firstAidKitCollected;

    public bool HasContactedCasualty() => _casualtyContacted;

    public bool HasReportedEmergency() => _emergencyReported;

    public bool IsSim2TreatmentComplete() => firstAidDone;

    public int GetSim1ItemsCollected() => itemCollected;

    public Door GetMissionExitDoor() => _missionExitDoor;

    public bool HasMissionStatusPanel() => UsesMissionStatusPanel();

    public void RefreshSimulation2MissionObjective() => UpdateSimulation2ObjectiveText();

    public void SetMissionCompletedLine(string text)
    {
        if (!UsesMissionStatusPanel())
            return;

        CancelMissionPanelTransientNote();
        missionStatusPanel.SetCompletedLine(text);
    }

    public void SetMissionObjectiveLine(string text)
    {
        if (!UsesMissionStatusPanel())
            return;

        missionStatusPanel.SetObjectiveLine(text);
    }

    void CancelMissionPanelTransientNote()
    {
        if (_pickupFeedbackRoutine != null)
        {
            StopCoroutine(_pickupFeedbackRoutine);
            _pickupFeedbackRoutine = null;
        }
    }

    private bool UsesMissionStatusPanel() =>
        missionStatusPanel != null && missionStatusPanel.IsConfigured;

    /// <summary>Current in-mission objective line for Help overlay and HUD.</summary>
    public string GetCurrentObjectiveText()
    {
        if (UsesMissionStatusPanel() &&
            missionStatusPanel.objectiveText != null &&
            !string.IsNullOrWhiteSpace(missionStatusPanel.objectiveText.text))
            return missionStatusPanel.objectiveText.text;

        if (objectiveText != null && !string.IsNullOrWhiteSpace(objectiveText.text))
            return objectiveText.text;

        if (_flow == null)
            _flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        if (_flow == null)
            return null;

        var phase = _flow.CurrentPhase;
        if (phase == TrainingFlowController.Phase.Simulation1Active)
        {
            if (UsesMissionStatusPanel())
                return MissionProximityObjectiveResolver.Resolve(this, _flow, Camera.main);

            return BuildLegacySim1ObjectiveText();
        }

        if (phase == TrainingFlowController.Phase.Simulation2Active)
        {
            if (UsesMissionStatusPanel())
                return MissionProximityObjectiveResolver.Resolve(this, _flow, Camera.main);

            return BuildLegacySim2ObjectiveText();
        }

        return null;
    }

    public void OnLightsTurnedOff()
    {
        if (_sim1Phase != Sim1MissionPhase.TurnOffLights)
            return;

        _lightsTurnedOff = true;
        _sim1Phase = Sim1MissionPhase.CloseDoor;
        if (UsesMissionStatusPanel())
        {
            SetMissionCompletedLine(
                _flow != null ? _flow.sim1LightsOffCompleted : "Lights turned off.");
            UpdateObjectiveText();
        }
        else
        {
            UpdateObjectiveText();
        }

        if (IsMissionExitDoorClosed())
            TryAdvanceAfterExitDoorClosed();
    }

    public void OnFirstAidKitCollected(string itemName)
    {
        if (_firstAidKitCollected)
            return;

        _firstAidKitCollected = true;
        _missionBootstrap?.RevealWounded();
        ShowPickupFeedback(itemName);
        UpdateSimulation2ObjectiveText();
    }

    /// <summary>Player pressed E on the casualty before calling dispatch.</summary>
    public void OnCasualtyApproached()
    {
        if (_casualtyContacted)
            return;

        _casualtyContacted = true;
        if (UsesMissionStatusPanel())
        {
            SetMissionCompletedLine(
                _flow != null ? _flow.sim2CasualtyContactedCompleted : "Wounded person found.");
            UpdateSimulation2ObjectiveText();
            return;
        }

        ShowMissionMessage(Sim2GoToPhoneObjective, Sim2HintDuration);
        UpdateSimulation2ObjectiveText();
    }

    public void OnEmergencyReported()
    {
        if (_emergencyReported)
            return;

        _emergencyReported = true;
        if (UsesMissionStatusPanel())
        {
            SetMissionCompletedLine(
                _flow != null ? _flow.sim2EmergencyReportedCompleted : "Emergency call placed.");
        }

        UpdateSimulation2ObjectiveText();
    }

    private void ResetSimulation1State()
    {
        itemCollected = 0;
        _itemsCollectionComplete = false;
        _lightsTurnedOff = false;
        _exitDoorClosed = false;
        _shelterReached = false;
        _allItemsCollectedRaised = false;
        _sim1Phase = Sim1MissionPhase.CollectItems;
        _missionExitDoor = null;
    }

    private void ConfigureShelterTargets()
    {
        var mamad = FindSceneObjectByName(mamadObjectName);
        if (mamad == null)
        {
            Debug.LogWarning($"GameManager: Could not find mamad object '{mamadObjectName}'. Shelter objective trigger may not fire.");
            return;
        }

        if (mamad.GetComponent<ShelterTrigger>() == null)
        {
            mamad.AddComponent<ShelterTrigger>();
            Debug.Log($"GameManager: Added ShelterTrigger to '{mamadObjectName}'.");
        }

        if (!disableLegacyOutdoorShelter)
            return;

        var legacy = FindSceneObjectByName(legacyOutdoorShelterObjectName);
        if (legacy != null && legacy != mamad)
        {
            legacy.SetActive(false);
            Debug.Log($"GameManager: Disabled legacy shelter '{legacyOutdoorShelterObjectName}'.");
        }
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            if (t != null && t.name == objectName)
                return t.gameObject;
        }

        return null;
    }

    public void AddItem()
    {
        AddItem("Item");
    }

    public void AddItem(string itemName)
    {
        if (_sim1Phase != Sim1MissionPhase.CollectItems || _itemsCollectionComplete)
            return;

        itemCollected++;

        ShowPickupFeedback(itemName);
        UpdateObjectiveText();

        if (itemCollected >= itemToCollect)
        {
            _itemsCollectionComplete = true;
            _sim1Phase = Sim1MissionPhase.TurnOffLights;
            if (UsesMissionStatusPanel())
            {
                SetMissionCompletedLine(
                    _flow != null ? _flow.sim1AllItemsCollectedCompleted : $"All {itemToCollect} supplies collected.");
                OnItemsCollectionComplete?.Invoke();
                UpdateObjectiveText();
            }
            else
            {
                OnItemsCollectionComplete?.Invoke();
                UpdateObjectiveText();
            }
        }
    }

    private void ShowPickupFeedback(string itemName)
    {
        string line = $"{itemName} collected.";
        if (UsesMissionStatusPanel())
        {
            SetMissionCompletedLine(line);
            return;
        }

        if (pickupFeedbackText == null)
            return;

        if (_pickupFeedbackRoutine != null)
            StopCoroutine(_pickupFeedbackRoutine);

        pickupFeedbackText.text = line;
        _pickupFeedbackRoutine = StartCoroutine(HidePickupFeedbackAfterDelay());
    }

    private void ShowFeedbackMessage(string message, float durationSeconds)
    {
        if (UsesMissionStatusPanel())
        {
            missionStatusPanel.SetObjectiveLine(message);
            return;
        }

        if (pickupFeedbackText == null)
            return;

        if (_pickupFeedbackRoutine != null)
            StopCoroutine(_pickupFeedbackRoutine);

        pickupFeedbackText.text = message;
        _pickupFeedbackRoutine = StartCoroutine(HideFeedbackAfterDelay(durationSeconds));
    }

    /// <summary>Short toast when mission panel has no hint target (does not replace objective line).</summary>
    public void ShowTransientMissionNote(string message, float durationSeconds = 3f)
    {
        if (!UsesMissionStatusPanel())
        {
            ShowLegacyMissionHintIfNoPanel(message, durationSeconds);
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
            return;

        CancelMissionPanelTransientNote();

        string previous = missionStatusPanel.completedText != null
            ? missionStatusPanel.completedText.text
            : string.Empty;
        missionStatusPanel.SetCompletedLine(message);
        _pickupFeedbackRoutine = StartCoroutine(RestoreCompletedLineAfterDelay(previous, durationSeconds));
    }

    private IEnumerator RestoreCompletedLineAfterDelay(string previous, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (UsesMissionStatusPanel())
            missionStatusPanel.SetCompletedLine(previous);
        _pickupFeedbackRoutine = null;
    }

    private IEnumerator HidePickupFeedbackAfterDelay()
    {
        pickupFeedbackText.raycastTarget = false;
        pickupFeedbackText.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(pickupFeedbackDuration);
        if (pickupFeedbackText != null)
            pickupFeedbackText.gameObject.SetActive(false);
        _pickupFeedbackRoutine = null;
    }

    private IEnumerator HideFeedbackAfterDelay(float delay)
    {
        pickupFeedbackText.raycastTarget = false;
        pickupFeedbackText.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(delay);
        if (pickupFeedbackText != null)
            pickupFeedbackText.gameObject.SetActive(false);
        _pickupFeedbackRoutine = null;
    }

    /// <summary>Updates mission panel completed + objective lines (phone booth steps, etc.).</summary>
    public void SetMissionPanelProgress(string completedLine, string objectiveLine)
    {
        if (!UsesMissionStatusPanel())
        {
            string message = !string.IsNullOrWhiteSpace(objectiveLine)
                ? objectiveLine
                : completedLine;
            if (!string.IsNullOrWhiteSpace(message))
                ShowMissionMessage(message, Sim2HintDuration);
            return;
        }

        if (!string.IsNullOrWhiteSpace(completedLine))
            SetMissionCompletedLine(completedLine);
        if (!string.IsNullOrWhiteSpace(objectiveLine))
            SetMissionObjectiveLine(objectiveLine);
    }

    /// <summary>Old single-line HUD when mission status panel is not used.</summary>
    void ShowLegacyMissionHintIfNoPanel(string message, float durationSeconds = 2.5f)
    {
        if (UsesMissionStatusPanel() || string.IsNullOrWhiteSpace(message))
            return;

        if (objectiveText != null)
        {
            objectiveText.text = message;
            return;
        }

        ShowFeedbackMessage(message, durationSeconds);
    }

    public void ShowMissionMessage(string message, float durationSeconds = 2.5f)
    {
        if (UsesMissionStatusPanel())
            return;

        if (objectiveText != null)
        {
            objectiveText.text = message;
            return;
        }

        ShowFeedbackMessage(message, durationSeconds);
    }

    public void ClearMissionMessages()
    {
        if (_pickupFeedbackRoutine != null)
        {
            StopCoroutine(_pickupFeedbackRoutine);
            _pickupFeedbackRoutine = null;
        }

        if (UsesMissionStatusPanel())
        {
            missionStatusPanel.ClearLines();
            return;
        }

        if (objectiveText != null)
            objectiveText.text = string.Empty;

        if (pickupFeedbackText != null)
        {
            pickupFeedbackText.text = string.Empty;
            pickupFeedbackText.gameObject.SetActive(false);
        }
    }

    private void PushObjectiveToMissionPanel(string text)
    {
        if (!UsesMissionStatusPanel())
            return;

        missionStatusPanel.SetObjectiveLine(text);
    }

    public void OnFirstAidFinished()
    {
        firstAidDone = true;
        if (UsesMissionStatusPanel())
        {
            SetMissionPanelProgress(
                _flow != null ? _flow.sim2TreatmentCompleteCompleted : "Treatment complete: 1, 2, 3.",
                _flow != null ? _flow.sim2CompletedHint : "First aid complete. Simulation 2 mission finished.");
        }
        else
        {
            ShowMissionMessage(
                _flow != null ? _flow.sim2CompletedHint : "First aid complete. Simulation 2 mission finished.",
                3f);
        }

        OnFirstAidComplete?.Invoke();
        Debug.Log("First aid Completed");
    }

    public SimulationMissionBootstrap GetMissionBootstrap() => _missionBootstrap;

    string BuildSim1CollectObjectiveText()
    {
        IReadOnlyList<string> remaining = _missionBootstrap != null
            ? _missionBootstrap.GetRemainingSim1PickupDisplayNames()
            : System.Array.Empty<string>();

        if (_flow != null)
            return _flow.BuildSim1CollectObjective(remaining, itemCollected, itemToCollect);

        if (remaining.Count == 0)
            return "Turn off the lights using the light switch inside the home.";

        return $"Collect supplies inside the home (press E). Remaining: {string.Join(", ", remaining)}. Progress: {itemCollected}/{itemToCollect}.";
    }

    public void UpdateObjectiveText()
    {
        if (_flow == null)
            _flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        string text = UsesMissionStatusPanel()
            ? MissionProximityObjectiveResolver.Resolve(this, _flow, Camera.main)
            : BuildLegacySim1ObjectiveText();

        if (string.IsNullOrWhiteSpace(text))
            return;

        if (objectiveText != null)
            objectiveText.text = text;
        PushObjectiveToMissionPanel(text);
    }

    private string BuildLegacySim1ObjectiveText()
    {
        switch (_sim1Phase)
        {
            case Sim1MissionPhase.CollectItems:
                return BuildSim1CollectObjectiveText();
            case Sim1MissionPhase.TurnOffLights:
                return _flow != null
                    ? _flow.sim1ObjectiveTurnOffLights
                    : "Turn off the lights using PFB_Lightswitch (1) inside the home.";
            case Sim1MissionPhase.CloseDoor:
                return _flow != null
                    ? _flow.sim1ObjectiveCloseDoor
                    : "Close the entrance door before going to the Mamad (press E anytime).";
            case Sim1MissionPhase.RunToShelter:
                return _flow != null
                    ? _flow.sim1ObjectiveRunToShelter
                    : "Run to the Mamad — the entrance door must be closed when you arrive.";
            default:
                return string.Empty;
        }
    }

    private void UpdateSimulation2ObjectiveText()
    {
        if (_flow == null)
            _flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        string text = UsesMissionStatusPanel()
            ? MissionProximityObjectiveResolver.Resolve(this, _flow, Camera.main)
            : BuildLegacySim2ObjectiveText();

        if (string.IsNullOrWhiteSpace(text))
            return;

        if (objectiveText != null)
            objectiveText.text = text;
        PushObjectiveToMissionPanel(text);
    }

    private string BuildLegacySim2ObjectiveText()
    {
        if (!_firstAidKitCollected)
        {
            return _flow != null
                ? _flow.sim2ObjectiveFindKit
                : "Search the city for the first aid kit.";
        }

        if (!_casualtyContacted)
        {
            return _flow != null
                ? _flow.sim2ObjectiveFindWounded
                : "Find the wounded person in the city. Press E when you reach them.";
        }

        if (!_emergencyReported)
        {
            return _flow != null
                ? _flow.sim2ObjectiveGoToPhone
                : Sim2GoToPhoneObjective;
        }

        if (!firstAidDone)
        {
            return _flow != null
                ? _flow.sim2TreatWoundedHint
                : "Return to the wounded. Press E, then 1, then 2, then 3.";
        }

        return "First aid complete.";
    }

    /// <returns>True if the shelter objective was completed now; false if prerequisites are not met yet.</returns>
    public bool ReachShelter()
    {
        if (_shelterReached) return false;
        if (!_itemsCollectionComplete) return false;
        if (!_lightsTurnedOff) return false;

        if (!IsMissionExitDoorClosed())
        {
            string hint = GetShelterDoorOpenHint();
            if (UsesMissionStatusPanel())
                SetMissionObjectiveLine(hint);
            else
                ShowLegacyMissionHintIfNoPanel(hint, 4f);
            return false;
        }

        _exitDoorClosed = true;
        _shelterReached = true;
        UpdateObjectiveText();
        TryCompleteSimulation1Goals();
        return true;
    }

    private void TryCompleteSimulation1Goals()
    {
        if (_allItemsCollectedRaised) return;
        if (!_itemsCollectionComplete) return;
        if (!_lightsTurnedOff) return;
        if (!_exitDoorClosed) return;
        if (!_shelterReached) return;

        _allItemsCollectedRaised = true;
        OnAllItemsCollected?.Invoke();
    }

    private void PlayAllItemsCollectedVoice()
    {
        if (voiceAudioSource == null || allItemsCollectedRunToMamadClip == null) return;
        voiceAudioSource.loop = false;
        voiceAudioSource.clip = allItemsCollectedRunToMamadClip;
        voiceAudioSource.Play();
    }
}
