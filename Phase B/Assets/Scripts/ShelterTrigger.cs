using UnityEngine;

/// <summary>
/// Attach to a trigger collider that represents the outdoor shelter (Mamad).
/// When the player enters, marks shelter objective as reached in GameManager.
/// </summary>
public class ShelterTrigger : MonoBehaviour
{
    public string playerTag = "Player";
    public bool autoCreateTriggerCollider = true;
    [Tooltip("World-space box size for the trigger when auto-created.")]
    public Vector3 autoTriggerSize = new Vector3(18f, 14f, 18f);
    [Tooltip("Local-space center offset (often negative Y for stairs / underground entrance).")]
    public Vector3 triggerCenter = new Vector3(0f, -4f, 0f);

    private GameManager _gameManager;
    private bool _triggered;

    void Awake()
    {
        EnsureTriggerCollider();
    }

    void Start()
    {
        _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (_gameManager == null)
            _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (_gameManager == null) return;
        if (!IsPlayerCollider(other)) return;

        _triggered = true;
        _gameManager.ReachShelter();
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

    private void EnsureTriggerCollider()
    {
        var col = GetComponent<Collider>();
        if (col == null && autoCreateTriggerCollider)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = triggerCenter;
            box.size = autoTriggerSize;
            col = box;
        }

        if (col != null)
            col.isTrigger = true;
    }
}
