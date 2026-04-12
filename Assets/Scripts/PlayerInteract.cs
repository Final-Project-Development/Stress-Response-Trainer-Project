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
            // try hit
            PickUpItem item = hit.collider.GetComponent<PickUpItem>();
            if (item != null)
            {
                item.OnPickUp();
                return;
            }

            Door door = hit.collider.GetComponent<Door>();
            if (door != null)
            {
                door.ToggleDoor();
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
}
