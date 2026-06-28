using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Small OpenXR/Quest controller bridge that keeps the existing keyboard-driven game playable in VR.
///
/// Quest mapping used by the patched gameplay scripts:
/// - Right trigger: E / interact
/// - A or X: digit 1
/// - B or Y: digit 0 for phone dialing, digit 2 for treatment
/// - Grip: digit 3 for treatment
/// - Menu button: pause/back
/// - Left stick: move
/// - Right stick horizontal: snap turn
/// </summary>
[DefaultExecutionOrder(-6000)]
public sealed class XRInputBridge : MonoBehaviour
{
    private static XRInputBridge instance;
    private static readonly InputFeatureUsage<Vector3> PointerPositionUsage = new InputFeatureUsage<Vector3>("pointerPosition");
    private static readonly InputFeatureUsage<Quaternion> PointerRotationUsage = new InputFeatureUsage<Quaternion>("pointerRotation");

    public static bool IsVrActive => instance != null && instance.vrActive;
    public static bool InteractPressed => instance != null && instance.interactDown;
    public static bool Digit1Pressed => instance != null && instance.digit1Down;
    public static bool Digit0Pressed => instance != null && instance.digit0Down;
    public static bool Digit2Pressed => instance != null && instance.digit2Down;
    public static bool Digit3Pressed => instance != null && instance.digit3Down;
    public static bool PausePressed => instance != null && instance.pauseDown;
    public static bool HelpPressed => instance != null && instance.helpDown;
    public static Vector2 MoveAxis => instance != null ? instance.moveAxis : Vector2.zero;
    public static Vector2 TurnAxis => instance != null ? instance.turnAxis : Vector2.zero;

    public static bool TryGetLocalHandPose(XRNode handNode, out Vector3 localPosition, out Quaternion localRotation)
    {
        if (instance != null)
            return instance.TryGetHandPose(handNode, out localPosition, out localRotation);

        localPosition = default;
        localRotation = Quaternion.identity;
        return false;
    }

    public static bool TryGetLocalHandPointerPose(XRNode handNode, out Vector3 localPosition, out Quaternion localRotation)
    {
        if (instance != null)
            return instance.TryGetHandPointerPose(handNode, out localPosition, out localRotation);

        localPosition = default;
        localRotation = Quaternion.identity;
        return false;
    }

    private readonly List<InputDevice> leftDevices = new List<InputDevice>(4);
    private readonly List<InputDevice> rightDevices = new List<InputDevice>(4);
    private readonly List<InputDevice> headDevices = new List<InputDevice>(2);

    private bool vrActive;
    private Vector2 moveAxis;
    private Vector2 turnAxis;

