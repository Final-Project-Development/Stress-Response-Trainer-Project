using UnityEngine;

/// <summary>
/// Simple runtime layout for top bar elements to avoid overlap.
/// Places progress container on the left and main buttons on the top-right.
/// </summary>
public class TopBarLayoutController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform topBarRoot;
    [SerializeField] private RectTransform leftContainer;       // Experience_simulation_Container
    [SerializeField] private RectTransform pauseButton;
    [SerializeField] private RectTransform backButton;
    [SerializeField] private RectTransform helpButton;
    [SerializeField] private RectTransform extraCenterObject;   // Optional

    [Header("Layout")]
    [SerializeField] private float topY = -18f;
    [SerializeField] private float leftPadding = 20f;
    [SerializeField] private float rightPadding = 26f;
    [SerializeField] private float rightSpacing = 92f;
    [SerializeField] private bool applyInStart = true;

    private void Start()
    {
        if (applyInStart)
            ApplyLayout();
    }

    [ContextMenu("Apply TopBar Layout")]
    public void ApplyLayout()
    {
        if (topBarRoot == null)
            topBarRoot = GetComponent<RectTransform>();
        if (topBarRoot == null)
            return;

        // Left area: progress container
        if (leftContainer != null)
        {
            leftContainer.anchorMin = new Vector2(0f, 1f);
            leftContainer.anchorMax = new Vector2(0f, 1f);
            leftContainer.pivot = new Vector2(0f, 1f);
            leftContainer.anchoredPosition = new Vector2(leftPadding, topY);
        }

        // Right row (Pause / Back / Help), ordered left->right as pause, back, help.
        PlaceRight(helpButton, 0f, topY, rightPadding);
        PlaceRight(backButton, rightSpacing, topY, rightPadding);
        PlaceRight(pauseButton, rightSpacing * 2f, topY, rightPadding);

        if (extraCenterObject != null)
            PlaceRight(extraCenterObject, rightSpacing * 3f, topY, rightPadding);
    }

    private static void PlaceRight(RectTransform rt, float offsetFromRight, float y, float rightPadding)
    {
        if (rt == null)
            return;

        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-(rightPadding + offsetFromRight), y);
    }
}
