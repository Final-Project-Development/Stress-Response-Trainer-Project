#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Renders a prefab/model thumbnail and saves it as a UI sprite.</summary>
public static class MissionBriefingPrefabIconCapture
{
    const int ThumbnailSize = 256;
    const int PreviewLayer = 31;

    public static Sprite CaptureAndSaveSprite(string assetPath, string savePath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        var source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (source == null)
        {
            Debug.LogWarning($"MissionBriefingPrefabIconCapture: asset not found at {assetPath}");
            return null;
        }

        var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (instance == null)
            instance = Object.Instantiate(source);

        if (instance == null)
            return null;

        SetLayerRecursively(instance, PreviewLayer);
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;

        var bounds = CalculateWorldBounds(instance);
        if (bounds.size.sqrMagnitude < 0.0001f)
        {
            Object.DestroyImmediate(instance);
            return null;
        }

        var camGo = new GameObject("BriefingPrefabThumbnailCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
        cam.orthographic = true;
        cam.cullingMask = 1 << PreviewLayer;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;

        var center = bounds.center;
        var extent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z, 0.2f);
        cam.orthographicSize = extent * 1.2f;
        camGo.transform.position = center + new Vector3(extent * 0.8f, extent * 0.55f, -extent * 2.4f);
        camGo.transform.LookAt(center);

        var rt = new RenderTexture(ThumbnailSize, ThumbnailSize, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(ThumbnailSize, ThumbnailSize, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, ThumbnailSize, ThumbnailSize), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        cam.targetTexture = null;
        rt.Release();
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(instance);
        Object.DestroyImmediate(rt);

        EnsureFolder(Path.GetDirectoryName(savePath)?.Replace('\\', '/'));
        File.WriteAllBytes(savePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

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

    public static Sprite CaptureSceneObjectSprite(string sceneObjectName, string savePath)
    {
        var target = FindSceneObject(sceneObjectName);
        if (target == null)
            return null;

        var bounds = CalculateWorldBounds(target);
        if (bounds.size.sqrMagnitude < 0.0001f)
            return null;

        var camGo = new GameObject("BriefingSceneThumbnailCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
        cam.orthographic = true;
        cam.cullingMask = ~0;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 200f;

        var center = bounds.center;
        var extent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z, 0.25f);
        cam.orthographicSize = extent * 1.15f;
        camGo.transform.position = center + new Vector3(extent * 0.8f, extent * 0.55f, -extent * 2.4f);
        camGo.transform.LookAt(center);

        var rt = new RenderTexture(ThumbnailSize, ThumbnailSize, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(ThumbnailSize, ThumbnailSize, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, ThumbnailSize, ThumbnailSize), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        cam.targetTexture = null;
        rt.Release();
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(rt);

        EnsureFolder(Path.GetDirectoryName(savePath)?.Replace('\\', '/'));
        File.WriteAllBytes(savePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

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

    static Bounds CalculateWorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.one * 0.5f);

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
            SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
    }

    static void EnsureFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
            return;

        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        var leaf = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
