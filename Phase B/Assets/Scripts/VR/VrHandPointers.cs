using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Shows both Quest controllers in VR with a visible laser so the player can see where each remote points.
/// </summary>
[DefaultExecutionOrder(-4900)]
public sealed class VrHandPointers : MonoBehaviour
{
    public static VrHandPointers Instance { get; private set; }

    [Header("Pointer")]
    public float pointerLength = 2.2f;
    public float lineWidth = 0.008f;
    public float handMarkerRadius = 0.05f;
    public float tipMarkerRadius = 0.028f;
    [Tooltip("Moves the laser origin from the controller grip to the visual center/front of the remote.")]
    public Vector3 pointerLocalOriginOffset = new Vector3(0f, 0f, 0.04f);
    [Tooltip("Direction offset in degrees (0-360 on each axis). X = pitch (up/down), Y = yaw (left/right), Z = roll. Applied on top of the controller's rotation.")]
    [Range(0f, 360f)] public float pointerAngleX = 90f;
    [Range(0f, 360f)] public float pointerAngleY = 0f;
    [Range(0f, 360f)] public float pointerAngleZ = 0f;
    [Tooltip("When on, the laser is auto-flipped so it always points away from you. Turn off for fully manual control while calibrating.")]
    public bool keepPointingForward = true;

    [Header("Colors")]
    public Color leftHandColor = new Color(0.35f, 0.85f, 1f, 1f);
    public Color rightHandColor = new Color(1f, 0.62f, 0.28f, 1f);

    private Transform playerRoot;
    private Transform cameraTransform;
    private Transform leftHandRoot;
    private Transform rightHandRoot;
    private HandVisual leftHand;
    private HandVisual rightHand;
    private Renderer[] playerBodyRenderers = System.Array.Empty<Renderer>();
    private bool playerBodyHidden;
    private Material lineMaterial;

    private struct HandVisual
    {
        public Transform root;
        public Transform aim;
        public Transform tip;
        public LineRenderer line;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance != null)
            return;

