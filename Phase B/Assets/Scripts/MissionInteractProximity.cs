using UnityEngine;

/// <summary>
/// Shared checks for whether the player is close enough that pressing E would register on a mission target.
/// </summary>
public static class MissionInteractProximity
{
    public static float GetDefaultInteractDistance()
    {
        var interact = Object.FindFirstObjectByType<PlayerInteract>(FindObjectsInactive.Include);
        return interact != null ? interact.interactDistance : 3f;
    }

    public static float GetWoundedInteractDistance()
    {
        var interact = Object.FindFirstObjectByType<PlayerInteract>(FindObjectsInactive.Include);
        return interact != null ? interact.woundedInteractDistance : 6f;
    }

    public static float GetPhoneBoothInteractDistance()
    {
        var interact = Object.FindFirstObjectByType<PlayerInteract>(FindObjectsInactive.Include);
        return interact != null ? interact.phoneBoothInteractDistance : 5f;
    }

    public static bool CanPressEOn(Camera cam, Component target, float maxDistance, float facingThreshold = 0.35f)
    {
        if (cam == null || target == null)
            return false;

        var colliders = target.GetComponentsInChildren<Collider>(true);
        if (colliders != null && colliders.Length > 0)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col == null || !col.enabled)
                    continue;

                if (IsRaycastHit(cam, col, maxDistance))
                    return true;

                if (IsFacingBounds(cam, col.bounds, maxDistance, facingThreshold))
                    return true;
            }

            return false;
        }

        return IsFacingPoint(cam, target.transform.position, maxDistance, facingThreshold);
    }

    public static bool IsFacingPoint(Camera cam, Vector3 worldPoint, float maxDistance, float facingThreshold)
    {
        if (cam == null)
            return false;

        Vector3 toPoint = worldPoint - cam.transform.position;
        float dist = toPoint.magnitude;
        if (dist > maxDistance)
            return false;

        if (dist <= 0.05f)
            return true;

        return Vector3.Dot(cam.transform.forward, toPoint.normalized) >= facingThreshold;
    }

    public static bool IsFacingBounds(Camera cam, Bounds bounds, float maxDistance, float facingThreshold)
    {
        if (cam == null)
            return false;

        if (bounds.SqrDistance(cam.transform.position) > maxDistance * maxDistance)
            return false;

        Vector3 closest = bounds.ClosestPoint(cam.transform.position + cam.transform.forward * maxDistance);
        return IsFacingPoint(cam, closest, maxDistance, facingThreshold);
    }

    static bool IsRaycastHit(Camera cam, Collider targetCollider, float maxDistance)
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            maxDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);

        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i].collider;
            if (hit == null)
                continue;

            if (hit == targetCollider || IsSameInteractTarget(hit, targetCollider))
                return true;
        }

        return false;
    }

    static bool IsSameInteractTarget(Collider hit, Collider target)
    {
        if (hit == null || target == null)
            return false;

        Transform hitRoot = hit.transform;
        Transform targetRoot = target.transform;
        return hitRoot == targetRoot
            || hitRoot.IsChildOf(targetRoot)
            || targetRoot.IsChildOf(hitRoot);
    }
}
