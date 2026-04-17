using UnityEditor;
using UnityEngine;

public class RoadsRootAlignerWindow : EditorWindow
{
    private const string DefaultRootName = "Roads_Root";

    private string rootObjectName = DefaultRootName;
    private float gridSize = 5f;
    private float fixedY = 0f;
    private bool alignPosition = true;
    private bool alignRotation = true;
    private bool useFixedY = false;
    private bool includeInactive = true;

    [MenuItem("Tools/Streets/Align Roads Root Children")]
    private static void Open()
    {
        GetWindow<RoadsRootAlignerWindow>("Road Aligner");
    }

    private void OnGUI()
    {
        GUILayout.Label("Align Existing Roads", EditorStyles.boldLabel);
        rootObjectName = EditorGUILayout.TextField("Root Name", rootObjectName);
        gridSize = EditorGUILayout.FloatField("Grid Size", gridSize);
        useFixedY = EditorGUILayout.Toggle("Force Y", useFixedY);
        if (useFixedY)
        {
            fixedY = EditorGUILayout.FloatField("Fixed Y", fixedY);
        }

        alignPosition = EditorGUILayout.Toggle("Snap Position", alignPosition);
        alignRotation = EditorGUILayout.Toggle("Snap Rotation (90 deg)", alignRotation);
        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);

        EditorGUILayout.HelpBox("This tool only repositions/rotates children already under Roads_Root. It does not create or delete objects.", MessageType.Info);

        using (new EditorGUI.DisabledScope(gridSize <= 0f || (!alignPosition && !alignRotation)))
        {
            if (GUILayout.Button("Align Children"))
            {
                AlignChildren();
            }
        }
    }

    private void AlignChildren()
    {
        GameObject root = GameObject.Find(rootObjectName);
        if (root == null)
        {
            Debug.LogError($"Aligner: Could not find root object '{rootObjectName}'.");
            return;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(includeInactive);
        int alignedCount = 0;

        Undo.RegisterFullObjectHierarchyUndo(root, "Align Roads Root Children");

        for (int i = 0; i < children.Length; i++)
        {
            Transform t = children[i];
            if (t == root.transform)
            {
                continue;
            }

            bool changed = false;

            if (alignPosition)
            {
                Vector3 p = t.position;
                p.x = SnapToGrid(p.x, gridSize);
                p.z = SnapToGrid(p.z, gridSize);
                if (useFixedY)
                {
                    p.y = fixedY;
                }

                if (t.position != p)
                {
                    t.position = p;
                    changed = true;
                }
            }

            if (alignRotation)
            {
                Vector3 e = t.eulerAngles;
                e.y = SnapToRightAngle(e.y);
                e.x = 0f;
                e.z = 0f;

                if (t.eulerAngles != e)
                {
                    t.rotation = Quaternion.Euler(e);
                    changed = true;
                }
            }

            if (changed)
            {
                alignedCount++;
                EditorUtility.SetDirty(t);
            }
        }

        EditorUtility.SetDirty(root);
        Debug.Log($"Aligner: aligned {alignedCount} child objects under '{rootObjectName}'.");
    }

    private static float SnapToGrid(float value, float step)
    {
        return Mathf.Round(value / step) * step;
    }

    private static float SnapToRightAngle(float angle)
    {
        return Mathf.Round(angle / 90f) * 90f;
    }
}
