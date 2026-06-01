using UnityEngine;

/// <summary>
/// Marks a collider on the UK phone booth for E-interaction. Added automatically by <see cref="PublicPhoneBoothMission"/>.
/// </summary>
public class PhoneBoothInteractPoint : MonoBehaviour
{
    public PublicPhoneBoothMission booth;
    public PublicPhoneBoothMission.BoothAction action;

    public void Initialize(PublicPhoneBoothMission owner, PublicPhoneBoothMission.BoothAction boothAction)
    {
        booth = owner;
        action = boothAction;
    }
}
