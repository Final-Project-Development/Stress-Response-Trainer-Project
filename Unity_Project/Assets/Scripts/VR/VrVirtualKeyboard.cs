using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Simple world-space keyboard for VR text entry. It is intentionally small and self-contained:
/// point at a key with the right controller and pull the trigger.
/// </summary>
[DefaultExecutionOrder(-4800)]
public sealed class VrVirtualKeyboard : MonoBehaviour
{
    private static VrVirtualKeyboard instance;

    private const float CanvasScale = 0.00082f;
    private const float DistanceFromCamera = 1.62f;

    private Canvas canvas;
    private RectTransform canvasRect;
    private TMP_InputField targetField;
    private bool shift;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        var go = new GameObject("VR Virtual Keyboard", typeof(RectTransform));
        go.AddComponent<VrVirtualKeyboard>();
    }

    public static void Show(TMP_InputField field)
    {
        if (field == null)
            return;

        EnsureInstance();
        if (instance == null)
            return;

        instance.Open(field);
    }

    public static void Hide()
    {
        if (instance != null)
            instance.Close();
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
        BuildKeyboard();
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (targetField == null)
        {
            Close();
            return;
        }

        PositionInFrontOfCamera();
    }

    private void Open(TMP_InputField field)
    {
        targetField = field;
        targetField.Select();
        targetField.ActivateInputField();
        gameObject.SetActive(true);
        PositionInFrontOfCamera();
    }

    private void Close()
    {
        if (targetField != null)
            targetField.DeactivateInputField();

        targetField = null;
        shift = false;
        gameObject.SetActive(false);
    }

    private void PositionInFrontOfCamera()
    {
        Camera cam = Camera.main;
        if (cam == null || canvasRect == null)
            return;

        canvas.worldCamera = cam;
        Vector3 center = cam.transform.position + cam.transform.forward * DistanceFromCamera;
        center -= cam.transform.up * 0.62f;

        canvasRect.position = center;
        canvasRect.rotation = cam.transform.rotation;
        canvasRect.localScale = Vector3.one * CanvasScale;
    }

    private void BuildKeyboard()
    {
        canvasRect = transform as RectTransform;
        if (canvasRect == null)
            return;

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 1000;
        canvasRect.sizeDelta = new Vector2(1180f, 390f);

        gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        Image bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.05f, 0.07f, 0.94f);

        CreateRow("1234567890", -135f, 0f, 82f);
        CreateRow("qwertyuiop", -45f, 0f, 82f);
        CreateRow("asdfghjkl", 45f, 41f, 82f);
        CreateRow("zxcvbnm", 135f, 82f, 82f);

        CreateKey("Shift", new Vector2(-430f, 135f), new Vector2(132f, 64f), ToggleShift);
        CreateKey("@", new Vector2(-260f, 205f), new Vector2(80f, 58f), () => Append("@"));
        CreateKey(".", new Vector2(-165f, 205f), new Vector2(80f, 58f), () => Append("."));
        CreateKey("_", new Vector2(-70f, 205f), new Vector2(80f, 58f), () => Append("_"));
        CreateKey("-", new Vector2(25f, 205f), new Vector2(80f, 58f), () => Append("-"));
        CreateKey("Space", new Vector2(190f, 205f), new Vector2(210f, 58f), () => Append(" "));
        CreateKey("Back", new Vector2(475f, 135f), new Vector2(132f, 64f), Backspace);
        CreateKey("Clear", new Vector2(430f, 205f), new Vector2(120f, 58f), Clear);
        CreateKey("Done", new Vector2(555f, -135f), new Vector2(132f, 64f), Close);
    }

    private void CreateRow(string chars, float y, float xOffset, float keyWidth)
    {
        float spacing = keyWidth + 10f;
        float startX = -((chars.Length - 1) * spacing) * 0.5f + xOffset;
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            CreateKey(c.ToString(), new Vector2(startX + i * spacing, y), new Vector2(keyWidth, 64f), () => Append(c.ToString()));
        }
    }

    private void CreateKey(string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject("Key_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.color = new Color(0.16f, 0.19f, 0.26f, 0.98f);

        var button = go.GetComponent<Button>();
        button.onClick.AddListener(action);

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textGo.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 26;
        text.color = Color.white;
    }

    private void Append(string value)
    {
        if (targetField == null)
            return;

        string text = shift ? value.ToUpperInvariant() : value;
        targetField.text += text;
        targetField.caretPosition = targetField.text.Length;
        if (shift)
            shift = false;
    }

    private void Backspace()
    {
        if (targetField == null || string.IsNullOrEmpty(targetField.text))
            return;

        targetField.text = targetField.text.Substring(0, targetField.text.Length - 1);
        targetField.caretPosition = targetField.text.Length;
    }

    private void Clear()
    {
        if (targetField == null)
            return;

        targetField.text = string.Empty;
        targetField.caretPosition = 0;
    }

    private void ToggleShift()
    {
        shift = !shift;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }
}
