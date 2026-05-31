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

    [Tooltip("Exit door root with Animator — usually PFB_DoorDouble (NOT leftDoor).")]
    public Door exitDoor;

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
        if (exitDoor == null)
            exitDoor = FindDoorRoot();

        if (exitDoor == null)
        {
            Debug.LogWarning("SimulationMissionBootstrap: Assign exitDoor (PFB_DoorDouble) in the Inspector.");
            return;
        }

        DisableMisplacedDoorComponents(exitDoor.transform);

        exitDoor.enabled = true;
        exitDoor.missionExitDoor = true;
        exitDoor.startOpen = true;
        exitDoor.CacheAnimator();

        var leafCollider = FindDeepChild(exitDoor.transform, "leftDoor");
        if (leafCollider != null)
            EnsureCollider(leafCollider.gameObject, new Vector3(0.6f, 2f, 0.15f));
        EnsureCollider(exitDoor.gameObject, new Vector3(1.2f, 2f, 0.3f));
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

    private Door FindDoorRoot()
    {
        var doorRootObject = FindSceneObjectByName(exitDoorRootName)
            ?? FindSceneObjectByName("PFB_DoorDouble")
            ?? FindSceneObjectByName("DoorDouble");

        if (doorRootObject == null)
            return null;

        var door = doorRootObject.GetComponent<Door>();
        if (door == null)
            door = doorRootObject.AddComponent<Door>();

        return door;
    }

    private void ResetSimulation1WorldState()
    {
        ReactivateSimulation1Pickups();
        _lightSwitch?.ResetSwitch();
        exitDoor?.ResetToOpen();
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
        var collider = target.GetComponent<Collider>();
        if (collider == null)
        {
            var box = target.AddComponent<BoxCollider>();
            box.size = size;
            box.center = Vector3.zero;
        }
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
