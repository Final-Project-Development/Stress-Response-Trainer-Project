using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// VR UI clicks via the right controller pointer (fallback: head direction).
/// </summary>
[DefaultExecutionOrder(-4700)]
public sealed class VRGazeUiPointer : MonoBehaviour
{
    private static VRGazeUiPointer instance;

    public static int LastConsumedInteractFrame { get; private set; } = -1;

    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>(32);
    private PointerEventData pointerEventData;
    private EventSystem pointerEventSystem;
    private GameObject currentHover;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        var go = new GameObject("VR Gaze UI Pointer");
        go.AddComponent<VRGazeUiPointer>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureEventSystem();
    }

    private void LateUpdate()
    {
        if (!VrGameplayInput.ShouldUseVrControls)
            return;

        Camera uiCamera = Camera.main;
        if (uiCamera == null)
            return;

        EventSystem eventSystem = EnsureEventSystem();
        if (eventSystem == null)
            return;

        if (pointerEventData == null || pointerEventSystem != eventSystem)
        {
            pointerEventSystem = eventSystem;
            pointerEventData = new PointerEventData(eventSystem);
        }

        if (!VrUiSupport.TryGetRightPointerScreenPoint(uiCamera, out Vector2 screenPoint))
        {
            UpdateHover(null);
            return;
        }

        pointerEventData.Reset();
        pointerEventData.button = PointerEventData.InputButton.Left;
        pointerEventData.position = screenPoint;
        pointerEventData.clickCount = 1;
        pointerEventData.clickTime = Time.unscaledTime;

        raycastResults.Clear();
        eventSystem.RaycastAll(pointerEventData, raycastResults);
        if (raycastResults.Count == 0)
        {
            UpdateHover(null);
            return;
        }

        RaycastResult hit = raycastResults[0];
        GameObject hoverTarget = hit.gameObject;
        pointerEventData.pointerCurrentRaycast = hit;
        pointerEventData.pointerPressRaycast = hit;
        UpdateHover(hoverTarget);

        if (!XRInputBridge.InteractPressed)
            return;

        // Only consume the trigger for actual interactive controls. Pointing at a plain HUD/text panel
        // must let the trigger fall through to world interaction (e.g. the phone booth, pickups).
        bool isInteractive = hoverTarget.GetComponentInParent<Selectable>() != null
            || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hoverTarget) != null;
        if (!isInteractive)
            return;

        eventSystem.SetSelectedGameObject(hoverTarget, pointerEventData);

        if (VrUiSupport.TryActivateInputFieldFromUiTarget(hoverTarget))
        {
            LastConsumedInteractFrame = Time.frameCount;
            return;
        }

        ExecuteEvents.ExecuteHierarchy(hoverTarget, pointerEventData, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hoverTarget, pointerEventData, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hoverTarget, pointerEventData, ExecuteEvents.pointerClickHandler);
        LastConsumedInteractFrame = Time.frameCount;
    }

    private void UpdateHover(GameObject target)
    {
        GameObject hover = null;
        if (target != null)
        {
            Selectable selectable = target.GetComponentInParent<Selectable>();
            hover = selectable != null ? selectable.gameObject : target;
        }

        if (hover == currentHover)
            return;

        if (currentHover != null)
            ExecuteEvents.Execute(currentHover, pointerEventData, ExecuteEvents.pointerExitHandler);

        currentHover = hover;

        if (currentHover != null)
            ExecuteEvents.Execute(currentHover, pointerEventData, ExecuteEvents.pointerEnterHandler);
    }

    private static EventSystem EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return EventSystem.current;

        var go = new GameObject("EventSystem");
        var eventSystem = go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
        return eventSystem;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
