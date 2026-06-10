using UnityEngine;

/// <summary>
/// Marks a possible wounded-person position for Simulation 2.
/// Child LabelAnchor / ViewAnchor define the label and tour stand for this spot.
/// </summary>
public class WoundedSpawnPointMarker : MonoBehaviour
{
    public Color gizmoColor = new Color(1f, 0.35f, 0.2f, 0.85f);
    public float gizmoRadius = 0.35f;

    [Tooltip("Optional override. If empty, uses child named LabelAnchor.")]
    public Transform labelAnchor;

    [Tooltip("Optional override. If empty, uses child named ViewAnchor.")]
    public Transform viewAnchor;

    public bool TryGetLabelAnchor(out Transform anchor)
    {
        anchor = ResolveAnchor(ref labelAnchor, WorldItemLabel.LabelAnchorName);
        return anchor != null;
    }

    public bool TryGetViewAnchor(out Transform anchor)
    {
        anchor = ResolveAnchor(ref viewAnchor, WorldItemLabel.ViewAnchorName);
        return anchor != null;
    }

    Transform ResolveAnchor(ref Transform cached, string anchorName)
    {
        if (cached != null)
            return cached;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name == anchorName)
            {
                cached = child;
                return cached;
            }
        }

        return null;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, gizmoRadius);
        Gizmos.DrawRay(transform.position, transform.forward * 1.2f);

        if (TryGetLabelAnchor(out Transform label))
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.35f, 0.9f);
            Gizmos.DrawSphere(label.position, 0.15f);
        }

        if (TryGetViewAnchor(out Transform view))
        {
            Gizmos.color = new Color(0.3f, 0.55f, 1f, 0.9f);
            Gizmos.DrawSphere(view.position, 0.12f);
            Gizmos.DrawRay(view.position, view.forward * 0.9f);
        }
    }
}
