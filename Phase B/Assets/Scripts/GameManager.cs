using System;
using System.Collections;
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
    public TextMeshProUGUI objectiveText;
    public TextMeshProUGUI pickupFeedbackText;
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

    void Awake()
    {
        _missionBootstrap = GetComponent<SimulationMissionBootstrap>();
        if (_missionBootstrap == null)
            _missionBootstrap = gameObject.AddComponent<SimulationMissionBootstrap>();
    }

    void Start()
    {
        _flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
        ConfigureShelterTargets();
        _allItemsCollectedRaised = false;
        if (pickupFeedbackText != null)
            pickupFeedbackText.gameObject.SetActive(false);
        UpdateObjectiveText();
    }

    public void PrepareSimulation1Mission()
    {
        if (_flow == null)
            _flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        ResetSimulation1State();
        _missionBootstrap?.PrepareSimulation1();
        UpdateObjectiveText();
        ShowMissionMessage(_flow != null ? _flow.sim1MissionStartHint : "Enter the home and collect all emergency supplies.", 4f);
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
        UpdateSimulation2ObjectiveText();
        ShowMissionMessage(_flow != null ? _flow.sim2MissionStartHint : "Search the city for the first aid kit.", 4f);
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

    /// <summary>Current in-mission objective line for Help overlay and HUD.</summary>
    public string GetCurrentObjectiveText()
    {
        if (objectiveText != null && !string.IsNullOrWhiteSpace(objectiveText.text))
            return objectiveText.text;

        if (_flow == null)
            _flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        if (_flow == null)
            return null;

        var phase = _flow.CurrentPhase;
        if (phase == TrainingFlowController.Phase.Simulation1Active)
        {
            switch (_sim1Phase)
            {
                case Sim1MissionPhase.CollectItems:
                    return _flow.BuildSim1CollectObjective(itemCollected, itemToCollect);
                case Sim1MissionPhase.TurnOffLights:
                    return _flow.sim1ObjectiveTurnOffLights;
                case Sim1MissionPhase.CloseDoor:
                    return _flow.sim1ObjectiveCloseDoor;
                case Sim1MissionPhase.RunToShelter:
                    return _flow.sim1ObjectiveRunToShelter;
            }
        }

        if (phase == TrainingFlowController.Phase.Simulation2Active)
        {
            if (!_firstAidKitCollected)
                return _flow.sim2ObjectiveFindKit;
            if (!_casualtyContacted)
                return _flow.sim2ObjectiveFindWounded;
            if (!_emergencyReported)
                return _flow.sim2ObjectiveCallDispatch;
            if (!firstAidDone)
                return _flow.sim2TreatWoundedHint;
            return "First aid complete.";
        }

        return null;
    }

    public void OnLightsTurnedOff()
    {
        if (_sim1Phase != Sim1MissionPhase.TurnOffLights)
            return;

        _lightsTurnedOff = true;
        _sim1Phase = Sim1MissionPhase.RunToShelter;
        PlayAllItemsCollectedVoice();
        ShowMissionMessage(
            _flow != null ? _flow.sim1LightsOffHint : "Lights off. Run to the Mamad — close the entrance door before you enter.",
            6f);
        UpdateObjectiveText();
    }

    public void OnFirstAidKitCollected(string itemName)
    {
        if (_firstAidKitCollected)
            return;

        _firstAidKitCollected = true;
        _missionBootstrap?.RevealWounded();
        ShowPickupFeedback(itemName);
        ShowMissionMessage(_flow != null ? _flow.sim2KitCollectedHint : "First aid kit collected. Search the city and find the wounded person.", 6f);
        UpdateSimulation2ObjectiveText();
    }

    /// <summary>Player pressed E on the casualty before calling dispatch.</summary>
    public void OnCasualtyApproached()
    {
        if (_casualtyContacted)
            return;

        _casualtyContacted = true;
        ShowMissionMessage(
            _flow != null ? _flow.sim2CasualtyApproachedHint : "Go to the public telephone and call for first aid help (dial 1, 0, 1).",
            6f);
        UpdateSimulation2ObjectiveText();
    }

    public void OnEmergencyReported()
    {
        if (_emergencyReported)
            return;

        _emergencyReported = true;
        ShowMissionMessage(
            _flow != null ? _flow.sim2ReportCompletedHint : "First aid help is on the way. Return to the wounded person for treatment.",
            6f);
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
            ShowMissionMessage(_flow != null ? _flow.sim1AllItemsCollectedHint : "All supplies collected. Turn off the lights using the switch inside the home.", 6f);
            OnItemsCollectionComplete?.Invoke();
            UpdateObjectiveText();
        }
    }

    private void ShowPickupFeedback(string itemName)
    {
        if (pickupFeedbackText == null)
            return;

        if (_pickupFeedbackRoutine != null)
            StopCoroutine(_pickupFeedbackRoutine);

        pickupFeedbackText.text = $"{itemName} collected.";
        _pickupFeedbackRoutine = StartCoroutine(HidePickupFeedbackAfterDelay());
    }

    private void ShowFeedbackMessage(string message, float durationSeconds)
    {
        if (pickupFeedbackText == null)
            return;

        if (_pickupFeedbackRoutine != null)
            StopCoroutine(_pickupFeedbackRoutine);

        pickupFeedbackText.text = message;
        _pickupFeedbackRoutine = StartCoroutine(HideFeedbackAfterDelay(durationSeconds));
    }

    private IEnumerator HidePickupFeedbackAfterDelay()
    {
        pickupFeedbackText.gameObject.SetActive(true);
        yield return new WaitForSeconds(pickupFeedbackDuration);
        if (pickupFeedbackText != null)
            pickupFeedbackText.gameObject.SetActive(false);
        _pickupFeedbackRoutine = null;
    }

    private IEnumerator HideFeedbackAfterDelay(float delay)
    {
        pickupFeedbackText.gameObject.SetActive(true);
        yield return new WaitForSeconds(delay);
        if (pickupFeedbackText != null)
            pickupFeedbackText.gameObject.SetActive(false);
        _pickupFeedbackRoutine = null;
    }

    public void ShowMissionMessage(string message, float durationSeconds = 2.5f)
    {
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

        if (objectiveText != null)
            objectiveText.text = string.Empty;

        if (pickupFeedbackText != null)
        {
            pickupFeedbackText.text = string.Empty;
            pickupFeedbackText.gameObject.SetActive(false);
        }
    }

    public void OnFirstAidFinished()
    {
        firstAidDone = true;
        ShowMissionMessage(_flow != null ? _flow.sim2CompletedHint : "First aid complete. Simulation 2 mission finished.", 3f);
        OnFirstAidComplete?.Invoke();
        Debug.Log("First aid Completed");
    }

    public void UpdateObjectiveText()
    {
        if (objectiveText == null)
            return;

        switch (_sim1Phase)
        {
            case Sim1MissionPhase.CollectItems:
                objectiveText.text = _flow != null
                    ? _flow.BuildSim1CollectObjective(itemCollected, itemToCollect)
                    : $"Enter the home and collect supplies: {itemCollected}/{itemToCollect} — water bottle, flash light, radio, compass, map.";
                break;
            case Sim1MissionPhase.TurnOffLights:
                objectiveText.text = _flow != null
                    ? _flow.sim1ObjectiveTurnOffLights
                    : "Turn off the lights using PFB_Lightswitch (1) inside the home.";
                break;
            case Sim1MissionPhase.CloseDoor:
                objectiveText.text = _flow != null
                    ? _flow.sim1ObjectiveCloseDoor
                    : "Close the entrance door before going to the Mamad (press E anytime).";
                break;
            case Sim1MissionPhase.RunToShelter:
                objectiveText.text = _flow != null
                    ? _flow.sim1ObjectiveRunToShelter
                    : "Run to the Mamad — the entrance door must be closed when you arrive.";
                break;
        }
    }

    private void UpdateSimulation2ObjectiveText()
    {
        if (objectiveText == null)
            return;

        if (!_firstAidKitCollected)
        {
            objectiveText.text = _flow != null
                ? _flow.sim2ObjectiveFindKit
                : "Search the city for the first aid kit.";
            return;
        }

        if (!_casualtyContacted)
        {
            objectiveText.text = _flow != null
                ? _flow.sim2ObjectiveFindWounded
                : "Find the wounded person in the city. Press E when you reach them.";
            return;
        }

        if (!_emergencyReported)
        {
            objectiveText.text = _flow != null
                ? _flow.sim2ObjectiveCallDispatch
                : "Public telephone: E door (once), E coin, E receiver, dial 1, 0, 1.";
            return;
        }

        if (!firstAidDone)
        {
            objectiveText.text = _flow != null
                ? _flow.sim2TreatWoundedHint
                : "Return to the wounded. Press E, then 1, then 2, then 3.";
            return;
        }

        objectiveText.text = "First aid complete.";
    }

    /// <returns>True if the shelter objective was completed now; false if prerequisites are not met yet.</returns>
    public bool ReachShelter()
    {
        if (_shelterReached) return false;
        if (!_itemsCollectionComplete) return false;
        if (!_lightsTurnedOff) return false;

        if (!IsMissionExitDoorClosed())
        {
            ShowMissionMessage(GetShelterDoorOpenHint(), 4f);
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
