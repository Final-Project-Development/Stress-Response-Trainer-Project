using UnityEngine;

public class Door : MonoBehaviour
{
    public Animator animator;
    
    private bool isOpen = false;

    void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    public void ToggleDoor()
    {
        isOpen = !isOpen;
        if (animator != null)
        {
            animator.SetBool("isOpen", isOpen);
        }
        else
        {
            Debug.LogWarning("Animator not assigned on Door.");
        }
    }
}