        var go = new GameObject("VR Hand Pointers");
        go.AddComponent<VrHandPointers>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        BindSceneObjects();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindSceneObjects();
    }

    private void LateUpdate()
    {
        bool show = XRInputBridge.IsVrActive;
        SetActive(show);

        if (!show)
        {
            RestorePlayerBodyRenderers();
            return;
        }

        if (leftHandRoot == null)
            BindSceneObjects();

        if (leftHandRoot == null)
            return;

        HidePlayerBodyRenderers();
        UpdateHand(leftHand, XRNode.LeftHand, leftHandColor, true);
        UpdateHand(rightHand, XRNode.RightHand, rightHandColor, false);
    }

    public void SetActive(bool active)
    {
        BindSceneObjectsIfMissing();
        if (leftHandRoot != null)
            leftHandRoot.gameObject.SetActive(active);
        if (rightHandRoot != null)
            rightHandRoot.gameObject.SetActive(active);

        if (!active)
            RestorePlayerBodyRenderers();
    }

    public bool TryGetRightPointerRay(out Ray ray) => TryGetPointerRay(rightHand, out ray);

    private bool TryGetPointerRay(HandVisual hand, out Ray ray)
    {
        ray = default;
        if (hand.root == null || !hand.root.gameObject.activeInHierarchy || hand.aim == null)
            return false;

        Vector3 direction = hand.aim.forward;
        if (direction.sqrMagnitude < 0.0001f)
            return false;

        ray = new Ray(GetPointerOrigin(hand), direction.normalized);
        return true;
    }

    public bool TryGetRightPointerScreenPoint(Camera camera, out Vector2 screenPoint)
    {
        screenPoint = default;
        if (camera == null || !TryGetRightPointerRay(out Ray ray))
            return false;

        if (TryProjectRayToCanvasScreenPoint(camera, ray, out screenPoint))
            return true;

        Vector3 tip = ray.origin + ray.direction * pointerLength;
        Vector3 screen = camera.WorldToScreenPoint(tip);
        if (screen.z <= 0f)
            return false;

        screenPoint = new Vector2(screen.x, screen.y);
        return true;
    }

    public static bool TryProjectRayToCanvasScreenPoint(Camera camera, Ray ray, out Vector2 screenPoint)
    {
        screenPoint = default;
        if (camera == null)
            return false;

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        float bestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.worldCamera == null)
                continue;

            Plane plane;
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                plane = new Plane(canvas.transform.forward, canvas.transform.position);
            }
            else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                Transform camTransform = canvas.worldCamera.transform;
                plane = new Plane(-camTransform.forward, camTransform.position + camTransform.forward * canvas.planeDistance);
            }
            else
            {
                continue;
            }

            if (!plane.Raycast(ray, out float distance) || distance <= 0f || distance >= bestDistance)
                continue;

            Vector3 hit = ray.GetPoint(distance);
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                RectTransform rect = canvas.transform as RectTransform;
                if (rect != null)
                {
                    Vector3 localHit = rect.InverseTransformPoint(hit);
                    Rect canvasRect = rect.rect;
                    if (!canvasRect.Contains(new Vector2(localHit.x, localHit.y)))
                        continue;
                }
            }

            Vector3 screen = canvas.worldCamera.WorldToScreenPoint(hit);
            if (screen.z <= 0f)
                continue;

            bestDistance = distance;
            screenPoint = new Vector2(screen.x, screen.y);
            found = true;
        }

        return found;
    }

    /// <summary>
    /// Finds the nearest UI canvas the ray crosses and returns the world-space hit point.
    /// Used to stop the laser exactly on the panel so the dot equals the real click point.
    /// </summary>
    public static bool TryGetCanvasHitPoint(Ray ray, out Vector3 hitPoint)
    {
        hitPoint = default;
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        float bestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
                continue;

            Plane plane;
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                plane = new Plane(canvas.transform.forward, canvas.transform.position);
            }
            else if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
            {
                Transform camTransform = canvas.worldCamera.transform;
                plane = new Plane(-camTransform.forward, camTransform.position + camTransform.forward * canvas.planeDistance);
            }
            else
            {
                continue;
            }

            if (!plane.Raycast(ray, out float distance) || distance <= 0f || distance >= bestDistance)
                continue;

            Vector3 hit = ray.GetPoint(distance);
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                RectTransform rect = canvas.transform as RectTransform;
                if (rect != null)
                {
                    Vector3 localHit = rect.InverseTransformPoint(hit);
                    if (!rect.rect.Contains(new Vector2(localHit.x, localHit.y)))
                        continue;
                }
            }

            bestDistance = distance;
            hitPoint = hit;
            found = true;
        }

        return found;
    }

    private void BindSceneObjectsIfMissing()
    {
        if (playerRoot == null || leftHandRoot == null)
            BindSceneObjects();
    }

    private void BindSceneObjects()
    {
        TrainingFlowController flow = TrainingFlowController.Instance != null
            ? TrainingFlowController.Instance
            : Object.FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        SimpleFPSController controller = flow != null && flow.playerFpsController != null
            ? flow.playerFpsController
            : Object.FindFirstObjectByType<SimpleFPSController>(FindObjectsInactive.Include);

        playerRoot = controller != null ? controller.transform : null;
        cameraTransform = controller != null && controller.cameraTransform != null
            ? controller.cameraTransform
            : Camera.main != null ? Camera.main.transform : null;

        if (playerRoot == null)
            return;

        playerBodyRenderers = playerRoot.GetComponents<Renderer>();
        EnsureHandHierarchy();
    }

    private void EnsureHandHierarchy()
    {
        if (leftHandRoot != null)
            return;

        EnsureLineMaterial();

        leftHandRoot = new GameObject("VR Left Hand Pointer").transform;
        rightHandRoot = new GameObject("VR Right Hand Pointer").transform;
        leftHandRoot.SetParent(playerRoot, false);
        rightHandRoot.SetParent(playerRoot, false);

        leftHand = BuildHandVisual(leftHandRoot, "Left", leftHandColor);
        rightHand = BuildHandVisual(rightHandRoot, "Right", rightHandColor);
    }

    private void EnsureLineMaterial()
    {
        if (lineMaterial != null)
            return;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        lineMaterial = new Material(shader);
        lineMaterial.color = Color.white;
        if (lineMaterial.HasProperty("_BaseColor"))
            lineMaterial.SetColor("_BaseColor", Color.white);
    }

    private HandVisual BuildHandVisual(Transform root, string label, Color color)
    {
        Material markerMaterial = CreateEmissiveMaterial(color);
        Material tipMaterial = CreateEmissiveMaterial(Color.Lerp(color, Color.white, 0.35f));
        Material controllerMaterial = CreateEmissiveMaterial(Color.Lerp(color, new Color(0.15f, 0.15f, 0.15f, 1f), 0.35f));

        CreateMarker(root, $"{label} Hand Marker", handMarkerRadius, markerMaterial);
        CreateControllerBody(root, $"{label} Controller", controllerMaterial);

        Transform aim = new GameObject($"{label} Aim").transform;
        aim.SetParent(root, false);
        aim.localRotation = Quaternion.identity;

        Transform tip = CreateMarker(aim, $"{label} Pointer Tip", tipMarkerRadius, tipMaterial);
        tip.localPosition = Vector3.forward * pointerLength;
        LineRenderer line = CreateLine(root, $"{label} Pointer Line", color);

        return new HandVisual
        {
            root = root,
            aim = aim,
            tip = tip,
            line = line
        };
    }

    private static Material CreateEmissiveMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        var material = new Material(shader);
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        return material;
    }

    private static Transform CreateMarker(Transform parent, string name, float radius, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * (radius * 2f);

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            Object.Destroy(collider);

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;

        return go.transform;
    }

    private static void CreateControllerBody(Transform parent, string name, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.04f);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(0.035f, 0.055f, 0.035f);

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            Object.Destroy(collider);

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    private LineRenderer CreateLine(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth * 0.35f;
        line.numCapVertices = 6;
        line.numCornerVertices = 4;
        line.material = lineMaterial;
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, 0.35f);
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = true;
        line.alignment = LineAlignment.View;
        return line;
    }

    private void UpdateHand(HandVisual hand, XRNode node, Color color, bool left)
    {
        if (hand.root == null)
            return;

        hand.root.gameObject.SetActive(true);

        if (XRInputBridge.TryGetLocalHandPointerPose(node, out Vector3 localPos, out Quaternion localRot))
        {
            hand.root.localPosition = localPos;
            hand.root.localRotation = localRot;
        }
        else
        {
            ApplyFallbackHandPose(hand.root, left);
        }

        // Compute the aim direction every frame so the visible laser and the click ray always match.
        // Start from the controller's rotation, apply the 0-360 direction offset, then optionally
        // guarantee it points away from the player so it can never aim back at the headset.
        Quaternion aimRot = hand.root.rotation * Quaternion.Euler(pointerAngleX, pointerAngleY, pointerAngleZ);
        Vector3 aimDir = aimRot * Vector3.forward;
        if (aimDir.sqrMagnitude < 0.0001f)
            aimDir = hand.root.forward;

        if (keepPointingForward && cameraTransform != null)
        {
            Vector3 camFwd = cameraTransform.forward;
            camFwd.y = 0f;
            Vector3 aimFlat = new Vector3(aimDir.x, 0f, aimDir.z);
            if (camFwd.sqrMagnitude > 0.0001f && aimFlat.sqrMagnitude > 0.0001f
                && Vector3.Dot(aimFlat.normalized, camFwd.normalized) < 0f)
            {
                aimDir = Quaternion.AngleAxis(180f, Vector3.up) * aimDir;
            }
        }

        aimDir.Normalize();
        if (hand.aim != null)
            hand.aim.rotation = Quaternion.LookRotation(aimDir, Vector3.up);

        Vector3 origin = GetPointerOrigin(hand);

        // Stop the laser exactly where it crosses a UI panel so the dot marks the real click point.
        float beamLength = pointerLength;
        if (TryGetCanvasHitPoint(new Ray(origin, aimDir), out Vector3 canvasHit))
            beamLength = Mathf.Min(pointerLength, Vector3.Distance(origin, canvasHit));

        Vector3 tipWorld = origin + aimDir * beamLength;

        if (hand.tip != null)
            hand.tip.position = tipWorld;
        if (hand.line != null)
        {
            hand.line.SetPosition(0, origin);
            hand.line.SetPosition(1, tipWorld);
            hand.line.startColor = color;
            hand.line.endColor = new Color(color.r, color.g, color.b, 0.35f);
        }
    }

    private void ApplyFallbackHandPose(Transform handRoot, bool left)
    {
        if (cameraTransform != null)
        {
            handRoot.localPosition = cameraTransform.localPosition
                + cameraTransform.right * (left ? -0.24f : 0.24f)
                + cameraTransform.forward * 0.38f
                - cameraTransform.up * 0.12f;
            handRoot.localRotation = cameraTransform.localRotation;
            return;
        }

        handRoot.localPosition = new Vector3(left ? -0.24f : 0.24f, 1.05f, 0.38f);
        handRoot.localRotation = Quaternion.identity;
    }

    private Vector3 GetPointerOrigin(HandVisual hand)
    {
        if (hand.aim != null)
            return hand.aim.TransformPoint(pointerLocalOriginOffset);

        return hand.root != null
            ? hand.root.TransformPoint(pointerLocalOriginOffset)
            : Vector3.zero;
    }

    private void HidePlayerBodyRenderers()
    {
        if (playerBodyHidden)
            return;

        for (int i = 0; i < playerBodyRenderers.Length; i++)
        {
            if (playerBodyRenderers[i] != null)
                playerBodyRenderers[i].enabled = false;
        }

        playerBodyHidden = true;
    }

    private void RestorePlayerBodyRenderers()
    {
        if (!playerBodyHidden)
            return;

        for (int i = 0; i < playerBodyRenderers.Length; i++)
        {
            if (playerBodyRenderers[i] != null)
                playerBodyRenderers[i].enabled = true;
        }

        playerBodyHidden = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        RestorePlayerBodyRenderers();
    }
}
