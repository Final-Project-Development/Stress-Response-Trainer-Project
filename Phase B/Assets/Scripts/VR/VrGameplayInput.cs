using UnityEngine;

/// <summary>
/// Desktop + Quest/OpenXR input in one place. When a headset is active, VR mappings take priority
/// for gameplay actions (see <see cref="XRInputBridge"/>).
/// </summary>
public static class VrGameplayInput
{
    /// <summary>VR controls whenever a headset is active (login, menus, and gameplay).</summary>
    public static bool ShouldUseVrControls =>
        XRInputBridge.IsVrActive;

    /// <summary>True during active in-world movement phases.</summary>
    public static bool IsInVrGameplayPhase(TrainingFlowController flow)
    {
        if (flow == null)
            return false;

        TrainingFlowController.Phase phase = flow.CurrentPhase;
        return phase == TrainingFlowController.Phase.Simulation1Active
            || phase == TrainingFlowController.Phase.Simulation2Active
            || phase == TrainingFlowController.Phase.EnvironmentLearning;
    }

    public static bool InteractPressed =>
        Input.GetKeyDown(KeyCode.E) ||
        (ShouldUseVrControls &&
         XRInputBridge.InteractPressed &&
         VRGazeUiPointer.LastConsumedInteractFrame != Time.frameCount);

    public static bool PausePressed =>
        Input.GetKeyDown(KeyCode.Escape) || (ShouldUseVrControls && XRInputBridge.PausePressed);

    public static bool HelpPressed =>
        Input.GetKeyDown(KeyCode.H) || (ShouldUseVrControls && XRInputBridge.HelpPressed);

    public static bool Digit1Pressed =>
        Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1) ||
        (ShouldUseVrControls && XRInputBridge.Digit1Pressed);

    public static bool Digit0Pressed =>
        Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0) ||
        (ShouldUseVrControls && XRInputBridge.Digit0Pressed);

    public static bool Digit2Pressed =>
        Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2) ||
        (ShouldUseVrControls && XRInputBridge.Digit2Pressed);

    public static bool Digit3Pressed =>
        Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3) ||
        (ShouldUseVrControls && XRInputBridge.Digit3Pressed);

    public static bool KeyPressed(KeyCode key)
    {
        if (Input.GetKeyDown(key))
            return true;

        if (!ShouldUseVrControls)
            return false;

        if (key == KeyCode.Alpha1 || key == KeyCode.Keypad1)
            return XRInputBridge.Digit1Pressed;
        if (key == KeyCode.Alpha2 || key == KeyCode.Keypad2)
            return XRInputBridge.Digit2Pressed;
        if (key == KeyCode.Alpha3 || key == KeyCode.Keypad3)
            return XRInputBridge.Digit3Pressed;
        if (key == KeyCode.Alpha0 || key == KeyCode.Keypad0)
            return XRInputBridge.Digit0Pressed;

        return false;
    }
}
