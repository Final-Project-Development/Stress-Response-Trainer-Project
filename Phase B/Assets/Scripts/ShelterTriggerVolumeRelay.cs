using UnityEngine;

/// <summary>
/// Lives on the child object that holds the trigger <see cref="BoxCollider"/>.
/// Unity sends trigger callbacks to the GameObject that owns the collider, not the parent.
/// </summary>
[DisallowMultipleComponent]
public class ShelterTriggerVolumeRelay : MonoBehaviour
{
    [SerializeField] private ShelterTrigger owner;

    public void Bind(ShelterTrigger shelterTrigger)
    {
        owner = shelterTrigger;
    }

    void OnTriggerEnter(Collider other)
    {
        if (owner != null)
            owner.NotifyTriggerFromVolume(other);
    }
}
