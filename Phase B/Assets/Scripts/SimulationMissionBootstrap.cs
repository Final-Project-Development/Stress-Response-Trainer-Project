using UnityEngine;

/// <summary>
/// Assign mission objects in the Inspector (pickups, light switch, exit door, Sim2 first aid kit).
/// </summary>
public class SimulationMissionBootstrap : MonoBehaviour
{
    [Header("Simulation 1 — drag your scene objects here")]
    [Tooltip("All collectible items inside the home (existing + 2 extra). Count sets itemToCollect automatically.")]
    public PickUpItem[] simulation1Pickups;

    [Tooltip("Light switch mesh inside the home, e.g. PFB_Lightswitch (1).")]
    public GameObject lightSwitchObject;

    [Tooltip("Exit door root — drag PFB_DoorDouble from Hierarchy (NOT leftDoor).")]
    public GameObject exitDoorObject;

    [Header("Simulation 2 — drag your scene objects here")]
    [Tooltip("Optional: drag the firstaid object from the Hierarchy.")]
    public GameObject simulation2FirstAidKitObject;

    [Tooltip("Optional: if the object above is set, this is filled automatically.")]
    public FirstAidKitPickup simulation2FirstAidKit;

    [Tooltip("Wounded character root, e.g. WoundedCharacter_TPose.")]
    public GameObject woundedRoot;

    [Header("Optional name fallback (only if refs above are empty)")]
    public string lightSwitchObjectName = "PFB_Lightswitch (1)";
    public string exitDoorRootName = "PFB_DoorDouble";
    public string woundedObjectName = "WoundedCharacter_TPose";
    public string firstAidKitObjectName = "firstaid";

    private GameManager _gameManager;
    private LightSwitch _lightSwitch;
    private Door _exitDoor;

    void Awake()
    {
        _gameManager = GetComponent<GameManager>();
        if (_gameManager == null)
            _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
    }

    void Start()
    {
        HideFirstAidKit();
    }

    public void PrepareSimulation1()
    {
        HideFirstAidKit();
        SyncItemCount();
        EnsureLightSwitch();
        EnsureExitDoor();
        ResetSimulation1WorldState();
    }

    public void PrepareSimulation2()
    {
        ResolveWoundedRoot();
        HideWounded();
        ResolveSimulation2FirstAidKit();
        ResetFirstAidKitForMission();
        ShowFirstAidKit();
    }

    private void SyncItemCount()
    {
        if (_gameManager == null || simulation1Pickups == null || simulation1Pickups.Length == 0)
            return;

        _gameManager.itemToCollect = simulation1Pickups.Length;
    }

    private void EnsureLightSwitch()
    {
        var switchObject = lightSwitchObject != null
            ? lightSwitchObject
            : FindSceneObjectByName(lightSwitchObjectName);

        if (switchObject == null)
        {
            Debug.LogWarning("SimulationMissionBootstrap: Assign lightSwitchObject in the Inspector.");
            return;
        }

        EnsureCollider(switchObject, new Vector3(0.18f, 0.25f, 0.08f));
        _lightSwitch = switchObject.GetComponent<LightSwitch>();
        if (_lightSwitch == null)
            _lightSwitch = switchObject.AddComponent<LightSwitch>();

        _lightSwitch.Initialize(_gameManager);
    }

    private void EnsureExitDoor()
    {
        _exitDoor = ResolveExitDoorRoot();

        if (_exitDoor == null)
        {
            Debug.LogWarning("SimulationMissionBootstrap: Assign exitDoorObject (PFB_DoorDouble) in the Inspector.");
            return;
        }

        DisableMisplacedDoorComponents(_exitDoor.transform);
        DisableMisplacedDoorAnimators(_exitDoor.transform);
        DisableLegacyDoorControllers(_exitDoor.transform);
        AssignDoorLeaves(_exitDoor);

        _exitDoor.enabled = true;
        _exitDoor.missionExitDoor = true;
        _exitDoor.startOpen = true;
        _exitDoor.openAngleY = 128f;
        _exitDoor.closeAngleY = 0f;
        _exitDoor.rotateSpeed = 360f;
        _exitDoor.CacheAnimator();
        _exitDoor.SnapLeavesToClosed();
        _exitDoor.CacheDoorLeafPoses();
        _exitDoor.ResetToOpen();

        var rootAnimator = _exitDoor.GetComponent<Animator>();
        if (rootAnimator != null)
            rootAnimator.enabled = true;

        var leafCollider = FindDeepChild(_exitDoor.transform, "leftDoor");
        if (leafCollider != null)
            EnsureSolidCollider(leafCollider.gameObject, new Vector3(0.6f, 2f, 0.15f));
        EnsureTriggerCollider(_exitDoor.gameObject, new Vector3(1.2f, 2f, 0.3f));
    }

