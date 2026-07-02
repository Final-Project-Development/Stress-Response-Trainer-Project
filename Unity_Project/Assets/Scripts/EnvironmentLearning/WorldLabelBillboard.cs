using UnityEngine;

/// <summary>
/// Keeps a world-space label facing the main camera (or a custom target).
/// </summary>
public class WorldLabelBillboard : MonoBehaviour
{
    [Tooltip("If empty, uses Camera.main each frame.")]
    public Transform lookTarget;

    [Tooltip("Rotate only around world Y (upright sign).")]
    public bool yawOnly = true;

    void LateUpdate()
    {
        Transform target = lookTarget;
        if (target == null && Camera.main != null)
            target = Camera.main.transform;
        if (target == null)
            return;

        Vector3 toCamera = target.position - transform.position;
        if (toCamera.sqrMagnitude < 0.0001f)
            return;

        if (yawOnly)
        {
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 0.0001f)
                return;
        }

        transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
    }
}
