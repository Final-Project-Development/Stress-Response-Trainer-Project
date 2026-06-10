using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    public float interactDistance = 3f;
    [Tooltip("Longer reach for the wounded casualty (E contact and treatment).")]
    public float woundedInteractDistance = 6f;
    [Tooltip("Longer reach for the UK phone booth (door / coin / handset).")]
    public float phoneBoothInteractDistance = 5f;

    private GameManager _gameManager;

    private PublicPhoneBoothMission[] _phoneBoothCache;

    void Update()
    {
        if (TrainingFlowController.Instance != null &&
            !TrainingFlowController.Instance.AllowsMissionGameplay)
            return;

        PollPhoneBoothDialInput();

        if (Input.GetKeyDown(KeyCode.E))
            TryInteract();
    }

    private void PollPhoneBoothDialInput()
    {
        _phoneBoothCache = FindObjectsByType<PublicPhoneBoothMission>(FindObjectsSortMode.None);
        if (_phoneBoothCache == null || _phoneBoothCache.Length == 0)
            return;

        for (int i = 0; i < _phoneBoothCache.Length; i++)
        {
            var booth = _phoneBoothCache[i];
            if (booth == null || !booth.isActiveAndEnabled || !booth.IsDialing)
                continue;

            booth.PollDialInput();
            return;
        }
    }

    void TryInteract()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        var gm = GetGameManager();
        if (gm == null || !gm.HasFirstAidKit())
        {
            if (TryPickFirstAidAlongRay(ray))
                return;
            if (TryFirstAidKitFallbackWhenFacing(cam))
                return;
        }

        if (TryGetInteractHit(ray, out RaycastHit hit))
        {
            if (ProcessHit(hit))
                return;
        }

        if (TryWoundedInteractWhenFacing(cam))
            return;

        TryPhoneBoothFallbackWhenFacing(cam);
    }

    private bool TryPickFirstAidAlongRay(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            interactDistance + 1f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);

        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].distance > interactDistance + 1f)
                continue;

            var kit = hits[i].collider.GetComponentInParent<FirstAidKitPickup>(true);
            if (kit != null && kit.isActiveAndEnabled)
            {
                kit.OnPickUp();
                return true;
            }
        }

        return false;
    }

    private bool ProcessHit(RaycastHit hit)
    {
        FirstAidKitPickup firstAidKit = hit.collider.GetComponent<FirstAidKitPickup>();
        if (firstAidKit == null)
            firstAidKit = hit.collider.GetComponentInParent<FirstAidKitPickup>();
        if (firstAidKit != null)
        {
            firstAidKit.OnPickUp();
            return true;
        }

        WoundedMan wounded = hit.collider.GetComponent<WoundedMan>();
        if (wounded == null)
            wounded = hit.collider.GetComponentInParent<WoundedMan>();
        if (wounded != null)
        {
            var gm = GetGameManager();
            if (gm == null || gm.HasFirstAidKit())
            {
                wounded.OnFirstAid();
                return true;
            }

            return false;
        }

        PickUpItem item = hit.collider.GetComponent<PickUpItem>();
        if (item == null)
            item = hit.collider.GetComponentInParent<PickUpItem>();
        if (item != null && (item.IsPhoneReceiverPickup || !IsUnderPhoneBooth(item.transform)))
        {
            item.OnPickUp();
            return true;
        }

        if (TryPhoneBoothInteract(hit.collider))
            return true;

        Door door = FindEnabledDoor(hit.collider);
        if (door != null)
        {
            door.ToggleDoor();
            return true;
        }

        LightSwitch lightSwitch = hit.collider.GetComponent<LightSwitch>();
        if (lightSwitch != null)
        {
            lightSwitch.OnInteract();
            return true;
        }

        EmergencyDispatchStation dispatch = hit.collider.GetComponent<EmergencyDispatchStation>();
        if (dispatch == null)
            dispatch = hit.collider.GetComponentInParent<EmergencyDispatchStation>();
        if (dispatch != null)
        {
            dispatch.OnInteract(hit.collider);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Picks the best hit along the look ray (phone booth wins over ground/walls at same distance).
    /// </summary>
    private bool TryGetInteractHit(Ray ray, out RaycastHit hit)
    {
        hit = default;
        int layerMask = Physics.DefaultRaycastLayers;
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            phoneBoothInteractDistance,
            layerMask,
            QueryTriggerInteraction.Collide);

        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RaycastHit? best = null;
        int bestPriority = int.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null)
                continue;

            float maxDist = GetMaxInteractDistance(col);
            if (hits[i].distance > maxDist)
                continue;

            int priority = GetInteractPriority(col);
            if (priority < bestPriority)
            {
                bestPriority = priority;
                best = hits[i];
            }
        }

        if (!best.HasValue)
            return false;

        hit = best.Value;
        return true;
    }

    private int GetInteractPriority(Collider col)
    {
        var gm = GetGameManager();
        bool reported = gm != null && gm.HasReportedEmergency();

        if (col.GetComponentInParent<WoundedMan>(true) != null)
            return reported ? 0 : 2;

        var pickup = col.GetComponentInParent<PickUpItem>(true);
        if (pickup != null && pickup.IsPhoneReceiverPickup)
            return reported ? 40 : 0;

        if (IsPhoneBoothHit(col))
            return reported ? 40 : 1;

        if (col.GetComponentInParent<FirstAidKitPickup>(true) != null)
            return gm != null && !gm.HasFirstAidKit() ? 0 : 1;
        if (col.GetComponentInParent<PickUpItem>(true) != null)
            return 3;
        if (FindEnabledDoor(col) != null && col.GetComponentInParent<PublicPhoneBoothMission>(true) == null)
            return 4;
        if (col.GetComponent<LightSwitch>() != null)
            return 5;
        if (col.GetComponentInParent<EmergencyDispatchStation>(true) != null)
            return reported ? 45 : 6;
        return 50;
    }

    private static Vector3 GetFirstAidInteractPoint(FirstAidKitPickup kit)
    {
        if (kit == null)
            return Vector3.zero;

        var renderer = kit.GetComponentInChildren<Renderer>(true);
        return renderer != null ? renderer.bounds.center : kit.transform.position;
    }

    private GameManager GetGameManager()
    {
        if (_gameManager == null)
            _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        return _gameManager;
    }

    private bool TryFirstAidKitFallbackWhenFacing(Camera cam)
    {
        var gm = GetGameManager();
        if (gm != null && gm.HasFirstAidKit())
            return false;

        var kits = FindObjectsByType<FirstAidKitPickup>(FindObjectsSortMode.None);
        if (kits == null || kits.Length == 0)
            return false;

        FirstAidKitPickup best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < kits.Length; i++)
        {
            var kit = kits[i];
            if (kit == null || !kit.isActiveAndEnabled)
                continue;

            Vector3 kitPoint = GetFirstAidInteractPoint(kit);
            Vector3 toKit = kitPoint - cam.transform.position;
            float dist = toKit.magnitude;
            if (dist > interactDistance + 1.5f)
                continue;

            float facing = Vector3.Dot(cam.transform.forward, toKit.normalized);
            if (facing < 0.35f)
                continue;

            float score = dist - facing * 2f;
            if (score < bestScore)
            {
                bestScore = score;
                best = kit;
            }
        }

        if (best == null)
            return false;

        best.OnPickUp();
        return true;
    }

    private void TryPhoneBoothFallbackWhenFacing(Camera cam)
    {
        var gm = GetGameManager();
        if (gm != null && gm.HasReportedEmergency())
            return;

        if (IsFacingNearbyWounded(cam))
            return;

        var booths = FindObjectsByType<PublicPhoneBoothMission>(FindObjectsSortMode.None);
        if (booths == null || booths.Length == 0)
            return;

        PublicPhoneBoothMission best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < booths.Length; i++)
        {
            var booth = booths[i];
            if (booth == null || !booth.isActiveAndEnabled)
                continue;

            Vector3 toBooth = booth.GetInteractCenter() - cam.transform.position;
            float dist = toBooth.magnitude;
            if (dist > phoneBoothInteractDistance)
                continue;

            float facing = Vector3.Dot(cam.transform.forward, toBooth.normalized);
            if (facing < 0.55f)
                continue;

            float score = dist - facing * 2f;
            if (score < bestScore)
            {
                bestScore = score;
                best = booth;
            }
        }

        best?.TryInteractWithoutRaycast();
    }

    private bool TryWoundedInteractWhenFacing(Camera cam)
    {
        var woundedMen = FindObjectsByType<WoundedMan>(FindObjectsSortMode.None);
        if (woundedMen == null || woundedMen.Length == 0)
            return false;

        WoundedMan best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < woundedMen.Length; i++)
        {
            var wounded = woundedMen[i];
            if (wounded == null || !wounded.isActiveAndEnabled)
                continue;

            if (!wounded.IsFacingForInteract(cam))
                continue;

            float dist = (wounded.GetInteractCenter() - cam.transform.position).magnitude;
            float score = dist;
            if (score < bestScore)
            {
                bestScore = score;
                best = wounded;
            }
        }

        if (best == null)
            return false;

        best.OnFirstAid();
        return true;
    }

    private bool IsFacingNearbyWounded(Camera cam)
    {
        var woundedMen = FindObjectsByType<WoundedMan>(FindObjectsSortMode.None);
        if (woundedMen == null)
            return false;

        for (int i = 0; i < woundedMen.Length; i++)
        {
            var wounded = woundedMen[i];
            if (wounded == null || !wounded.isActiveAndEnabled)
                continue;

            if (wounded.IsFacingForInteract(cam))
                return true;
        }

        return false;
    }

    private float GetMaxInteractDistance(Collider col)
    {
        if (col == null)
            return interactDistance;

        if (IsPhoneBoothHit(col))
            return phoneBoothInteractDistance;

        var wounded = col.GetComponentInParent<WoundedMan>(true);
        if (wounded != null)
            return Mathf.Max(woundedInteractDistance, wounded.InteractDistance);

        return interactDistance;
    }

    private static bool TryPhoneBoothInteract(Collider col)
    {
        if (!IsPhoneBoothHit(col))
            return false;

        PublicPhoneBoothMission booth = col.GetComponentInParent<PublicPhoneBoothMission>(true);
        if (booth != null)
            return booth.TryInteract(col);

        PhoneBoothInteractPoint phonePoint = col.GetComponent<PhoneBoothInteractPoint>()
            ?? col.GetComponentInParent<PhoneBoothInteractPoint>();
        if (phonePoint != null && phonePoint.booth != null)
            return phonePoint.booth.TryInteract(col);

        return false;
    }

    private static bool IsPhoneBoothHit(Collider col)
    {
        if (col == null)
            return false;

        if (col.GetComponentInParent<PublicPhoneBoothMission>(true) != null)
            return true;

        return col.GetComponentInParent<PhoneBoothInteractPoint>(true) != null;
    }

    private static bool IsUnderPhoneBooth(Transform t)
    {
        if (t == null)
            return false;

        return t.GetComponentInParent<PublicPhoneBoothMission>(true) != null;
    }

    private static Door FindEnabledDoor(Collider hitCollider)
    {
        var doors = hitCollider.GetComponentsInParent<Door>(true);
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null || !doors[i].enabled)
                continue;

            if (doors[i].GetComponentInParent<PublicPhoneBoothMission>(true) != null)
                continue;

            return doors[i];
        }

        return null;
    }
}
