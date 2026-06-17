#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MissionBriefingAssignThumbnailIcons
{
    [MenuItem("Tools/Stress Trainer/Apply Briefing Thumbnail Icons (Sim1 + Sim2)")]
    public static void ApplyAllBriefingThumbnailIcons()
    {
        EnsureThumbnailImportSettings();

        var assigned = 0;
        assigned += ApplyForPanel("Sim1Briefing_Panel", MissionBriefingCatalog.Simulation.Simulation1);
        assigned += ApplyForPanel("Sim2Briefing_Panel", MissionBriefingCatalog.Simulation.Simulation2);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Stress Trainer", $"Applied thumbnail icons to {assigned} briefing cards.", "OK");
    }

    static void EnsureThumbnailImportSettings()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Briefing/Thumbnails" });
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            var changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }
    }

    static int ApplyForPanel(string panelName, MissionBriefingCatalog.Simulation simulation)
    {
        var panel = GameObject.Find(panelName);
        if (panel == null)
            return 0;

        var cards = panel.GetComponentsInChildren<MissionBriefingItemCard>(true);
        var assigned = 0;

        for (int i = 0; i < cards.Length; i++)
        {
            var card = cards[i];
            var key = !string.IsNullOrWhiteSpace(card.itemKeyOverride)
                ? card.itemKeyOverride.Trim()
                : card.gameObject.name.Trim();

            if (!MissionBriefingCatalog.TryGet(simulation, key, out _))
                continue;

            var sprite = MissionBriefingCatalog.LoadThumbnail(simulation, key);
            if (sprite == null)
            {
                Debug.LogWarning($"MissionBriefingAssignThumbnailIcons: no sprite for {panelName}/{key}");
                continue;
            }

            card.useManualIcon = true;
            card.iconOverride = sprite;
            card.EnsureReferencesForEditor();
            if (card.iconImage != null)
            {
                card.iconImage.sprite = sprite;
                card.iconImage.preserveAspect = true;
                card.iconImage.color = Color.white;
                card.iconImage.enabled = true;
                EditorUtility.SetDirty(card.iconImage);
            }

            EditorUtility.SetDirty(card);
            assigned++;
        }

        return assigned;
    }
}
#endif
