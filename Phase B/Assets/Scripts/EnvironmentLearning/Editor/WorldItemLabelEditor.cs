#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WorldItemLabel))]
public class WorldItemLabelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var label = (WorldItemLabel)target;
        EditorGUILayout.Space(8);

        if (GUILayout.Button("Create / Select Label Anchor (move in Scene for exact position)"))
            label.EnsureLabelAnchor();

        if (GUILayout.Button("Create / Select View Anchor (stand here + rotate to face item)"))
            label.EnsureViewAnchor();

        if (GUILayout.Button("Remove duplicate anchors on this item"))
            label.PruneDuplicateAnchorsForItem();

        if (GUILayout.Button("Rebuild label preview"))
            label.EnsureLabelBuilt();
    }

    void OnSceneGUI()
    {
        var label = (WorldItemLabel)target;
        label.DrawSceneHandles();
    }
}
#endif
