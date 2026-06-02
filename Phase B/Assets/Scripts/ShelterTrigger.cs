using UnityEngine;

/// <summary>
/// Attach to the Mamad root. Creates a child "ShelterTriggerVolume" with a trigger BoxCollider
/// (Unity sends trigger events to the object that owns the collider, so we relay from a child).
/// When the player enters, marks shelter objective as reached in GameManager.
/// </summary>
public class ShelterTrigger : MonoBehaviour
{
    public const string VolumeChildName = "ShelterTriggerVolume";

    public string playerTag = "Player";
    public bool autoCreateTriggerCollider = true;

    [Header("Optional: most reliable — empty at bottom of stairs")]
    [Tooltip("Create an empty GameObject at the underground spot and assign it here. Completion uses sphere around it.")]
    public Transform undergroundCompletionPoint;
    [Tooltip("Radius around Underground Completion Point (used when that point is assigned).")]
    public float completionRadius = 2.2f;

    [Header("Auto volume (when completion point is not used)")]
    [Tooltip("Where the trigger volume sits under the Mamad root (usually negative Y = down the stairs).")]
    public Vector3 volumeLocalPosition = new Vector3(0f, -3.2f, 0f);
    [Tooltip("Local-space box size (keep small so the street path toward the entrance does not count).")]
    public Vector3 autoTriggerSize = new Vector3(4f, 3f, 4f);
    [Tooltip("Local-space center offset inside the volume object.")]
    public Vector3 triggerCenter = Vector3.zero;
    [Tooltip("Along the Mamad up axis, player must be this many meters below the Mamad origin. 0 = off (recommended if unsure).")]
    public float minimumDepthBelowMamadOrigin = 0f;

    private GameManager _gameManager;
    private bool _triggered;
    private Collider _triggerCollider;
    private Transform _playerTransform;

    void Awake()
    {
        EnsureTriggerCollider();
        _triggerCollider = ResolveTriggerCollider();
    }

    void Start()
    {
        _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        _playerTransform = FindPlayerTransform();
    }

    void Update()
    {
        if (_triggered) return;
        if (_gameManager == null)
            _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (_gameManager == null) return;

        if (_playerTransform == null)
            _playerTransform = FindPlayerTransform();
        if (_playerTransform == null) return;

        var playerPos = _playerTransform.position;

        if (!IsPlayerInCompletionZone(playerPos))
            return;

        TriggerShelterReached();
    }

    void OnTriggerEnter(Collider other)
    {
        HandleTriggerEnter(other);
    }

    /// <summary>Called from <see cref="ShelterTriggerVolumeRelay"/> on the child volume.</summary>
    public void NotifyTriggerFromVolume(Collider other)
    {
        HandleTriggerEnter(other);
    }

    private void HandleTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (_gameManager == null)
            _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (_gameManager == null) return;
        if (!IsPlayerCollider(other)) return;

        var playerPos = other.transform.position;
        if (!IsPlayerInCompletionZone(playerPos))
            return;

        TriggerShelterReached();
    }

    private void TriggerShelterReached()
    {
        if (_triggered) return;
        if (_gameManager == null) return;

        if (_gameManager.ReachShelter())
            _triggered = true;
    }

    /// <summary>
    /// If <see cref="undergroundCompletionPoint"/> is set, only distance to that point matters (best for “only when down in the mamad”).
    /// Otherwise uses trigger bounds + optional depth.
    /// </summary>
    private bool IsPlayerInCompletionZone(Vector3 playerWorldPosition)
    {
        if (undergroundCompletionPoint != null)
            return Vector3.Distance(playerWorldPosition, undergroundCompletionPoint.position) <= completionRadius;

        if (_triggerCollider == null)
            _triggerCollider = ResolveTriggerCollider();
        if (_triggerCollider == null)
            return false;

        if (!_triggerCollider.bounds.Contains(playerWorldPosition))
            return false;

        return IsPlayerDeepEnough(playerWorldPosition);
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag(playerTag)) return true;

        var t = other.transform;
        while (t != null)
        {
            if (t.CompareTag(playerTag)) return true;
            t = t.parent;
        }

        return false;
    }

    private Transform FindPlayerTransform()
    {
        var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            if (t != null && t.CompareTag(playerTag))
                return t;
        }

        var fps = FindFirstObjectByType<SimpleFPSController>(FindObjectsInactive.Include);
        if (fps != null)
            return fps.transform;

        var cc = FindFirstObjectByType<CharacterController>(FindObjectsInactive.Include);
        if (cc != null)
            return cc.transform;

        return null;
    }

    private bool IsPlayerDeepEnough(Vector3 playerWorldPosition)
    {
        if (minimumDepthBelowMamadOrigin <= 0f)
            return true;

        float depthAlongDown = Vector3.Dot(playerWorldPosition - transform.position, -transform.up);
        return depthAlongDown >= minimumDepthBelowMamadOrigin;
    }

    private Collider ResolveTriggerCollider()
    {
        var child = transform.Find(VolumeChildName);
        if (child != null)
        {
            var box = child.GetComponent<BoxCollider>();
            if (box != null)
                return box;
        }

        return GetComponent<Collider>();
    }

    private void EnsureTriggerCollider()
    {
        if (!autoCreateTriggerCollider)
        {
            var c = GetComponent<Collider>() ?? GetComponentInChildren<Collider>(true);
            if (c != null)
                c.isTrigger = true;
            return;
        }

        var volumeT = transform.Find(VolumeChildName);
        GameObject volumeGo;
        if (volumeT == null)
        {
            volumeGo = new GameObject(VolumeChildName);
            volumeGo.transform.SetParent(transform, false);
            volumeGo.transform.localRotation = Quaternion.identity;
            volumeGo.transform.localScale = Vector3.one;
        }
        else
        {
            volumeGo = volumeT.gameObject;
        }

        volumeGo.transform.localPosition = volumeLocalPosition;

        var boxCol = volumeGo.GetComponent<BoxCollider>();
        if (boxCol == null)
            boxCol = volumeGo.AddComponent<BoxCollider>();

        boxCol.center = triggerCenter;
        boxCol.size = autoTriggerSize;
        boxCol.isTrigger = true;

        var relay = volumeGo.GetComponent<ShelterTriggerVolumeRelay>();
        if (relay == null)
            relay = volumeGo.AddComponent<ShelterTriggerVolumeRelay>();
        relay.Bind(this);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (undergroundCompletionPoint != null)
        {
            Gizmos.color = new Color(0f, 1f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(undergroundCompletionPoint.position, completionRadius);
        }
    }
#endif
}
