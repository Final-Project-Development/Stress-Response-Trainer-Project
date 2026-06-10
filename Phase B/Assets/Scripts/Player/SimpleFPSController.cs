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
    [Tooltip("During active simulation: look with the mouse in the world, click the toolbar when the cursor is over UI.")]
    [SerializeField] bool unlockCursorDuringSimulation = true;
    [Tooltip("Screen rect for the top toolbar — only this area blocks mouse-look for button clicks.")]
    [SerializeField] RectTransform toolbarScreenRegion;
    [Tooltip("Mission status panel (center) — pointer here unlocks cursor for the Hint button.")]
    [SerializeField] RectTransform missionStatusPanelRegion;
    [Tooltip("Environment Learning tour sidebar — pointer here unlocks cursor for item navigation.")]
    [SerializeField] RectTransform learningTourSidebarRegion;

    private CharacterController controller;
    private float verticalVelocity;
    private float xRotation;
    private bool overlayUiOpen;
    private bool simulationToolbarMode;
    private bool learningTourSidebarMode;

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
        verticalVelocity = -2f;
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.identity;

        if (controller != null)
        {
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
        }
    }

    /// <summary>Resets fall speed after teleport (called by ground snap).</summary>
    public void ResetVerticalVelocity()
    {
        verticalVelocity = -2f;
    }

    /// <summary>Called by <see cref="TrainingFlowController"/> so menu buttons work with the mouse.</summary>
    public void SetUiMenuMode(bool menusOpen)
    {
        uiMenuMode = menusOpen;
    }

    /// <summary>Help / pause / confirm panels during an active simulation.</summary>
    public void SetOverlayUiOpen(bool open)
    {
        overlayUiOpen = open;
    }

    /// <summary>Active simulation: mouse-look in the world; toolbar clickable when pointer is over UI.</summary>
    public void SetSimulationToolbarMode(bool enabled)
    {
        simulationToolbarMode = enabled;
    }

    public void SetToolbarScreenRegion(RectTransform region)
    {
        toolbarScreenRegion = region;
    }

    public void SetMissionStatusPanelRegion(RectTransform region)
    {
        missionStatusPanelRegion = region;
    }

    /// <summary>Environment Learning: mouse over the left sidebar unlocks the cursor for navigation clicks.</summary>
    public void SetLearningTourSidebar(RectTransform sidebarRegion, bool active)
    {
        learningTourSidebarRegion = sidebarRegion;
        learningTourSidebarMode = active;
    }

    void Update()
    {
        if (overlayUiOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (uiMenuMode)
        {
            HandleUiMenuLookMode();
            return;
        }

        if (unlockCursorDuringSimulation && simulationToolbarMode)
        {
            bool pointerOverUi = IsPointerOverSimulationUi();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = pointerOverUi;
            HandleMovement();
            if (!pointerOverUi)
                HandleMouseLook();
            return;
        }

        if (learningTourSidebarMode)
        {
            bool pointerOverSidebar = learningTourSidebarRegion != null
                && IsPointerOverRect(learningTourSidebarRegion);
            bool pointerOverToolbar = IsPointerOverToolbar();
            bool pointerOverUi = pointerOverSidebar || pointerOverToolbar;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = pointerOverUi;
            HandleMovement();
            if (!pointerOverUi)
                HandleMouseLook();
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        HandleMouseLook();
        HandleMovement();
    }

    private bool IsPointerOverSimulationUi()
    {
        return IsPointerOverToolbar() || IsPointerOverMissionPanel();
    }

    private bool IsPointerOverToolbar()
    {
        return IsPointerOverRect(toolbarScreenRegion);
    }

    private bool IsPointerOverMissionPanel()
    {
        return IsPointerOverRect(missionStatusPanelRegion);
    }

    private static bool IsPointerOverRect(RectTransform region)
    {
        if (region == null)
            return false;

        var canvas = region.GetComponentInParent<Canvas>();
        var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        return RectTransformUtility.RectangleContainsScreenPoint(
            region,
            Input.mousePosition,
            cam);
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
