using UnityEngine;

public class PickUpItem : MonoBehaviour
{
    public enum PickUpMode
    {
        /// <summary>Simulation 1 — collect item and hide it.</summary>
        HideAfterPickup,
        /// <summary>Simulation 2 — lift only the phone receiver, then dial 101.</summary>
        LiftPhoneReceiver
    }

    [SerializeField] private string itemDisplayName = "Item";
    [SerializeField] private PickUpMode pickUpMode = PickUpMode.HideAfterPickup;

    private GameManager gameManager;
    private bool _pickedUp;

    public string ItemDisplayName => itemDisplayName;
    public bool IsPhoneReceiverPickup => pickUpMode == PickUpMode.LiftPhoneReceiver;
    public bool WasPickedUp => _pickedUp;

    public void Configure(string displayName)
    {
        itemDisplayName = displayName;
    }

    public void ConfigureForPhoneReceiver(string displayName = "Receiver")
    {
        itemDisplayName = displayName;
        pickUpMode = PickUpMode.LiftPhoneReceiver;
        _pickedUp = false;
    }

    void Awake()
    {
        if (pickUpMode != PickUpMode.HideAfterPickup)
            return;

        if (GetComponentInParent<PublicPhoneBoothMission>(true) == null)
            return;

        if (transform.name.IndexOf("Receiver", System.StringComparison.OrdinalIgnoreCase) >= 0
            || transform.name.IndexOf("ReceiverInteract", System.StringComparison.OrdinalIgnoreCase) >= 0)
            ConfigureForPhoneReceiver("Receiver");
    }

    void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
    }

    public void OnPickUp()
    {
        if (pickUpMode == PickUpMode.LiftPhoneReceiver)
        {
            var booth = GetComponentInParent<PublicPhoneBoothMission>(true);
            if (booth != null && booth.TryPickupReceiver())
                _pickedUp = true;
            return;
        }

        if (_pickedUp)
            return;

        if (GetComponentInParent<PublicPhoneBoothMission>(true) != null)
            return;

        var firstAidKit = GetComponent<FirstAidKitPickup>();
        if (firstAidKit != null)
        {
            firstAidKit.OnPickUp();
            return;
        }

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

        if (gameManager != null)
            gameManager.AddItem(itemDisplayName);

        gameObject.SetActive(false);
    }
}
