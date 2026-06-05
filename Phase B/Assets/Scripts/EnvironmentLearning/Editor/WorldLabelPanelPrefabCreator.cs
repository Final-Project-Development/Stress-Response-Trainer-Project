#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class WorldLabelPanelPrefabCreator
{
    const string PrefabPath = "Assets/Prefabs/EnvironmentLearning/WorldLabelPanel.prefab";

    [MenuItem("Tools/Stress Trainer/Create World Label Panel Prefab")]
    public static void CreatePrefab()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/EnvironmentLearning");

        var root = new GameObject("WorldLabelPanel");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

        var canvasRect = root.GetComponent<RectTransform>();
        canvasRect.sizeDelta = WorldLabelAppearance.PanelSize;
        canvasRect.localScale = Vector3.one * WorldLabelAppearance.DefaultWorldScale;

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(root.transform, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var image = panelGo.AddComponent<Image>();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WorldLabelAppearance.PanelSpritePath);
        image.sprite = sprite;
        image.color = WorldLabelAppearance.PanelColor;
        image.raycastTarget = false;

        var textGo = new GameObject("LabelText");
        textGo.transform.SetParent(panelGo.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 8f);
        textRect.offsetMax = new Vector2(-12f, -8f);

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "Mamad";
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        root.AddComponent<WorldLabelBillboard>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"Created {PrefabPath}. Assign on WorldItemLabel → Label Panel Prefab.");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        var name = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