    private Door ResolveExitDoorRoot()
    {
        GameObject doorRootObject = exitDoorObject;
        if (doorRootObject != null
            && (doorRootObject.name.Equals("leftDoor", System.StringComparison.OrdinalIgnoreCase)
                || doorRootObject.name.Equals("rightDoor", System.StringComparison.OrdinalIgnoreCase)))
        {
            doorRootObject = FindDoorRootObject(doorRootObject.transform);
        }

        if (doorRootObject == null)
            doorRootObject = FindDoorRootObject(null);

        if (doorRootObject == null)
            return null;

        exitDoorObject = doorRootObject;
        var door = doorRootObject.GetComponent<Door>();
        if (door == null)
            door = doorRootObject.AddComponent<Door>();

        return door;
    }

    private GameObject FindDoorRootObject(Transform start)
    {
        if (start != null)
        {
            Transform current = start;
            while (current != null)
            {
                if (current.name.Equals("PFB_DoorDouble", System.StringComparison.OrdinalIgnoreCase)
                    || FindDeepChild(current, "DoorDouble") != null)
                    return current.gameObject;

                current = current.parent;
            }
        }

        return FindSceneObjectByName(exitDoorRootName)
            ?? FindSceneObjectByName("PFB_DoorDouble")
            ?? FindSceneObjectByName("DoorDouble");
    }

    private static void DisableLegacyDoorControllers(Transform doorRoot)
    {
        var legacyControllers = doorRoot.GetComponentsInChildren<MoveObjectController>(true);
        for (int i = 0; i < legacyControllers.Length; i++)
        {
            if (legacyControllers[i] != null)
                legacyControllers[i].enabled = false;
        }
    }

