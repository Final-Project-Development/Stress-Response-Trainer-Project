using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Shared look for world item name panels (matches EnvironmentLearningHud highlight sprite).
/// </summary>
public static class WorldLabelAppearance
{
    public const string PanelSpritePath =
        "Assets/Space_Exploration_GUI_Kit/Grid_Components/Large/inventory-highlight-large 1.png";

    public static readonly Color PanelColor = Color.white;
    public static readonly Vector2 PanelSize = new Vector2(140f, 44f);
    public const float DefaultWorldScale = 0.006f;

    static Sprite _cachedSprite;

    public static Sprite PanelSprite
    {
        get
        {
            if (_cachedSprite != null)
                return _cachedSprite;

#if UNITY_EDITOR
            _cachedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
#endif
            return _cachedSprite;
        }
    }
}
