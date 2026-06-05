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

    public bool IsOpen => isOpen;

    private bool isOpen;
    private bool _useManualRotation;
    private GameManager _gameManager;
    private float _currentAngleY;
    private float _targetAngleY;
    private DoorLeafPose[] _leafPoses;

    private struct DoorLeafPose
    {
        public Transform transform;
        public Quaternion closedLocalRotation;
        public float openSign;
    }

    void Awake()
    {
        CacheAnimator();
        CacheDoorLeafPoses();
        _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        _useManualRotation = !HasWorkingAnimator() && HasDoorLeaves();
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

    void LateUpdate()
    {
        if (!missionExitDoor || !_useManualRotation || doorLeaves == null)
            return;

        ApplyManualAngle(_currentAngleY);
    }

    public void CacheAnimator()
    {
        if (TryResolveDoorAnimator(out Animator resolved))
            animator = resolved;

        EnsureDoorLeavesCached();
    }

    public void CacheDoorLeafPoses()
    {
        EnsureDoorLeavesCached();
        if (doorLeaves == null || doorLeaves.Length == 0)
        {
            _leafPoses = null;
            return;
        }

        _leafPoses = new DoorLeafPose[doorLeaves.Length];
        for (int i = 0; i < doorLeaves.Length; i++)
        {
            var leaf = doorLeaves[i];
            if (leaf == null)
                continue;

            _leafPoses[i] = new DoorLeafPose
            {
                transform = leaf,
                closedLocalRotation = leaf.localRotation,
                openSign = GetLeafOpenSign(leaf.name, doorLeaves.Length)
            };
        }
    }

    private bool TryResolveDoorAnimator(out Animator resolved)
    {
        resolved = null;

        Transform root = GetDoorAnimationRoot();
        if (root != null)
            resolved = root.GetComponent<Animator>();

        if (resolved == null)
            resolved = GetComponentInParent<Animator>();

        if (resolved == null)
            resolved = GetComponent<Animator>();

        return resolved != null && resolved.runtimeAnimatorController != null;
    }

    private bool HasWorkingAnimator()
    {
        return TryResolveDoorAnimator(out Animator resolved)
            && resolved != null
            && resolved.runtimeAnimatorController != null;
    }

    private Transform GetDoorAnimationRoot()
    {
        Transform current = transform;
        for (int i = 0; i < 8 && current != null; i++)
        {
            if (current.name.Equals("PFB_DoorDouble", System.StringComparison.OrdinalIgnoreCase))
                return current;

            current = current.parent;
        }

        return null;
    }

    private void AutoFindDoorLeaves()
    {
        Transform root = GetDoorAnimationRoot() ?? transform;
        Transform doorDouble = FindDirectChild(root, "DoorDouble") ?? root;

        var left = FindDirectChild(doorDouble, "leftDoor");
        var right = FindDirectChild(doorDouble, "rightDoor");

        if (left == null)
            left = FindDeepChild(root, "leftDoor");
        if (right == null)
            right = FindDeepChild(root, "rightDoor");

        if (left != null && right != null)
            doorLeaves = new[] { left, right };
        else if (left != null)
            doorLeaves = new[] { left };
        else if (right != null)
            doorLeaves = new[] { right };
    }

    /// <summary>Independent leaf states (e.g. tour: right closed, left open for light-switch visibility).</summary>
    public void ApplyLeafStates(bool leftOpen, bool rightOpen, bool immediate = true)
    {
        EnsureDoorLeavesCached();
        CacheDoorLeafPoses();

        if (TryResolveDoorAnimator(out Animator resolvedAnimator) && resolvedAnimator != null)
            resolvedAnimator.enabled = false;

        _useManualRotation = false;

        if (_leafPoses != null && _leafPoses.Length > 0)
        {
            for (int i = 0; i < _leafPoses.Length; i++)
            {
                var pose = _leafPoses[i];
                if (pose.transform == null)
                    continue;

                bool open = IsLeftLeaf(pose.transform) ? leftOpen : rightOpen;
                pose.transform.localRotation = open
                    ? pose.closedLocalRotation * Quaternion.Euler(0f, pose.openSign * openAngleY, 0f)
                    : pose.closedLocalRotation;
            }
        }
        else if (doorLeaves != null)
        {
            for (int i = 0; i < doorLeaves.Length; i++)
            {
                var leaf = doorLeaves[i];
                if (leaf == null)
                    continue;

                bool open = IsLeftLeaf(leaf) ? leftOpen : rightOpen;
                float sign = GetLeafOpenSign(leaf.name, doorLeaves.Length);
                leaf.localRotation = open
                    ? Quaternion.Euler(0f, sign * openAngleY, 0f)
                    : Quaternion.identity;
            }
        }

        isOpen = leftOpen || rightOpen;
        _currentAngleY = isOpen ? openAngleY : closeAngleY;
        _targetAngleY = _currentAngleY;
    }

    public void ResetToOpen()
    {
        isOpen = true;
        ApplyDoorState(true, immediate: true);
        NotifyMissionDoorState();
    }

    public void Open()
    {
        if (isOpen)
            return;

        isOpen = true;
        ApplyDoorState(true, immediate: false);
        NotifyMissionDoorState();
    }

    public void Close()
    {
        if (!isOpen)
            return;

        isOpen = false;
        ApplyDoorState(false, immediate: false);
        NotifyMissionDoorState();
    }

    public void ToggleDoor()
    {
        if (isOpen)
            Close();
        else
            Open();
    }

    private void NotifyMissionDoorState()
    {
        if (!missionExitDoor || _gameManager == null)
            return;

        _gameManager.SyncExitDoorState(!isOpen);
    }

    private void ApplyDoorState(bool open, bool immediate)
    {
        EnsureDoorLeavesCached();
        DisableMisplacedDoorAnimators();

        if (missionExitDoor && TryApplyAnimatorDoorState(open, immediate))
            return;

        if (TryApplyAnimatorDoorState(open, immediate))
            return;

        if (!HasDoorLeaves())
        {
            Debug.LogWarning($"Door on '{name}': no Animator controller found. Assign PFB_DoorDouble or doorLeaves.");
            return;
        }

        ApplyManualDoorState(open, immediate);
    }

    private bool TryApplyAnimatorDoorState(bool open, bool immediate)
    {
        if (!TryResolveDoorAnimator(out Animator resolvedAnimator)
            || resolvedAnimator.runtimeAnimatorController == null)
            return false;

        animator = resolvedAnimator;
        animator.enabled = true;
        _useManualRotation = false;

        if (!string.IsNullOrWhiteSpace(openBoolParameter))
            animator.SetBool(openBoolParameter, open);

        string stateName = open ? "DoorDouble_Open" : "DoorDouble_Close";
        if (!HasState(stateName))
            return false;

        float startTime = immediate ? (open ? 1f : 0f) : 0f;
        animator.CrossFade(stateName, 0.08f, 0, startTime);
        animator.Update(0f);
        return true;
    }

    private void EnsureDoorLeavesCached()
    {
        if (doorLeaves == null || doorLeaves.Length == 0)
            AutoFindDoorLeaves();
    }

    private bool HasDoorLeaves()
    {
        EnsureDoorLeavesCached();
        return doorLeaves != null && doorLeaves.Length > 0;
    }

    public void SnapLeavesToClosed()
    {
        EnsureDoorLeavesCached();
        if (doorLeaves != null)
        {
            for (int i = 0; i < doorLeaves.Length; i++)
            {
                if (doorLeaves[i] != null)
                    doorLeaves[i].localRotation = Quaternion.identity;
            }
        }

        CacheDoorLeafPoses();
        _currentAngleY = closeAngleY;
        _targetAngleY = closeAngleY;
        _useManualRotation = false;
    }

    private void ApplyManualDoorState(bool open, bool immediate)
    {
        _targetAngleY = open ? openAngleY : closeAngleY;
        if (immediate)
        {
            _currentAngleY = _targetAngleY;
            ApplyManualAngle(_currentAngleY);
            _useManualRotation = false;
            return;
        }

        _useManualRotation = true;
    }

    private void DisableMisplacedDoorAnimators()
    {
        Transform root = GetDoorAnimationRoot() ?? transform;
        var animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            var anim = animators[i];
            if (anim == null || anim.transform == root)
                continue;

            anim.enabled = false;
        }
    }

    private void ApplyManualAngle(float angleY)
    {
        if (_leafPoses != null && _leafPoses.Length > 0)
        {
            float openT = Mathf.InverseLerp(closeAngleY, openAngleY, angleY);
            for (int i = 0; i < _leafPoses.Length; i++)
            {
                var pose = _leafPoses[i];
                if (pose.transform == null)
                    continue;

                var openRotation = pose.closedLocalRotation * Quaternion.Euler(0f, pose.openSign * openAngleY, 0f);
                pose.transform.localRotation = openT <= 0.001f
                    ? pose.closedLocalRotation
                    : openT >= 0.999f
                        ? openRotation
                        : Quaternion.Slerp(pose.closedLocalRotation, openRotation, openT);
            }

            return;
        }

        if (doorLeaves == null)
            return;

        for (int i = 0; i < doorLeaves.Length; i++)
        {
            var leaf = doorLeaves[i];
            if (leaf == null)
                continue;

            float sign = GetLeafOpenSign(leaf.name, doorLeaves.Length);
            leaf.localRotation = Quaternion.Euler(0f, sign * angleY, 0f);
        }
    }

    private static float GetLeafOpenSign(string leafName, int leafCount)
    {
        if (leafCount == 1)
            return 1f;

        return IsLeftLeafName(leafName) ? 1f : -1f;
    }

    private static bool IsLeftLeaf(Transform leaf)
    {
        return leaf != null && IsLeftLeafName(leaf.name);
    }

    private static bool IsLeftLeafName(string leafName)
    {
        return !string.IsNullOrEmpty(leafName)
            && leafName.ToLowerInvariant().Contains("left");
    }

    private bool HasState(string stateName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        int hash = Animator.StringToHash(stateName);
        return animator.HasState(0, hash);
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
