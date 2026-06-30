using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// Runtime Quest/OpenXR adapter for the existing first-person project.
/// It does not require XR Interaction Toolkit: it tracks the HMD through UnityEngine.XR,
/// moves the existing CharacterController with Quest sticks, converts screen canvases for VR,
/// and disables mouse-look while a headset is active.
/// </summary>
[DefaultExecutionOrder(-5000)]
public sealed class QuestVrRigBridge : MonoBehaviour
{
    private static QuestVrRigBridge instance;

    [Header("Movement")]
    public float vrMoveSpeed = 2.4f;
    public float gravity = -9.81f;
    public float fallbackEyeHeight = 1.65f;
    public float stickDeadZone = 0.18f;

    [Header("Turning")]
    public bool snapTurn = true;
    public float snapTurnDegrees = 35f;
    public float snapTurnCooldownSeconds = 0.22f;
    public float smoothTurnDegreesPerSecond = 90f;

    [Header("VR UI")]
    public float canvasPlaneDistance = 2.0f;
    public float canvasRefreshSeconds = 0.15f;

    public static QuestVrRigBridge Instance => instance;

    private TrainingFlowController flow;
    private UINavigationManager navigation;
    private SimpleFPSController desktopController;
    private CharacterController characterController;
    private Transform playerRoot;
    private Camera viewCamera;
    private Transform cameraTransform;

    private bool desktopControllerWasEnabled;
    private bool desktopControllerOverridden;
    private float verticalVelocity;
    private float nextSnapTurnTime;
    private float nextCanvasRefreshTime;

