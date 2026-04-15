using UnityEngine;

public class PickUpItem : MonoBehaviour
{
    [SerializeField] private string itemDisplayName = "Item";

    private GameManager gameManager;

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
    }

    public void OnPickUp()
    {
        if (gameManager != null)
        {
            gameManager.AddItem(itemDisplayName);
        }

        // hide or destroy the item
        gameObject.SetActive(false);
    }
    
}
