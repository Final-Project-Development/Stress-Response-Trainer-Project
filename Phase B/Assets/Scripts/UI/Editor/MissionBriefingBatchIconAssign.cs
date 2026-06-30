#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Entry point for Unity batch mode icon generation.</summary>
public static class MissionBriefingBatchIconAssign
{
    const string ScenePath = "Assets/Scenes/backup.unity";

    [MenuItem("Tools/Stress Trainer/Assign Icons And Save Scene (Batch)")]
    public static void AssignIconsAndSaveScene()
    {
        if (!System.IO.File.Exists(ScenePath))
        {
            Debug.LogError($"MissionBriefingBatchIconAssign: scene not found at {ScenePath}");
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        MissionBriefingPanelSetupEditor.AssignPrefabIconsToBriefingCardsSilent();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("MissionBriefingBatchIconAssign: icons assigned and scene saved.");
    }

    public static void RunFromCommandLine()
    {
        AssignIconsAndSaveScene();
        EditorApplication.Exit(0);
    }
}
#endif
