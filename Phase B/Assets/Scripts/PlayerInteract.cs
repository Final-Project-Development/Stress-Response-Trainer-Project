using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    public float interactDistance = 3f;
    
    void Update()
    {
        if  (Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }
    }
    void TryInteract()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray  = new Ray(cam.transform.position, cam.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            FirstAidKitPickup firstAidKit = hit.collider.GetComponent<FirstAidKitPickup>();
            if (firstAidKit == null)
                firstAidKit = hit.collider.GetComponentInParent<FirstAidKitPickup>();
            if (firstAidKit != null)
            {
                firstAidKit.OnPickUp();
                return;
            }

            PickUpItem item = hit.collider.GetComponent<PickUpItem>();
            if (item == null)
                item = hit.collider.GetComponentInParent<PickUpItem>();
            if (item != null)
            {
                item.OnPickUp();
                return;
            }

            Door door = FindEnabledDoor(hit.collider);
            if (door != null)
            {
                door.ToggleDoor();
                return;
            }

            LightSwitch lightSwitch = hit.collider.GetComponent<LightSwitch>();
            if (lightSwitch != null)
            {
                lightSwitch.OnInteract();
                return;
            }

            // try wounded man
            WoundedMan wonded = hit.collider.GetComponent<WoundedMan>();
            if (wonded != null)
            {
                wonded.OnFirstAid();
                return;
            }
        }
    }

    private static Door FindEnabledDoor(Collider hitCollider)
    {
        var doors = hitCollider.GetComponentsInParent<Door>(true);
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null && doors[i].enabled)
                return doors[i];
        }

        return null;
    }
}
