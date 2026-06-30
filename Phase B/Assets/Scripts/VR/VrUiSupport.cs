using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared VR UI helpers: canvas setup for the HMD, pointer rays, and text entry in login/forms.
/// </summary>
public static class VrUiSupport
{
    public const float DefaultMenuPlaneDistance = 2.0f;
    public const float DefaultHudPlaneDistance = 1.6f;
    public const float WorldCanvasScale = 0.00125f;

    public static void RefreshAllCanvases(Camera vrCamera, float planeDistance = DefaultMenuPlaneDistance)
    {
        if (vrCamera == null)
            return;

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].rootCanvas != canvases[i])
                continue;

            ConfigureCanvasForVr(canvases[i], vrCamera, planeDistance);
        }
    }

    public static void ConfigureCanvasForVr(Canvas canvas, Camera vrCamera, float planeDistance)
    {
        if (canvas == null || vrCamera == null)
            return;

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = vrCamera;

        RectTransform rect = canvas.transform as RectTransform;
        if (rect != null)
        {
            if (rect.sizeDelta.x < 100f || rect.sizeDelta.y < 100f)
                rect.sizeDelta = new Vector2(1920f, 1080f);

            // Center the canvas on the focal point so the panel sits squarely in front of the player.
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.position = vrCamera.transform.position + vrCamera.transform.forward * planeDistance;
            rect.rotation = vrCamera.transform.rotation;
            rect.localScale = Vector3.one * WorldCanvasScale;
        }

        EnsureGraphicRaycaster(canvas);
    }

    public static void ConfigureRuntimeCanvas(Canvas canvas, Camera vrCamera = null)
    {
        if (canvas == null)
            return;

        if (vrCamera == null)
            vrCamera = Camera.main;

        ConfigureCanvasForVr(canvas, vrCamera, DefaultMenuPlaneDistance);
    }

    private static void EnsureGraphicRaycaster(Canvas canvas)
    {
        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    public static bool TryGetRightPointerScreenPoint(Camera camera, out Vector2 screenPoint, float planeDistance = DefaultMenuPlaneDistance)
    {
        screenPoint = default;
        if (camera == null)
            return false;

        if (VrHandPointers.Instance != null &&
            VrHandPointers.Instance.TryGetRightPointerScreenPoint(camera, out screenPoint))
        {
            return true;
        }

        return TryHeadRayScreenPoint(camera, planeDistance, out screenPoint);
    }

    public static bool TryHeadRayScreenPoint(Camera camera, float planeDistance, out Vector2 screenPoint)
    {
        screenPoint = default;
        if (camera == null)
            return false;

        Ray ray = new Ray(camera.transform.position, camera.transform.forward);
        return TryRayToScreenPoint(camera, ray, planeDistance, out screenPoint);
    }

    public static bool TryRayToScreenPoint(Camera camera, Ray ray, float planeDistance, out Vector2 screenPoint)
    {
        screenPoint = default;
        if (camera == null)
            return false;

        if (VrHandPointers.TryProjectRayToCanvasScreenPoint(camera, ray, out screenPoint))
            return true;

        Plane plane = new Plane(camera.transform.forward,
            camera.transform.position + camera.transform.forward * planeDistance);
        if (!plane.Raycast(ray, out float distance) || distance <= 0f)
            return false;

        Vector3 world = ray.GetPoint(distance);
        Vector3 screen = camera.WorldToScreenPoint(world);
        if (screen.z <= 0f)
            return false;

        screenPoint = new Vector2(screen.x, screen.y);
        return true;
    }

    public static Ray GetInteractionRay()
    {
        if (VrGameplayInput.ShouldUseVrControls &&
            VrHandPointers.Instance != null &&
            VrHandPointers.Instance.TryGetRightPointerRay(out Ray handRay))
        {
            return handRay;
        }

        Camera cam = Camera.main;
        if (cam == null)
            return default;

        return new Ray(cam.transform.position, cam.transform.forward);
    }

    public static void ActivateInputFieldForVr(TMP_InputField field)
    {
        if (field == null)
            return;

        field.Select();
        field.ActivateInputField();

        if (!VrGameplayInput.ShouldUseVrControls)
            return;

        VrVirtualKeyboard.Show(field);
    }

    public static bool TryActivateInputFieldFromUiTarget(GameObject target)
    {
        if (target == null)
            return false;

        TMP_InputField field = target.GetComponentInParent<TMP_InputField>();
        if (field == null)
            return false;

        ActivateInputFieldForVr(field);
        return true;
    }
}
