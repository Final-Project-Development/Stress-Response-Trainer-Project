#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Loads prefab preview sprites in the Unity Editor (edit mode + play mode).</summary>
public static class MissionBriefingEditorIconLoader
{
    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static Sprite GetSpriteForEntry(MissionBriefingCatalog.Entry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.PrefabAssetPath))
        {
            var sprite = GetPrefabPreviewSprite(entry.PrefabAssetPath);
            if (sprite != null)
                return sprite;
        }

        if (!string.IsNullOrWhiteSpace(entry.SceneObjectName))
            return GetSceneObjectPreviewSprite(entry.SceneObjectName);

        return null;
    }

    public static Sprite GetPrefabPreviewSprite(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        if (Cache.TryGetValue(assetPath, out var cached) && cached != null)
            return cached;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
            return null;

        var texture = GetAssetPreviewTexture(prefab);
        if (texture == null)
            return null;

        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        Cache[assetPath] = sprite;
        return sprite;
    }

    public static Sprite GetSceneObjectPreviewSprite(string sceneObjectName)
    {
        var key = $"scene:{sceneObjectName}";
        if (Cache.TryGetValue(key, out var cached) && cached != null)
            return cached;

        var go = FindSceneObject(sceneObjectName);
        if (go == null)
            return null;

        var texture = GetAssetPreviewTexture(go);
        if (texture == null)
            return null;

        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        Cache[key] = sprite;
        return sprite;
    }

    static Texture2D GetAssetPreviewTexture(Object target)
    {
        AssetPreview.SetPreviewTextureCacheSize(256);

        Texture2D preview = null;
        for (int i = 0; i < 40; i++)
        {
            preview = AssetPreview.GetAssetPreview(target);
            if (preview != null)
                break;

            preview = AssetPreview.GetMiniThumbnail(target);
            if (preview != null)
                break;

            System.Threading.Thread.Sleep(50);
        }

        return preview;
    }

    static GameObject FindSceneObject(string objectName)
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            var found = FindDeepChild(root.transform, objectName);
            if (found != null)
                return found.gameObject;
        }
        return null;
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindDeepChild(parent.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }

    public static void ClearCache() => Cache.Clear();

    public static Sprite SaveSpriteToPng(Sprite sprite, string savePath)
    {
        if (sprite == null || sprite.texture == null)
            return null;

        var readable = GetReadableTexture(sprite.texture);
        if (readable == null)
            return null;

        var rect = sprite.textureRect;
        var pixels = readable.GetPixels(
            Mathf.RoundToInt(rect.x),
            Mathf.RoundToInt(rect.y),
            Mathf.RoundToInt(rect.width),
            Mathf.RoundToInt(rect.height));

        var tex = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply();

        var folder = System.IO.Path.GetDirectoryName(savePath)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
        {
            var parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(parent));
            AssetDatabase.CreateFolder(parent, leaf);
        }

        System.IO.File.WriteAllBytes(savePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(readable);

        AssetDatabase.ImportAsset(savePath);
        var importer = AssetImporter.GetAtPath(savePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(savePath);
    }

    static Texture2D GetReadableTexture(Texture2D source)
    {
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }
}
#endif
