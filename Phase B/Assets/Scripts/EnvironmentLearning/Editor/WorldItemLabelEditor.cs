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
