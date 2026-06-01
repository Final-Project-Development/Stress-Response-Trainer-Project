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

    [Header("Handset lift offset when taken")]
    [SerializeField] Vector3 handsetLiftOffset = new Vector3(0f, 0.04f, 0.12f);

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

        if (_step != BoothStep.Dial101)
            return;

        if (!CanUseBooth())
            return;

        if (!TryReadDialDigit(out char digit))
            return;

        _dialedDigits += digit;
        if (_dialedDigits.Length > 6)
            _dialedDigits = _dialedDigits.Substring(_dialedDigits.Length - 6);

        if (_dialedDigits.EndsWith("101"))
        {
            _dialedDigits = "";
            CompleteEmergencyCall();
            return;
        }

        int progress = CountDialProgressToward101();
        ShowFlowMessage(_flow != null
            ? $"{_flow.sim2PhoneDialProgressHint} ({progress}/3)"
            : $"Dial 101… ({progress}/3)");
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
                _gameManager?.ShowMissionMessage(blockMessage, 4f);
            return false;
        }

        if (_step == BoothStep.Dial101 || _step == BoothStep.CallComplete)
        {
            ShowDialHint();
            return true;
        }

        if (_step == BoothStep.OpenDoor)
        {
            ShowFlowMessage(_flow != null ? _flow.sim2PhoneNeedDoorHint : "Open the booth door first (press E on the door).");
            return false;
        }

        if (_step == BoothStep.InsertCoin)
        {
            ShowFlowMessage(_flow != null ? _flow.sim2PhoneNeedCoinHint : "Insert a coin first (press E on the coin slot).");
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
        ShowDialHint();
        return true;
    }

    public void EnsureSetupFromHierarchy()
    {
        if (_hierarchySetupApplied && Application.isPlaying)
            return;

        RemoveMisplacedHomeDoorScripts();

        if (phoneBox == null)
            phoneBox = GetComponent<DSUKPhoneBox>() ?? GetComponentInChildren<DSUKPhoneBox>(true);

        if (phoneBox == null)
            Debug.LogWarning("PublicPhoneBoothMission: Add DSUKPhoneBox to the UK Phone Box root (same object as Animator).");

        handsetTransform = ResolveReceiverTransform();

        TagInteractPoint(FindChildTransform("Door"), BoothAction.OpenDoor, new Vector3(0.7f, 1.9f, 0.2f));
        TagInteractPoint(FindChildTransform("Coin Insert"), BoothAction.InsertCoin, new Vector3(0.2f, 0.2f, 0.15f));
        TagInteractPoint(FindChildTransform("Coin Collect Box"), BoothAction.InsertCoin, new Vector3(0.25f, 0.25f, 0.15f));

        EnsureReceiverPickup();
        SanitizePickupScripts();
        _hierarchySetupApplied = true;
    }

    /// <summary>Removes Sim1-style pickups on the booth (never on Receiver).</summary>
    private void SanitizePickupScripts()
    {
        var pickups = GetComponentsInChildren<PickUpItem>(true);
        var toRemove = new List<PickUpItem>(4);
        for (int i = 0; i < pickups.Length; i++)
        {
            var pickup = pickups[i];
            if (pickup == null || IsReceiverTransform(pickup.transform))
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

    private void EnsureReceiverPickup()
    {
        handsetTransform = ResolveReceiverTransform();
        if (handsetTransform == null)
        {
            Debug.LogWarning("PublicPhoneBoothMission: No child named 'Receiver' found.");
            return;
        }

        var col = handsetTransform.GetComponent<Collider>();
        if (col == null)
        {
            var box = handsetTransform.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.2f, 0.3f, 0.2f);
        }
        else if (!col.isTrigger && col is BoxCollider boxCol)
        {
            boxCol.isTrigger = true;
        }

        var pickup = handsetTransform.GetComponent<PickUpItem>();
        if (pickup == null)
            pickup = handsetTransform.gameObject.AddComponent<PickUpItem>();

        pickup.ConfigureForPhoneReceiver("Receiver");
        RemoveTakeHandsetInteractPointsFromReceiver();
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
                _gameManager?.ShowMissionMessage(blockMessage, 4f);
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
                _gameManager?.ShowMissionMessage(blockMessage, 4f);
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
                ShowFlowMessage(_flow != null ? _flow.sim2PhoneTakeHandsetHint : "Press E on the receiver to pick it up.");
                return true;
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
                    ShowFlowMessage(_flow != null ? _flow.sim2PhoneNeedDoorHint : "Press E on the door to open the booth first.");
                    return;
                }

                if (_step != BoothStep.InsertCoin)
                {
                    ShowWrongStepHint();
                    return;
                }

                _step = BoothStep.TakeHandset;
                ShowFlowMessage(_flow != null ? _flow.sim2PhoneCoinInsertedHint : "Coin inserted. Press E on the receiver.");
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
        ShowFlowMessage(_flow != null ? _flow.sim2PhoneDoorOpenedHint : "Door open (one time only). Press E on the coin slot, then the receiver.");
    }

    private void CompleteEmergencyCall()
    {
        _step = BoothStep.CallComplete;
        _gameManager?.OnEmergencyReported();
        ShowFlowMessage(_flow != null ? _flow.sim2PhoneCallConnectedHint : "First aid help called. Return to the wounded person for treatment.");
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
                ShowFlowMessage(_flow != null ? _flow.sim2PhoneTakeHandsetHint : "Press E on the receiver to pick it up.");
                break;
            case BoothStep.Dial101:
                ShowDialHint();
                break;
        }
    }

    private void ShowDialHint()
    {
        ShowFlowMessage(_flow != null
            ? _flow.sim2PhoneDialStartHint
            : "Dial 1, then 0, then 1 to call for help (number keys only — not E).");
    }

    private void ShowWrongStepHint()
    {
        ShowCurrentStepHint();
    }

    private void ShowFlowMessage(string msg)
    {
        _gameManager?.ShowMissionMessage(msg, 4.5f);
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
        ShowFlowMessage(_flow != null ? _flow.sim2ApproachDispatchHint : "Use the public telephone to call for first aid help.");
    }
}
