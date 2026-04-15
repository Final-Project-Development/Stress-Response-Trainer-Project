using UnityEngine;

/// <summary>
/// Attach to a trigger collider that represents the outdoor shelter (Mamad).
/// When the player enters, marks shelter objective as reached in GameManager.
/// </summary>
public class ShelterTrigger : MonoBehaviour
{
    public string playerTag = "Player";
    public bool autoCreateTriggerCollider = true;
    public Vector3 autoTriggerSize = new Vector3(6f, 3f, 6f);

    private GameManager _gameManager;
    private bool _triggered;

    void Awake()
    {
        EnsureTriggerCollider();
    }

    void Start()
    {
        _gameManager = FindObjectOfType<GameManager>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (_gameManager == null) return;
        if (!other.CompareTag(playerTag)) return;

        _triggered = true;
        _gameManager.ReachShelter();
    }

    private void EnsureTriggerCollider()
    {
        var col = GetComponent<Collider>();
        if (col == null && autoCreateTriggerCollider)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.size = autoTriggerSize;
            col = box;
        }

        if (col != null)
            col.isTrigger = true;
    }
}