    private static void AssignDoorLeaves(Door door)
    {
        if (door == null)
            return;

        Transform root = door.transform;
        Transform doorDouble = FindDirectChild(root, "DoorDouble") ?? root;

        var left = FindDirectChild(doorDouble, "leftDoor");
        var right = FindDirectChild(doorDouble, "rightDoor");

        if (left == null)
            left = FindDeepChild(root, "leftDoor");
        if (right == null)
            right = FindDeepChild(root, "rightDoor");

        if (left != null && right != null)
            door.doorLeaves = new[] { left, right };
        else if (left != null)
            door.doorLeaves = new[] { left };
        else if (right != null)
            door.doorLeaves = new[] { right };
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child != null && child.name == childName)
                return child;
        }

        return null;
    }

    private static void DisableMisplacedDoorAnimators(Transform doorRoot)
    {
        var animators = doorRoot.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            var anim = animators[i];
            if (anim == null || anim.transform == doorRoot)
                continue;

            anim.enabled = false;
            Destroy(anim);
        }
    }

    private static void DisableMisplacedDoorComponents(Transform doorRoot)
    {
        var doors = doorRoot.GetComponentsInChildren<Door>(true);
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null)
                continue;

            if (doors[i].transform == doorRoot)
                continue;

            doors[i].enabled = false;
        }
    }

    private void ResetSimulation1WorldState()
    {
        ReactivateSimulation1Pickups();
        _lightSwitch?.ResetSwitch();
        _exitDoor?.ResetToOpen();
    }

    private void ReactivateSimulation1Pickups()
    {
        if (simulation1Pickups == null || simulation1Pickups.Length == 0)
            return;

        for (int i = 0; i < simulation1Pickups.Length; i++)
        {
            if (simulation1Pickups[i] != null)
                simulation1Pickups[i].gameObject.SetActive(true);
        }
    }

    private void ResolveWoundedRoot()
    {
        if (woundedRoot == null)
            woundedRoot = FindSceneObjectByName(woundedObjectName);
    }

    private void HideWounded()
    {
        ResolveWoundedRoot();
        if (woundedRoot != null)
            woundedRoot.SetActive(false);
    }

    public void RevealWounded()
    {
        ResolveWoundedRoot();
        if (woundedRoot != null)
            woundedRoot.SetActive(true);
    }

    private void ResolveSimulation2FirstAidKit()
    {
        if (simulation2FirstAidKit != null)
            return;

        GameObject kitObject = simulation2FirstAidKitObject;
        if (kitObject == null)
        {
            kitObject = FindCollectibleObjectByName(firstAidKitObjectName)
                ?? FindCollectibleObjectByName("firstaid")
                ?? FindCollectibleObjectByName("FirstAid");
        }

        if (kitObject != null)
        {
            simulation2FirstAidKit = EnsureFirstAidKitOnObject(kitObject);
            return;
        }

        simulation2FirstAidKit = FindFirstObjectByType<FirstAidKitPickup>(FindObjectsInactive.Include);
    }

    private static FirstAidKitPickup EnsureFirstAidKitOnObject(GameObject kitObject)
    {
        if (kitObject.GetComponent<RectTransform>() != null)
        {
            Debug.LogWarning($"SimulationMissionBootstrap: '{kitObject.name}' is UI — assign the 3D firstaid object instead.");
            return null;
        }

        EnsureCollider(kitObject, new Vector3(0.45f, 0.3f, 0.35f));

        var legacyPickup = kitObject.GetComponent<PickUpItem>();
        if (legacyPickup != null)
            Destroy(legacyPickup);

        var pickup = kitObject.GetComponent<FirstAidKitPickup>();
        if (pickup == null)
            pickup = kitObject.AddComponent<FirstAidKitPickup>();

        return pickup;
    }

    private void ResetFirstAidKitForMission()
    {
        ResolveSimulation2FirstAidKit();
        if (simulation2FirstAidKit == null)
        {
            Debug.LogWarning("SimulationMissionBootstrap: Could not find firstaid object. Add it to the scene or assign Simulation 2 First Aid Kit Object.");
            return;
        }

        simulation2FirstAidKit.gameObject.SetActive(true);
    }

    private void ShowFirstAidKit()
    {
        ResolveSimulation2FirstAidKit();
        if (simulation2FirstAidKit != null)
            simulation2FirstAidKit.gameObject.SetActive(true);
    }

    private void HideFirstAidKit()
    {
        ResolveSimulation2FirstAidKit();
        if (simulation2FirstAidKit != null)
            simulation2FirstAidKit.gameObject.SetActive(false);
    }

    private static void EnsureCollider(GameObject target, Vector3 size)
    {
        EnsureSolidCollider(target, size);
    }

    private static void EnsureSolidCollider(GameObject target, Vector3 size)
    {
        var collider = target.GetComponent<Collider>();
        if (collider == null)
        {
            var box = target.AddComponent<BoxCollider>();
            box.size = size;
            box.center = Vector3.zero;
            box.isTrigger = false;
        }
    }

    private static void EnsureTriggerCollider(GameObject target, Vector3 size)
    {
        var colliders = target.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].isTrigger)
                return;
        }

        var box = target.AddComponent<BoxCollider>();
        box.size = size;
        box.center = Vector3.zero;
        box.isTrigger = true;
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        var all = parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == childName)
                return all[i];
        }

        return null;
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        return FindCollectibleObjectByName(objectName);
    }

    private static GameObject FindCollectibleObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            if (t == null)
                continue;

            if (!string.Equals(t.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (t.GetComponent<RectTransform>() != null)
                continue;

            return t.gameObject;
        }

        return null;
    }
}
