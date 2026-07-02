#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class QuestBuildSettingsMenu
{
    [MenuItem("Tools/Stress Trainer/Enable PC VR (Quest Link / Cable)")]
    public static void ApplyPcVrLinkSettings()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

        PlayerSettings.companyName = "Stress Response Trainer";
        PlayerSettings.productName = "Stress Response Trainer VR";
        PlayerSettings.colorSpace = ColorSpace.Linear;

        EditorUtility.DisplayDialog(
            "PC VR (Quest Link) ready",
            "Standalone (Windows) is selected for Play Mode and builds.\n\n" +
            "1) Install Meta Quest Link on PC\n" +
            "2) Connect Quest with USB-C cable\n" +
            "3) Enable Link in the headset\n" +
            "4) In Unity: Edit > Project Settings > XR Plug-in Management > Standalone — confirm OpenXR is checked\n" +
            "5) OpenXR settings: Oculus Touch + Meta Quest Touch Plus profiles are enabled\n" +
            "6) Press Play in MainScene — the headset should show the game\n\n" +
            "Controls: Left stick move | Right stick turn | Right Trigger interact | Menu pause | A+X help",
            "OK");
    }

    [MenuItem("Tools/Stress Trainer/Apply Basic Quest Build Settings")]
    public static void ApplyBasicQuestBuildSettings()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        PlayerSettings.companyName = "Stress Response Trainer";
        PlayerSettings.productName = "Stress Response Trainer VR";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.stresstrainer.vr");
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.colorSpace = ColorSpace.Linear;

        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        // Property exists in current Unity versions, but reflection keeps this menu tolerant if Unity changes the API name.
        PropertyInfo forceInternet = typeof(PlayerSettings.Android).GetProperty(
            "forceInternetPermission",
            BindingFlags.Public | BindingFlags.Static);
        forceInternet?.SetValue(null, true);

        EditorUtility.DisplayDialog(
            "Quest settings applied",
            "Basic Android/Quest build settings were applied.\n\nNext: open Edit > Project Settings > XR Plug-in Management, select Android, enable OpenXR, then in OpenXR enable the Meta Quest feature group and Oculus Touch Controller Profile.",
            "OK");
    }
}
#endif
