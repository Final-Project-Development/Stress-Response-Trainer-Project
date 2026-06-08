using UnityEngine;

/// <summary>
/// Simulation 2 — first aid kit pickup (must be collected before treating the wounded).
/// </summary>
public class FirstAidKitPickup : MonoBehaviour
{
    [SerializeField] private string displayName = "First Aid Kit";

    private GameManager _gameManager;

    void Awake()
    {
        EnsurePickupCollider();
    }

    void OnEnable()
    {
        EnsurePickupCollider();
    }

    void Start()
    {
        _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
    }

    public void EnsurePickupCollider()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider>();

        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            box.center = Vector3.zero;
            box.size = new Vector3(0.6f, 0.4f, 0.4f);
            box.isTrigger = false;
            return;
        }

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            worldBounds.Encapsulate(renderers[i].bounds);

        Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
        Vector3 localSize = transform.InverseTransformVector(worldBounds.size);
        localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

        const float padding = 1.15f;
        box.center = localCenter;
        box.size = localSize * padding;
        box.isTrigger = false;
    }

    public void OnPickUp()
    {
        if (TrainingFlowController.Instance != null &&
            !TrainingFlowController.Instance.AllowsMissionGameplay)
            return;

        if (_gameManager == null)
            _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

        if (_gameManager != null && _gameManager.HasFirstAidKit())
            return;

        if (_gameManager == null)
        {
            Debug.LogWarning("FirstAidKitPickup: GameManager not found — kit state was not saved.");
            return;
        }

        _gameManager.OnFirstAidKitCollected(displayName);
        gameObject.SetActive(false);
    }
}