    private readonly List<InputDevice> headDevices = new List<InputDevice>(2);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        var go = new GameObject("Quest VR Rig Bridge");
        go.AddComponent<QuestVrRigBridge>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        BindSceneObjects();
        if (XRInputBridge.IsVrActive)
            RefreshCanvasesIfNeeded(true);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindSceneObjects();
        if (XRInputBridge.IsVrActive)
            RefreshCanvasesIfNeeded(true);
    }

    private void Update()
    {
        if (!ShouldUseVrGameplayMode())
        {
            BindSceneObjectsIfMissing();
            RestoreDesktopControllerIfNeeded();
            return;
        }

        BindSceneObjectsIfMissing();
        if (characterController == null || playerRoot == null || cameraTransform == null)
            return;

        OverrideDesktopControllerIfNeeded();
        ConfigureCameraForVr();
        ApplyHeadPose();
        RefreshCanvasesIfNeeded(true);
        HandleVrMovement();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
    }

    /// <summary>
    /// Full VR whenever a headset is active (login, menus, gameplay).
    /// Stick movement only during in-world training phases.
    /// </summary>
    private bool ShouldUseVrGameplayMode()
    {
        return XRInputBridge.IsVrActive;
    }

    private void BindSceneObjectsIfMissing()
    {
        if (flow == null || desktopController == null || characterController == null || viewCamera == null || cameraTransform == null)
            BindSceneObjects();
    }

    private void BindSceneObjects()
    {
        flow = TrainingFlowController.Instance != null
            ? TrainingFlowController.Instance
            : FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        navigation = FindFirstObjectByType<UINavigationManager>(FindObjectsInactive.Include);

        if (flow != null && flow.playerFpsController != null)
            desktopController = flow.playerFpsController;
        else
            desktopController = FindFirstObjectByType<SimpleFPSController>(FindObjectsInactive.Include);

        if (desktopController != null)
        {
            characterController = desktopController.GetComponent<CharacterController>();
            playerRoot = desktopController.transform;
            cameraTransform = desktopController.cameraTransform != null
                ? desktopController.cameraTransform
                : Camera.main != null ? Camera.main.transform : null;
        }
        else
        {
            characterController = FindFirstObjectByType<CharacterController>(FindObjectsInactive.Include);
            playerRoot = characterController != null ? characterController.transform : null;
            cameraTransform = Camera.main != null ? Camera.main.transform : null;
        }

        viewCamera = cameraTransform != null
            ? cameraTransform.GetComponent<Camera>()
            : Camera.main;

        if (viewCamera == null)
            viewCamera = FindFirstObjectByType<Camera>(FindObjectsInactive.Include);

        if (viewCamera != null)
            cameraTransform = viewCamera.transform;

        if (navigation != null && desktopController != null)
            navigation.playerController = desktopController;
    }

    private void OverrideDesktopControllerIfNeeded()
    {
        if (desktopController == null || desktopControllerOverridden)
            return;

        desktopControllerWasEnabled = desktopController.enabled;
        desktopController.enabled = false;
        desktopControllerOverridden = true;
    }

    private void RestoreDesktopControllerIfNeeded()
    {
        if (!desktopControllerOverridden || desktopController == null)
            return;

        desktopController.enabled = desktopControllerWasEnabled;
        desktopControllerOverridden = false;
    }

    private void ConfigureCameraForVr()
    {
        if (viewCamera == null)
            return;

        viewCamera.stereoTargetEye = StereoTargetEyeMask.Both;
        viewCamera.nearClipPlane = Mathf.Min(viewCamera.nearClipPlane, 0.05f);

        if (!viewCamera.CompareTag("MainCamera"))
        {
            try { viewCamera.tag = "MainCamera"; }
            catch { /* Ignore duplicate tag issues. */ }
        }
    }

    private void ApplyHeadPose()
    {
        if (!TryGetHeadPose(out Vector3 localPosition, out Quaternion localRotation))
        {
            localPosition = new Vector3(0f, fallbackEyeHeight, 0f);
            localRotation = Quaternion.identity;
        }

        if (localPosition.sqrMagnitude < 0.0001f)
            localPosition = new Vector3(0f, fallbackEyeHeight, 0f);

        cameraTransform.localPosition = localPosition;
        cameraTransform.localRotation = localRotation;
    }

    private bool TryGetHeadPose(out Vector3 localPosition, out Quaternion localRotation)
    {
        localPosition = default;
        localRotation = Quaternion.identity;

        headDevices.Clear();
        InputDevices.GetDevicesAtXRNode(XRNode.Head, headDevices);

        for (int i = 0; i < headDevices.Count; i++)
        {
            InputDevice device = headDevices[i];
            if (!device.isValid)
                continue;

            bool havePos = device.TryGetFeatureValue(CommonUsages.devicePosition, out localPosition);
            bool haveRot = device.TryGetFeatureValue(CommonUsages.deviceRotation, out localRotation);
            if (havePos || haveRot)
                return true;
        }

        localPosition = InputTracking.GetLocalPosition(XRNode.Head);
        localRotation = InputTracking.GetLocalRotation(XRNode.Head);
        return localPosition.sqrMagnitude > 0.0001f || localRotation != Quaternion.identity;
    }

    private void HandleVrMovement()
    {
        if (!CanMoveInCurrentPhase())
            return;

        Vector2 moveAxis = XRInputBridge.MoveAxis;
        if (moveAxis.magnitude < stickDeadZone)
            moveAxis = Vector2.zero;

        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 move = forward * moveAxis.y + right * moveAxis.x;
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        characterController.Move(move * vrMoveSpeed * Time.deltaTime);

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;
        characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);

        Vector2 turnAxis = XRInputBridge.TurnAxis;
        if (Mathf.Abs(turnAxis.x) < 0.65f)
            return;

        if (snapTurn)
        {
            if (Time.unscaledTime < nextSnapTurnTime)
                return;

            float sign = Mathf.Sign(turnAxis.x);
            playerRoot.Rotate(Vector3.up, sign * snapTurnDegrees, Space.World);
            nextSnapTurnTime = Time.unscaledTime + snapTurnCooldownSeconds;
        }
        else
        {
            playerRoot.Rotate(Vector3.up, turnAxis.x * smoothTurnDegreesPerSecond * Time.deltaTime, Space.World);
        }
    }

    private bool CanMoveInCurrentPhase()
    {
        if (flow == null)
            return true;

        if (flow.IsPaused)
            return false;

        if (navigation != null && navigation.IsOverlayUiOpen)
            return false;

        return flow.CurrentPhase == TrainingFlowController.Phase.Simulation1Active ||
               flow.CurrentPhase == TrainingFlowController.Phase.Simulation2Active ||
               flow.CurrentPhase == TrainingFlowController.Phase.EnvironmentLearning;
    }

    public static void ForceRefreshCanvases()
    {
        if (instance == null)
            return;

        instance.BindSceneObjectsIfMissing();
        instance.RefreshCanvasesIfNeeded(true);
    }

    private void RefreshCanvasesIfNeeded(bool force = false)
    {
        if (viewCamera == null)
            return;

        if (!force && Time.unscaledTime < nextCanvasRefreshTime)
            return;

        nextCanvasRefreshTime = Time.unscaledTime + canvasRefreshSeconds;
        VrUiSupport.RefreshAllCanvases(viewCamera, canvasPlaneDistance);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        RestoreDesktopControllerIfNeeded();
    }
}
