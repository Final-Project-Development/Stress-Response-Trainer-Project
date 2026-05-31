using UnityEngine;

public class Door : MonoBehaviour
{
    public Animator animator;
    [Tooltip("Animator bool/trigger name. Default matches FurnishedCabin DoorDoubleAnimator.")]
    public string openBoolParameter = "isOpen";
    [Tooltip("When enabled, closing this door can complete the Simulation 1 exit-door objective.")]
    public bool missionExitDoor;
    [Tooltip("Door starts open (player entered the home before the mission).")]
    public bool startOpen;
    [Tooltip("Optional: rotate these transforms when no animator is found.")]
    public Transform[] doorLeaves;

    [Header("Manual fallback rotation (degrees Y)")]
    public float openAngleY = 128f;
    public float closeAngleY = 0f;
    public float rotateSpeed = 360f;

    private bool isOpen;
    private bool _useManualRotation;
    private GameManager _gameManager;
    private float _currentAngleY;
    private float _targetAngleY;

    void Awake()
    {
        CacheAnimator();
        _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        _useManualRotation = animator == null && doorLeaves != null && doorLeaves.Length > 0;
    }

    void Start()
    {
        if (startOpen)
            ResetToOpen();
    }

    void Update()
    {
        if (!_useManualRotation)
            return;

        if (Mathf.Approximately(_currentAngleY, _targetAngleY))
            return;

        _currentAngleY = Mathf.MoveTowards(_currentAngleY, _targetAngleY, rotateSpeed * Time.deltaTime);
        ApplyManualAngle(_currentAngleY);
    }

    public void CacheAnimator()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInParent<Animator>();

        if (doorLeaves == null || doorLeaves.Length == 0)
            AutoFindDoorLeaves();
    }

    private void AutoFindDoorLeaves()
    {
        var left = transform.Find("DoorDouble/leftDoor");
        var right = transform.Find("DoorDouble/rightDoor");
        if (left == null)
            left = FindDeepChild(transform, "leftDoor");
        if (right == null)
            right = FindDeepChild(transform, "rightDoor");

        if (left != null && right != null)
            doorLeaves = new[] { left, right };
        else if (left != null)
            doorLeaves = new[] { left };
    }

    public void ResetToOpen()
    {
        isOpen = true;
        ApplyDoorState(true, immediate: true);
    }

    public void ToggleDoor()
    {
        isOpen = !isOpen;
        ApplyDoorState(isOpen, immediate: false);

        if (missionExitDoor && !isOpen)
            _gameManager?.OnExitDoorClosed();
    }

    private void ApplyDoorState(bool open, bool immediate)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            if (!string.IsNullOrWhiteSpace(openBoolParameter))
                animator.SetBool(openBoolParameter, open);

            if (immediate)
            {
                string stateName = open ? "DoorDouble_Open" : "DoorDouble_Close";
                if (HasState(stateName))
                    animator.Play(stateName, 0, open ? 1f : 0f);
            }

            return;
        }

        _useManualRotation = doorLeaves != null && doorLeaves.Length > 0;
        if (!_useManualRotation)
        {
            Debug.LogWarning($"Door on '{name}': no Animator controller found. Assign PFB_DoorDouble or doorLeaves.");
            return;
        }

        _targetAngleY = open ? openAngleY : closeAngleY;
        if (immediate)
        {
            _currentAngleY = _targetAngleY;
            ApplyManualAngle(_currentAngleY);
        }
    }

    private void ApplyManualAngle(float angleY)
    {
        if (doorLeaves == null)
            return;

        for (int i = 0; i < doorLeaves.Length; i++)
        {
            var leaf = doorLeaves[i];
            if (leaf == null)
                continue;

            float sign = leaf.name.ToLowerInvariant().Contains("left") ? 1f : -1f;
            if (doorLeaves.Length == 1)
                sign = 1f;

            leaf.localRotation = Quaternion.Euler(0f, sign * angleY, 0f);
        }
    }

    private bool HasState(string stateName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        int hash = Animator.StringToHash(stateName);
        return animator.HasState(0, hash);
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
}
