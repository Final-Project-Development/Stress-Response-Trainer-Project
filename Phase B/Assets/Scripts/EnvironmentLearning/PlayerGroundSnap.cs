using UnityEngine;

/// <summary>
/// Places the player on walkable ground (uses CharacterController capsule dimensions).
/// </summary>
public static class PlayerGroundSnap
{
    public enum SpawnHeightMode
    {
        /// <summary>Spawn transform position = where feet should stand (recommended for EnvironmentLearningSpawn).</summary>
        FeetAtMarker,
        /// <summary>Spawn transform position = CharacterController center.</summary>
        CharacterCenter
    }

    /// <summary>
    /// Stand on ground at marker XZ. Y comes from raycast below marker (ignores marker Y if useGroundHeight is true).
    /// </summary>
    public static bool PlacePlayerAtSpawnMarker(
        Transform playerRoot,
        Transform spawnMarker,
        Quaternion rotation,
        SpawnHeightMode heightMode = SpawnHeightMode.FeetAtMarker,
        bool useGroundHeight = true)
    {
        if (playerRoot == null || spawnMarker == null)
            return false;

        Vector3 pos = spawnMarker.position;
        if (useGroundHeight && TryGetGroundCenterY(pos.x, pos.z, pos.y + 8f, playerRoot, out float groundCenterY))
            pos.y = groundCenterY;
        else if (heightMode == SpawnHeightMode.FeetAtMarker)
            pos.y = FeetPositionToCenterY(pos.y, playerRoot);

        var fps = playerRoot.GetComponent<SimpleFPSController>();
        if (fps != null)
            fps.TeleportTo(pos, rotation);
        else
            playerRoot.SetPositionAndRotation(pos, rotation);

        return true;
    }

    public static bool TrySnapToGround(Transform playerRoot, float rayHeight = 40f, float maxRayDistance = 80f)
    {
        if (playerRoot == null)
            return false;

        if (!TryGetGroundCenterY(
                playerRoot.position.x,
                playerRoot.position.z,
                playerRoot.position.y + rayHeight,
                playerRoot,
                out float centerY,
                maxRayDistance))
        {
            Debug.LogWarning(
                $"PlayerGroundSnap: no ground under {playerRoot.position}. Move spawn onto pavement/terrain.");
            return false;
        }

        ApplyGroundedPosition(playerRoot, centerY);
        return true;
    }

    /// <summary>
    /// Raycast a short distance below a reference height so indoor tour teleports land on the floor, not the roof.
    /// </summary>
    /// <summary>
    /// Teleport to the exact world position and yaw of a manually placed ViewAnchor.
    /// </summary>
    public static bool PlacePlayerAtViewAnchor(Transform playerRoot, Transform viewAnchor)
    {
        if (playerRoot == null || viewAnchor == null)
            return false;

        Quaternion look = ComputeYawRotation(viewAnchor.forward);
        Vector3 pos = viewAnchor.position;

        var fps = playerRoot.GetComponent<SimpleFPSController>();
        if (fps != null)
            fps.TeleportTo(pos, look);
        else
            playerRoot.SetPositionAndRotation(pos, look);

        return true;
    }

    static Quaternion ComputeYawRotation(Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
            return Quaternion.identity;

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    public static bool TrySnapNearReferenceHeight(
        Transform playerRoot,
        float referenceY,
        float rayUp = 2f,
        float rayDown = 5f)
    {
        if (playerRoot == null)
            return false;

        float rayStartY = referenceY + rayUp;
        float maxDistance = rayUp + rayDown;

        if (!TryGetGroundCenterY(
                playerRoot.position.x,
                playerRoot.position.z,
                rayStartY,
                playerRoot,
                out float centerY,
                maxDistance))
            return false;

        ApplyGroundedPosition(playerRoot, centerY);
        return true;
    }

    static void ApplyGroundedPosition(Transform playerRoot, float centerY)
    {
        Vector3 grounded = new Vector3(playerRoot.position.x, centerY, playerRoot.position.z);
        var fps = playerRoot.GetComponent<SimpleFPSController>();
        if (fps != null)
            fps.TeleportTo(grounded, playerRoot.rotation);
        else
            playerRoot.SetPositionAndRotation(grounded, playerRoot.rotation);
    }

    static bool TryGetGroundCenterY(
        float x,
        float z,
        float rayStartY,
        Transform playerRoot,
        out float centerY,
        float maxRayDistance = 120f)
    {
        centerY = 0f;
        Vector3 origin = new Vector3(x, rayStartY, z);

        if (!Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                maxRayDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            if (!Physics.Raycast(
                    origin,
                    Vector3.down,
                    out hit,
                    maxRayDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore))
                return false;
        }

        centerY = hit.point.y + CapsuleBottomToCenterOffset(playerRoot) + 0.05f;
        return true;
    }

    static float FeetPositionToCenterY(float feetY, Transform playerRoot)
    {
        return feetY + CapsuleBottomToCenterOffset(playerRoot);
    }

    static float CapsuleBottomToCenterOffset(Transform playerRoot)
    {
        var cc = playerRoot.GetComponent<CharacterController>();
        if (cc == null)
            return 1f;

        return cc.height * 0.5f + cc.center.y + cc.skinWidth;
    }
}
