using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Full-screen "Waiting for data from the watch…" overlay shown after a simulation
/// ends and before the results window, until watch data arrives (or the user skips).
/// Built entirely at runtime so it needs no scene wiring. In VR the canvas is picked
/// up and converted to world space by <see cref="QuestVrRigBridge.ForceRefreshCanvases"/>.
/// </summary>
public class WatchResultWaitOverlay : MonoBehaviour
{
    /// <summary>Raised when the user presses the "Continue anyway" button.</summary>
    public event Action OnContinue;

    private Canvas _canvas;
    private TextMeshProUGUI _message;
    private GameObject _continueButtonRoot;

    public static WatchResultWaitOverlay Show(string message)
    {
        var go = new GameObject("WatchResultWaitOverlay");
        var overlay = go.AddComponent<WatchResultWaitOverlay>();
        overlay.Build(message);
        return overlay;
    }

    private void Build(string message)
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        // Dim background covering the whole screen.
        var dim = CreateChild("Dim", transform);
        var dimImage = dim.gameObject.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.72f);
        Stretch(dim);

        // Centered card.
        var card = CreateChild("Card", transform);
        var cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = new Color(0.18f, 0.16f, 0.28f, 0.96f);
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(900f, 420f);
        card.anchoredPosition = Vector2.zero;

        // Message text.
        var text = CreateChild("Message", card);
        _message = text.gameObject.AddComponent<TextMeshProUGUI>();
        _message.text = message;
        _message.fontSize = 44f;
        _message.alignment = TextAlignmentOptions.Center;
        _message.color = Color.white;
        _message.enableWordWrapping = true;
        text.anchorMin = new Vector2(0.05f, 0.35f);
        text.anchorMax = new Vector2(0.95f, 0.95f);
        text.offsetMin = Vector2.zero;
        text.offsetMax = Vector2.zero;

        // "Continue anyway" button (hidden until the timeout elapses).
        var buttonRt = CreateChild("ContinueButton", card);
        _continueButtonRoot = buttonRt.gameObject;
        var buttonImage = buttonRt.gameObject.AddComponent<Image>();
        buttonImage.color = new Color(0.45f, 0.36f, 0.75f, 1f);
        buttonRt.anchorMin = new Vector2(0.5f, 0.05f);
        buttonRt.anchorMax = new Vector2(0.5f, 0.05f);
        buttonRt.pivot = new Vector2(0.5f, 0f);
        buttonRt.sizeDelta = new Vector2(420f, 90f);
        buttonRt.anchoredPosition = new Vector2(0f, 18f);

        var button = buttonRt.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(HandleContinuePressed);

        var label = CreateChild("Label", buttonRt);
        var labelText = label.gameObject.AddComponent<TextMeshProUGUI>();
        labelText.text = "Continue anyway";
        labelText.fontSize = 34f;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;
        Stretch(label);

        _continueButtonRoot.SetActive(false);

        QuestVrRigBridge.ForceRefreshCanvases();
    }

    public void SetMessage(string message)
    {
        if (_message != null)
            _message.text = message;
    }

    public void ShowContinueButton()
    {
        if (_continueButtonRoot != null && !_continueButtonRoot.activeSelf)
        {
            _continueButtonRoot.SetActive(true);
            QuestVrRigBridge.ForceRefreshCanvases();
        }
    }

    private void HandleContinuePressed() => OnContinue?.Invoke();

    public void Hide()
    {
        if (this != null && gameObject != null)
            Destroy(gameObject);
    }

    private static RectTransform CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
