using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simulation 2 phone flow (after kit + wounded contact):
/// E open door (once) → E insert coin → E lift Receiver → keys 1, 0, 1 → return to wounded.
/// Uses <see cref="DSUKPhoneBox"/> for the booth door animation.
/// </summary>
public class PublicPhoneBoothMission : MonoBehaviour
{
    public enum BoothAction
    {
        OpenDoor,
        InsertCoin,
        TakeHandset
    }

    public enum BoothStep
    {
        OpenDoor,
        InsertCoin,
        TakeHandset,
        Dial101,
        CallComplete
    }

    [Header("Asset")]
    [SerializeField] DSUKPhoneBox phoneBox;

    [Header("Receiver (optional — auto-found as child named Receiver)")]
    [SerializeField] Transform handsetTransform;

    [Header("Handset lift offset when taken (local space)")]
    [SerializeField] Vector3 handsetLiftOffset = new Vector3(0f, 0.12f, 0.22f);

    [Header("Receiver interaction")]
    [SerializeField] Vector3 receiverInteractTriggerSize = new Vector3(0.38f, 0.48f, 0.38f);

    [Header("Approach hint")]
    [SerializeField] bool showApproachHintOnce = true;

    [Header("Interaction")]
    [Tooltip("Press E on the front glass / booth shell to open the door (not only the door mesh).")]
    [SerializeField] bool allowFrontShellInteract = true;
    [Tooltip("Seconds after opening before blocking colliders are disabled so you can walk in.")]
    [SerializeField] float doorOpenColliderDelay = 0.35f;
    [Tooltip("Fallback when raycast misses: still accept E while facing the booth within this range.")]
    [SerializeField] float facingInteractDistance = 5f;

    private BoothStep _step = BoothStep.OpenDoor;
    private readonly List<Collider> _passageBlockers = new List<Collider>(8);
    private Vector3 _handsetRestLocalPosition;
    private Quaternion _handsetRestLocalRotation;
    private bool _handsetPoseStored;
    private bool _hintShown;
    private bool _passageAllowsWalk;
    private bool _doorOpened;
    private bool _receiverLifted;
    private bool _hierarchySetupApplied;
    private string _dialedDigits = "";
    private GameManager _gameManager;
    private TrainingFlowController _flow;
    private const float Sim2BoothHintDuration = 7f;

    public BoothStep CurrentStep => _step;

    public bool IsDialing => _step == BoothStep.Dial101;

    public Vector3 GetInteractCenter()
    {
        var door = FindChildTransform("Door");
        if (door != null)
            return door.position;
        return transform.position + transform.forward * 0.5f + Vector3.up * 1.2f;
    }

    void Awake()
    {
        if (phoneBox == null)
            phoneBox = GetComponentInChildren<DSUKPhoneBox>(true);
    }

    void Start()
    {
        _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        _flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
        EnsureSetupFromHierarchy();
        CachePassageBlockers();
        StoreHandsetPose();
        SetPassageAllowsWalk(false);
    }

    void Update()
    {
        SyncPassageWithDoorState();
    }

    /// <summary>Call from <see cref="PlayerInteract"/> as well so 1/0/1 is always captured during sim.</summary>
    public void PollDialInput()
    {
        if (_step != BoothStep.Dial101 || !CanUseBooth())
            return;

        if (!TryReadDialDigit(out char digit))
            return;

        RegisterDialDigit(digit);
    }

    private void RegisterDialDigit(char digit)
    {
        _dialedDigits += digit;
        if (_dialedDigits.Length > 6)
            _dialedDigits = _dialedDigits.Substring(_dialedDigits.Length - 6);

        if (_dialedDigits.EndsWith("101"))
        {
            _dialedDigits = "";
            CompleteEmergencyCall();
            return;
        }

        UpdateDialPanelProgress();
    }

    private int CountDialProgressToward101()
    {
        int best = 0;
        for (int start = 0; start < _dialedDigits.Length; start++)
        {
            int matched = 0;
            for (int i = 0; i < 3; i++)
            {
                int idx = start + i;
                if (idx >= _dialedDigits.Length)
                    break;

                char expected = i == 0 || i == 2 ? '1' : '0';
                if (_dialedDigits[idx] != expected)
                    break;

                matched++;
            }

            if (matched > best)
                best = matched;
        }

        return best;
    }

