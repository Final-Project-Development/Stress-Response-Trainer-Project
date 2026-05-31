using UnityEngine;

/// <summary>
/// Simulation 2 — first aid kit pickup (must be collected before treating the wounded).
/// </summary>
public class FirstAidKitPickup : MonoBehaviour
{
    [SerializeField] private string displayName = "First Aid Kit";

    private GameManager _gameManager;

    void Start()
    {
        _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
    }

    public void OnPickUp()
    {
        if (_gameManager == null)
            _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

        if (_gameManager != null && _gameManager.HasFirstAidKit())
            return;

        _gameManager?.OnFirstAidKitCollected(displayName);
        gameObject.SetActive(false);
    }
}