    private bool interact, prevInteract, interactDown;
    private bool digit1, prevDigit1, digit1Down;
    private bool digit0, prevDigit0, digit0Down;
    private bool digit2, prevDigit2, digit2Down;
    private bool digit3, prevDigit3, digit3Down;
    private bool pause, prevPause, pauseDown;
    private bool help, prevHelp, helpDown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        var go = new GameObject("XR Input Bridge");
        go.AddComponent<XRInputBridge>();
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
    }

    private void Update()
    {
        prevInteract = interact;
        prevDigit1 = digit1;
        prevDigit0 = digit0;
        prevDigit2 = digit2;
        prevDigit3 = digit3;
        prevPause = pause;
        prevHelp = help;

        RefreshDevices();

        InputDevice left = FirstValid(leftDevices);
        InputDevice right = FirstValid(rightDevices);

        moveAxis = ReadAxis(left, CommonUsages.primary2DAxis);
        turnAxis = ReadAxis(right, CommonUsages.primary2DAxis);

        interact = ReadButton(right, CommonUsages.triggerButton) || ReadAnalogButton(right, CommonUsages.trigger, 0.75f);
        digit1 = ReadButton(right, CommonUsages.primaryButton) || ReadButton(left, CommonUsages.primaryButton);
        digit0 = ReadButton(right, CommonUsages.secondaryButton) || ReadButton(left, CommonUsages.secondaryButton);
        digit2 = digit0;
        digit3 = ReadButton(right, CommonUsages.gripButton) || ReadButton(left, CommonUsages.gripButton) || ReadAnalogButton(right, CommonUsages.grip, 0.75f) || ReadAnalogButton(left, CommonUsages.grip, 0.75f);
        pause = ReadButton(left, CommonUsages.menuButton) || ReadButton(right, CommonUsages.menuButton);
        help = digit1 && digit0;

        interactDown = interact && !prevInteract;
        digit1Down = digit1 && !prevDigit1;
        digit0Down = digit0 && !prevDigit0;
        digit2Down = digit2 && !prevDigit2;
        digit3Down = digit3 && !prevDigit3;
        pauseDown = pause && !prevPause;
        helpDown = help && !prevHelp;
    }

    private void RefreshDevices()
    {
        leftDevices.Clear();
        rightDevices.Clear();
        headDevices.Clear();

        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftDevices);
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightDevices);
        InputDevices.GetDevicesAtXRNode(XRNode.Head, headDevices);

        vrActive = XRSettings.enabled && (XRSettings.isDeviceActive || FirstValid(headDevices).isValid || FirstValid(leftDevices).isValid || FirstValid(rightDevices).isValid);
    }

    private static InputDevice FirstValid(List<InputDevice> devices)
    {
        if (devices == null)
            return default;

        for (int i = 0; i < devices.Count; i++)
        {
            if (devices[i].isValid)
                return devices[i];
        }

        return default;
    }

    private static bool ReadButton(InputDevice device, InputFeatureUsage<bool> usage)
    {
        return device.isValid && device.TryGetFeatureValue(usage, out bool value) && value;
    }

    private static bool ReadAnalogButton(InputDevice device, InputFeatureUsage<float> usage, float threshold)
    {
        return device.isValid && device.TryGetFeatureValue(usage, out float value) && value >= threshold;
    }

    private static Vector2 ReadAxis(InputDevice device, InputFeatureUsage<Vector2> usage)
    {
        if (device.isValid && device.TryGetFeatureValue(usage, out Vector2 value))
            return value;
        return Vector2.zero;
    }

    private bool TryGetHandPose(XRNode handNode, out Vector3 localPosition, out Quaternion localRotation)
    {
        localPosition = default;
        localRotation = Quaternion.identity;

        if (!vrActive)
            return false;

        List<InputDevice> devices = handNode == XRNode.LeftHand ? leftDevices : rightDevices;
        devices.Clear();
        InputDevices.GetDevicesAtXRNode(handNode, devices);

        InputDevice device = FirstValid(devices);
        if (device.isValid)
        {
            bool havePos = device.TryGetFeatureValue(CommonUsages.devicePosition, out localPosition);
            bool haveRot = device.TryGetFeatureValue(CommonUsages.deviceRotation, out localRotation);
            if (havePos || haveRot)
                return true;
        }

        localPosition = InputTracking.GetLocalPosition(handNode);
        localRotation = InputTracking.GetLocalRotation(handNode);
        return localPosition.sqrMagnitude > 0.0001f || localRotation != Quaternion.identity;
    }

    private bool TryGetHandPointerPose(XRNode handNode, out Vector3 localPosition, out Quaternion localRotation)
    {
        localPosition = default;
        localRotation = Quaternion.identity;

        if (!vrActive)
            return false;

        List<InputDevice> devices = handNode == XRNode.LeftHand ? leftDevices : rightDevices;
        devices.Clear();
        InputDevices.GetDevicesAtXRNode(handNode, devices);

        InputDevice device = FirstValid(devices);
        if (device.isValid)
        {
            bool havePointerPos = device.TryGetFeatureValue(PointerPositionUsage, out localPosition);
            bool havePointerRot = device.TryGetFeatureValue(PointerRotationUsage, out localRotation);
            if (havePointerPos || havePointerRot)
                return true;
        }

        return TryGetHandPose(handNode, out localPosition, out localRotation);
    }
}