    private static bool TryReadDialDigit(out char digit)
    {
        digit = default;
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            digit = '1';
            return true;
        }

        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
        {
            digit = '0';
            return true;
        }

        return false;
    }

    public void ResetForMission()
    {
        _step = BoothStep.OpenDoor;
        _dialedDigits = "";
        _hintShown = false;
        _doorOpened = false;
        _receiverLifted = false;
        phoneBox?.Close();
        ResetReceiverPickupState();
        SetPassageAllowsWalk(false);
    }

    /// <summary>Called from <see cref="PickUpItem"/> on the Receiver when the player presses E.</summary>
    public bool TryPickupReceiver()
    {
        if (!CanUseBooth(out string blockMessage))
        {
            if (!string.IsNullOrEmpty(blockMessage))
                _gameManager?.ShowTransientMissionNote(blockMessage, Sim2BoothHintDuration);
            return false;
        }

        if (_step == BoothStep.Dial101 || _step == BoothStep.CallComplete)
        {
            ShowDialHint();
            return true;
        }

        if (_step == BoothStep.OpenDoor)
        {
            ShowFlowMessage(_flow != null ? _flow.sim2PhoneOpenDoorHint : "Open the booth door first (press E on the door).");
            return false;
        }

        if (_step == BoothStep.InsertCoin)
        {
            ShowFlowMessage(_flow != null ? _flow.sim2PhoneInsertCoinHint : "Insert a coin first (press E on the coin slot).");
            return false;
        }

        if (_step != BoothStep.TakeHandset)
        {
            ShowWrongStepHint();
            return false;
        }

        if (_receiverLifted)
            return true;

        LiftReceiver();
        _receiverLifted = true;
        _step = BoothStep.Dial101;
        _dialedDigits = "";
        ShowBoothStep(
            _flow != null ? _flow.sim2PhoneReceiverLiftedCompleted : "Receiver lifted.",
            FormatPhoneDialRemaining(0));
        return true;
    }

    public void EnsureSetupFromHierarchy()
    {
        if (!_hierarchySetupApplied)
        {
            RemoveMisplacedHomeDoorScripts();

            if (phoneBox == null)
                phoneBox = GetComponent<DSUKPhoneBox>() ?? GetComponentInChildren<DSUKPhoneBox>(true);

            if (phoneBox == null)
                Debug.LogWarning("PublicPhoneBoothMission: Add DSUKPhoneBox to the UK Phone Box root (same object as Animator).");

            TagInteractPoint(FindChildTransform("Door"), BoothAction.OpenDoor, new Vector3(0.7f, 1.9f, 0.2f));
            TagInteractPoint(FindChildTransform("Coin Insert"), BoothAction.InsertCoin, new Vector3(0.2f, 0.2f, 0.15f));
            TagInteractPoint(FindChildTransform("Coin Collect Box"), BoothAction.InsertCoin, new Vector3(0.25f, 0.25f, 0.15f));
            _hierarchySetupApplied = true;
        }

        handsetTransform = ResolveReceiverTransform();
        EnsureReceiverPickup();
        SanitizePickupScripts();
    }

    /// <summary>Removes Sim1-style pickups on the booth (never on Receiver).</summary>
    private void SanitizePickupScripts()
    {
        var pickups = GetComponentsInChildren<PickUpItem>(true);
        var toRemove = new List<PickUpItem>(4);
        for (int i = 0; i < pickups.Length; i++)
        {
            var pickup = pickups[i];
            if (pickup == null || IsReceiverPickupTransform(pickup.transform))
                continue;

            toRemove.Add(pickup);
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            if (toRemove[i] == null)
                continue;

            if (Application.isPlaying)
                Destroy(toRemove[i]);
            else
                DestroyImmediate(toRemove[i]);
        }
    }

    private static bool IsReceiverTransform(Transform t)
    {
        if (t == null)
            return false;

        return t.name.IndexOf("Receiver", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool IsReceiverPickupTransform(Transform t)
    {
        if (t == null)
            return false;

        if (IsReceiverTransform(t))
            return true;

        if (handsetTransform != null && (t == handsetTransform || t.IsChildOf(handsetTransform)))
            return true;

        return t.name.IndexOf("ReceiverInteract", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void EnsureReceiverPickup()
    {
        handsetTransform = ResolveReceiverTransform();
        if (handsetTransform == null)
        {
            Debug.LogWarning("PublicPhoneBoothMission: No child named 'Receiver' found.");
            return;
        }

        EnsureReceiverInteractTrigger(handsetTransform);

        var pickup = handsetTransform.GetComponent<PickUpItem>();
        if (pickup == null)
            pickup = handsetTransform.gameObject.AddComponent<PickUpItem>();

        pickup.ConfigureForPhoneReceiver("Receiver");
        RemoveTakeHandsetInteractPointsFromReceiver();
    }

    private void EnsureReceiverInteractTrigger(Transform receiver)
    {
        const string interactChildName = "ReceiverInteract";
        Transform interactRoot = receiver.Find(interactChildName);
        if (interactRoot == null)
        {
            var interactObject = new GameObject(interactChildName);
            interactRoot = interactObject.transform;
            interactRoot.SetParent(receiver, false);
            interactRoot.localPosition = Vector3.zero;
            interactRoot.localRotation = Quaternion.identity;
            interactRoot.localScale = Vector3.one;
        }

        var trigger = interactRoot.GetComponent<BoxCollider>();
        if (trigger == null)
            trigger = interactRoot.gameObject.AddComponent<BoxCollider>();

        trigger.isTrigger = true;
        trigger.size = receiverInteractTriggerSize;
        trigger.center = Vector3.zero;

        var pickup = interactRoot.GetComponent<PickUpItem>();
        if (pickup == null)
            pickup = interactRoot.gameObject.AddComponent<PickUpItem>();

        pickup.ConfigureForPhoneReceiver("Receiver");
    }

    private void ResetReceiverPickupState()
    {
        if (handsetTransform == null)
            handsetTransform = ResolveReceiverTransform();

        if (handsetTransform == null)
            return;

        var pickup = handsetTransform.GetComponent<PickUpItem>();
        if (pickup != null)
            pickup.ConfigureForPhoneReceiver("Receiver");

        var interact = handsetTransform.Find("ReceiverInteract");
        if (interact != null)
        {
            var interactPickup = interact.GetComponent<PickUpItem>();
            if (interactPickup != null)
                interactPickup.ConfigureForPhoneReceiver("Receiver");
        }

        RestoreHandsetPose();
    }

    private void RemoveTakeHandsetInteractPointsFromReceiver()
    {
        if (handsetTransform == null)
            return;

        var points = handsetTransform.GetComponents<PhoneBoothInteractPoint>();
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null)
                continue;

            if (Application.isPlaying)
                Destroy(points[i]);
            else
                DestroyImmediate(points[i]);
        }
    }

    private Transform ResolveReceiverTransform()
    {
        var receiver = FindChildTransform("Receiver");
        if (receiver != null)
            return receiver;

        if (handsetTransform != null && IsValidReceiverTransform(handsetTransform))
            return handsetTransform;

        return null;
    }

    private bool IsValidReceiverTransform(Transform t)
    {
        if (t == null || t == transform)
            return false;

        string name = t.name;
        if (name.IndexOf("Box", System.StringComparison.OrdinalIgnoreCase) >= 0
            && name.IndexOf("Receiver", System.StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        return name.IndexOf("Receiver", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>When raycast misses the booth, still run the current step if player faces the booth nearby.</summary>
    public bool TryInteractWithoutRaycast()
    {
        if (_gameManager != null && _gameManager.HasReportedEmergency())
            return false;

        if (!CanUseBooth(out string blockMessage))
        {
            if (!string.IsNullOrEmpty(blockMessage))
                _gameManager?.ShowTransientMissionNote(blockMessage, Sim2BoothHintDuration);
            return false;
        }

        if (_step == BoothStep.CallComplete)
        {
            ShowFlowMessage(_flow != null ? _flow.sim2AlreadyReportedHint : "You already called dispatch. Return to the wounded person.");
            return true;
        }

        if (_step == BoothStep.Dial101)
        {
            ShowDialHint();
            return true;
        }

        if (_step == BoothStep.TakeHandset)
            return TryPickupReceiver();

        return TryHandleBoothShellInteract();
    }

    /// <summary>Called from <see cref="PlayerInteract"/> when the player presses E.</summary>
    public bool TryInteract(Collider hitCollider)
    {
        if (_gameManager != null && _gameManager.HasReportedEmergency())
            return false;

        if (!CanUseBooth(out string blockMessage))
        {
            if (!string.IsNullOrEmpty(blockMessage))
                _gameManager?.ShowTransientMissionNote(blockMessage, Sim2BoothHintDuration);
            return true;
        }

        if (_step == BoothStep.CallComplete)
        {
            ShowFlowMessage(_flow != null ? _flow.sim2AlreadyReportedHint : "You already called dispatch. Return to the wounded person.");
            return true;
        }

        if (_step == BoothStep.Dial101)
        {
            ShowDialHint();
            return true;
        }

        var point = hitCollider.GetComponent<PhoneBoothInteractPoint>()
            ?? hitCollider.GetComponentInParent<PhoneBoothInteractPoint>();
        if (point == null || point.booth != this)
            point = ResolvePointFromTransform(hitCollider.transform);

        if (_step == BoothStep.TakeHandset && IsUnderBooth(hitCollider.transform))
        {
            if (TryPickupReceiver())
                return true;
        }

        if (point == null && allowFrontShellInteract && IsUnderBooth(hitCollider.transform))
        {
            if (TryHandleBoothShellInteract())
                return true;
        }

        if (point == null)
        {
            ShowCurrentStepHint();
            return true;
        }

        HandleAction(point.action);
        return true;
    }

    private bool TryHandleBoothShellInteract()
    {
        switch (_step)
        {
            case BoothStep.OpenDoor:
                HandleAction(BoothAction.OpenDoor);
                return true;
            case BoothStep.InsertCoin:
                HandleAction(BoothAction.InsertCoin);
                return true;
            case BoothStep.TakeHandset:
                return TryPickupReceiver();
            case BoothStep.Dial101:
                ShowDialHint();
                return true;
            default:
                ShowCurrentStepHint();
                return true;
        }
    }

    private bool IsUnderBooth(Transform t)
    {
        if (t == null)
            return false;

        return t == transform || t.IsChildOf(transform);
    }

    private void HandleAction(BoothAction action)
    {
        switch (action)
        {
            case BoothAction.OpenDoor:
                if (_doorOpened)
                {
                    ShowFlowMessage(_flow != null ? _flow.sim2PhoneDoorAlreadyOpenHint : "The door is already open (one time only). Press E on the coin slot.");
                    return;
                }

                if (_step != BoothStep.OpenDoor)
                {
                    ShowWrongStepHint();
                    return;
                }

                OpenDoorOnce();
                break;

            case BoothAction.InsertCoin:
                if (_step == BoothStep.OpenDoor)
                {
                    ShowFlowMessage(_flow != null ? _flow.sim2PhoneOpenDoorHint : "Press E on the door to open the booth first.");
                    return;
                }

                if (_step != BoothStep.InsertCoin)
                {
                    ShowWrongStepHint();
                    return;
                }

                _step = BoothStep.TakeHandset;
                ShowBoothStep(
                    _flow != null ? _flow.sim2PhoneCoinInsertedCompleted : "Coin inserted.",
                    _flow != null ? _flow.sim2PhoneCoinInsertedObjective : "Press E on the receiver.");
                break;

            case BoothAction.TakeHandset:
                TryPickupReceiver();
                break;
        }
    }

    private void OpenDoorOnce()
    {
        if (_doorOpened)
            return;

        _doorOpened = true;

        if (phoneBox != null)
            phoneBox.Open();
        else
            Debug.LogWarning("PublicPhoneBoothMission: DSUKPhoneBox missing on telephone root.");

        _step = BoothStep.InsertCoin;
        SetPassageAllowsWalk(true);
        StartCoroutine(EnablePassageAfterDoorOpens());
        ShowBoothStep(
            _flow != null ? _flow.sim2PhoneDoorOpenedCompleted : "Door opened.",
            _flow != null ? _flow.sim2PhoneDoorOpenedObjective : "Press E on the coin slot, then E on the receiver.");
    }

    private void CompleteEmergencyCall()
    {
        _step = BoothStep.CallComplete;
        _gameManager?.OnEmergencyReported();
    }

    private void LiftReceiver()
    {
        handsetTransform = ResolveReceiverTransform();
        if (handsetTransform == null)
        {
            Debug.LogWarning("PublicPhoneBoothMission: Cannot lift — Receiver not found.");
            return;
        }

        StoreHandsetPose();
        handsetTransform.localPosition = _handsetRestLocalPosition + handsetLiftOffset;
        handsetTransform.localRotation = _handsetRestLocalRotation;
    }

    private void StoreHandsetPose()
    {
        if (handsetTransform == null || _handsetPoseStored)
            return;

        _handsetRestLocalPosition = handsetTransform.localPosition;
        _handsetRestLocalRotation = handsetTransform.localRotation;
        _handsetPoseStored = true;
    }

    private void RestoreHandsetPose()
    {
        if (handsetTransform == null || !_handsetPoseStored)
            return;

        handsetTransform.localPosition = _handsetRestLocalPosition;
        handsetTransform.localRotation = _handsetRestLocalRotation;
    }

    private bool CanUseBooth() => CanUseBooth(out _);

    private bool CanUseBooth(out string message)
    {
        message = null;
        if (_gameManager == null)
            _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

        if (_gameManager == null)
            return true;

        if (!_gameManager.HasFirstAidKit())
        {
            message = _flow != null ? _flow.sim2NeedKitHint : "Collect the first aid kit first.";
            return false;
        }

        if (!_gameManager.HasContactedCasualty())
        {
            message = _flow != null
                ? _flow.sim2NeedContactCasualtyBeforePhoneHint
                : "Find the wounded person first and press E on the casualty.";
            return false;
        }

        if (_gameManager.HasReportedEmergency())
        {
            message = _flow != null ? _flow.sim2AlreadyReportedHint : "You already called dispatch. Return to the wounded person.";
            return false;
        }

        return true;
    }

    private void ShowCurrentStepHint()
    {
        switch (_step)
        {
            case BoothStep.OpenDoor:
                ShowFlowMessage(_flow != null ? _flow.sim2PhoneOpenDoorHint : "Press E on the booth door to open it.");
                break;
            case BoothStep.InsertCoin:
                ShowFlowMessage(_flow != null ? _flow.sim2PhoneInsertCoinHint : "Press E on the coin slot.");
                break;
            case BoothStep.TakeHandset:
                ShowFlowMessage(_flow != null ? _flow.sim2PhoneCoinInsertedObjective : "Press E on the receiver.");
                break;
            case BoothStep.Dial101:
                ShowDialHint();
                break;
        }
    }

    private void ShowDialHint()
    {
        UpdateDialPanelProgress();
    }

    private void UpdateDialPanelProgress()
    {
        int progress = CountDialProgressToward101();
        string completed = FormatPhoneDialCompleted(progress);
        string objective = FormatPhoneDialRemaining(progress);

        if (progress <= 0 && string.IsNullOrEmpty(completed))
        {
            ShowBoothStep(
                _flow != null ? _flow.sim2PhoneReceiverLiftedCompleted : "Receiver lifted.",
                objective);
            return;
        }

        ShowBoothStep(completed, objective);
    }

    private static string FormatPhoneDialCompleted(int progress)
    {
        if (progress <= 0)
            return string.Empty;

        return $"Entered: {GetPhoneDialEntered(progress)}";
    }

    private static string GetPhoneDialEntered(int progress)
    {
        if (progress >= 3)
            return "1, 0, 1";
        if (progress == 2)
            return "1, 0";
        if (progress == 1)
            return "1";
        return "—";
    }

    private static string FormatPhoneDialRemaining(int progress)
    {
        if (progress >= 3)
            return string.Empty;

        if (progress == 2)
            return "Remaining: 1 (number keys only)";
        if (progress == 1)
            return "Remaining: 0, 1 (number keys only)";
        return "Remaining: 1, 0, 1 (number keys only)";
    }

    private void ShowWrongStepHint()
    {
        ShowCurrentStepHint();
    }

    private void ShowFlowMessage(string msg)
    {
        if (_gameManager == null || string.IsNullOrWhiteSpace(msg))
            return;

        if (_gameManager.HasMissionStatusPanel())
            _gameManager.SetMissionObjectiveLine(msg);
        else
            _gameManager.ShowMissionMessage(msg, Sim2BoothHintDuration);
    }

    private void ShowBoothStep(string completedLine, string objectiveLine)
    {
        if (_gameManager != null)
            _gameManager.SetMissionPanelProgress(completedLine, objectiveLine);
        else if (!string.IsNullOrWhiteSpace(objectiveLine))
            ShowFlowMessage(objectiveLine);
    }

    private PhoneBoothInteractPoint ResolvePointFromTransform(Transform t)
    {
        while (t != null)
        {
            var point = t.GetComponent<PhoneBoothInteractPoint>();
            if (point != null && point.booth == this)
                return point;

            string name = t.name;
            if (name.IndexOf("Door", System.StringComparison.OrdinalIgnoreCase) >= 0 && t != transform)
                return TagInteractPoint(t, BoothAction.OpenDoor, new Vector3(0.7f, 1.9f, 0.2f));
            if (name.IndexOf("Coin Insert", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Coin Collect", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return TagInteractPoint(t, BoothAction.InsertCoin, new Vector3(0.25f, 0.25f, 0.15f));
            if (name.IndexOf("Receiver", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("UK Phone", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("UK_Phone", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return null;

            t = t.parent;
        }

        return null;
    }

    private PhoneBoothInteractPoint TagInteractPoint(Transform target, BoothAction action, Vector3 triggerSize)
    {
        if (target == null)
            return null;

        Collider col = target.GetComponent<Collider>();
        if (col == null)
        {
            var box = target.gameObject.AddComponent<BoxCollider>();
            box.size = triggerSize;
            col = box;
        }

        if (col is BoxCollider boxCol)
        {
            boxCol.isTrigger = true;
            if (action == BoothAction.OpenDoor)
                boxCol.size = triggerSize;
        }
        else
        {
            col.isTrigger = true;
        }

        var point = target.GetComponent<PhoneBoothInteractPoint>();
        if (point == null)
            point = target.gameObject.AddComponent<PhoneBoothInteractPoint>();
        point.Initialize(this, action);
        return point;
    }

    /// <summary>
    /// The home exit-door script must not sit on the UK booth door — it breaks E and has no animator here.
    /// </summary>
    private void RemoveMisplacedHomeDoorScripts()
    {
        var homeDoors = GetComponentsInChildren<Door>(true);
        var toRemove = new List<Door>(4);
        for (int i = 0; i < homeDoors.Length; i++)
        {
            if (homeDoors[i] != null)
                toRemove.Add(homeDoors[i]);
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            if (toRemove[i] == null)
                continue;

            if (Application.isPlaying)
                Destroy(toRemove[i]);
            else
                DestroyImmediate(toRemove[i]);
        }
    }

    private Transform FindChildTransform(string objectName)
    {
        var all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == objectName)
                return all[i];
        }

        return null;
    }

    private void CachePassageBlockers()
    {
        _passageBlockers.Clear();

        AddPassageBlockerOn(FindChildTransform("Door"));
        AddPassageBlockerOn(FindChildTransform("UK_Phone BoxCollider"));

        if (_passageBlockers.Count > 0)
            return;

        var colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            var col = colliders[i];
            if (col == null || col.isTrigger)
                continue;

            if (!_passageBlockers.Contains(col))
                _passageBlockers.Add(col);
        }
    }

    private void AddPassageBlockerOn(Transform target)
    {
        if (target == null)
            return;

        var colliders = target.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            var col = colliders[i];
            if (col == null || col.isTrigger)
                continue;

            if (!_passageBlockers.Contains(col))
                _passageBlockers.Add(col);
        }
    }

    /// <summary>When true, solid booth colliders are off so the player can walk through the open door.</summary>
    private void SetPassageAllowsWalk(bool allowWalk)
    {
        _passageAllowsWalk = allowWalk;

        for (int i = 0; i < _passageBlockers.Count; i++)
        {
            if (_passageBlockers[i] != null)
                _passageBlockers[i].enabled = !allowWalk;
        }
    }

    private void SyncPassageWithDoorState()
    {
        if (phoneBox == null)
            return;

        if (_step == BoothStep.OpenDoor)
        {
            if (_passageAllowsWalk)
                SetPassageAllowsWalk(false);
            return;
        }

        if (phoneBox.IsOpen && !_passageAllowsWalk)
            SetPassageAllowsWalk(true);
    }

    private IEnumerator EnablePassageAfterDoorOpens()
    {
        if (doorOpenColliderDelay > 0f)
            yield return new WaitForSeconds(doorOpenColliderDelay);

        SetPassageAllowsWalk(true);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!showApproachHintOnce || _hintShown)
            return;

        if (!other.CompareTag("Player") && other.GetComponent<CharacterController>() == null)
            return;

        if (!CanUseBooth())
            return;

        _hintShown = true;
    }
}
