#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LevelSelectSubtitleSetup
{
    [MenuItem("Tools/Stress Trainer/Apply Level Select Card Subtitles")]
    public static void ApplyLevelSelectCardSubtitles()
    {
        var levelSelect = Object.FindFirstObjectByType<LevelSelectUI>(FindObjectsInactive.Include);
        if (levelSelect == null)
        {
            EditorUtility.DisplayDialog("Stress Trainer", "LevelSelectUI was not found in the open scene.", "OK");
            return;
        }

        levelSelect.ApplyCopy();
        EditorUtility.SetDirty(levelSelect);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Stress Trainer", "Level select card subtitles applied.", "OK");
    }
}
#endif
