using UnityEngine;

public class PickUpItem : MonoBehaviour
{
    [SerializeField] private string itemDisplayName = "Item";

    private GameManager gameManager;

    void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
    }

    public void OnPickUp()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

        if (gameManager != null)
            gameManager.AddItem(itemDisplayName);

        // hide or destroy the item
        gameObject.SetActive(false);
    }
}
