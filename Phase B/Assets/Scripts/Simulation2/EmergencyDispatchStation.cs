using UnityEngine;

/// <summary>
/// Legacy entry point — forwards to <see cref="PublicPhoneBoothMission"/> when present.
/// </summary>
public class EmergencyDispatchStation : MonoBehaviour
{
    [SerializeField] PublicPhoneBoothMission phoneBoothMission;

    public void ResetForMission()
    {
        if (phoneBoothMission == null)
            phoneBoothMission = GetComponent<PublicPhoneBoothMission>()
                ?? GetComponentInParent<PublicPhoneBoothMission>()
                ?? GetComponentInChildren<PublicPhoneBoothMission>(true);

        phoneBoothMission?.ResetForMission();
    }

    public void OnInteract(Collider hitCollider = null)
    {
        if (phoneBoothMission == null)
            phoneBoothMission = GetComponent<PublicPhoneBoothMission>()
                ?? GetComponentInParent<PublicPhoneBoothMission>()
                ?? GetComponentInChildren<PublicPhoneBoothMission>(true);

        if (phoneBoothMission != null)
        {
            phoneBoothMission.TryInteract(hitCollider);
            return;
        }

        var ui = EmergencyDispatchUI.Instance
            ?? FindFirstObjectByType<EmergencyDispatchUI>(FindObjectsInactive.Include);
        ui?.OpenPanel();
    }
}
