using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimpleFPSController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float mouseSensitivity = 2f;
    public float gravity = -9.81f;
    public Transform cameraTransform;

    /// <summary>When true, movement is blocked for UI/menu phases.</summary>
    [SerializeField] bool uiMenuMode = true;
    [Tooltip("Allow head/camera rotation during UI phases (keeps VR-like spatial feel without enabling movement).")]
    [SerializeField] bool allowLookWhileInUiMenus = true;
    [Tooltip("If true, looking in UI mode requires holding the modifier key (default: Mouse1).")]
    [SerializeField] bool requireLookModifierInUiMenus = false;
    [Tooltip("Look modifier key when requireLookModifierInUiMenus is enabled.")]
    [SerializeField] KeyCode uiMenuLookModifier = KeyCode.Mouse1;
    [Tooltip("Hold Alt during UI mode to temporarily free the cursor for button clicks.")]
    [SerializeField] bool holdAltToUseCursorInUiMenus = true;

    private CharacterController controller;
    private float verticalVelocity;
    private float xRotation;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    /// <summary>
    /// Snap position and yaw after spawn / scene transitions. Resets vertical look so the camera matches the new heading.
    /// CharacterController is briefly disabled so the move is not overridden.
    /// </summary>
    public void TeleportTo(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        bool wasEnabled = controller != null && controller.enabled;
        if (controller != null)
            controller.enabled = false;

        transform.SetPositionAndRotation(worldPosition, worldRotation);
        xRotation = 0f;
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.identity;

        if (controller != null)
            controller.enabled = wasEnabled;
    }

    /// <summary>Called by <see cref="TrainingFlowController"/> so menu buttons work with the mouse.</summary>
    public void SetUiMenuMode(bool menusOpen)
    {
        uiMenuMode = menusOpen;
    }

    void Update()
    {
        if (uiMenuMode)
        {
            HandleUiMenuLookMode();
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        HandleMouseLook();
        HandleMovement();
    }

    private void HandleUiMenuLookMode()
    {
        bool forceCursor =
            holdAltToUseCursorInUiMenus &&
            (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));

        bool lookNow = allowLookWhileInUiMenus &&
                       !forceCursor &&
                       (!requireLookModifierInUiMenus || Input.GetKey(uiMenuLookModifier));

        if (lookNow)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            HandleMouseLook();
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = transform.right * h + transform.forward * v;
        controller.Move(move * moveSpeed * Time.deltaTime);

        if (controller.isGrounded && verticalVelocity < 0)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;
        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }
}
