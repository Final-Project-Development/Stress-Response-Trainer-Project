using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows temporary world labels (LabelAnchor) above current mission target(s).
/// </summary>
public class MissionHintService : MonoBehaviour
{
    [Tooltip("How long the world label stays visible after Hint is pressed.")]
    public float hintVisibleSeconds = 14f;

    readonly List<WorldItemLabel> _activeHintLabels = new List<WorldItemLabel>();
    Coroutine _hideRoutine;

    public void ShowHintForObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return;

        ShowHintsForObjects(new[] { objectName });
    }

    public void ShowHintsForPickups(IReadOnlyList<PickUpItem> pickups)
    {
        HideActiveHint();
        if (pickups == null || pickups.Count == 0)
            return;

        var learning = FindFirstObjectByType<EnvironmentLearningController>(FindObjectsInactive.Include);
        int shown = 0;

        for (int i = 0; i < pickups.Count; i++)
        {
            var pickup = pickups[i];
            if (pickup == null || pickup.WasPickedUp || !pickup.gameObject.activeInHierarchy)
                continue;

            WorldItemLabel label = pickup.GetComponent<WorldItemLabel>();
            if (label == null && !TryFindWorldItemLabel(pickup.gameObject.name, out label))
            {
                Debug.LogWarning(
                    $"Mission hint: no WorldItemLabel on '{pickup.gameObject.name}'. " +
                    "Run Tools → Stress Trainer → Setup Environment Learning Tour Labels.");
                continue;
            }

            EnsureHostHierarchyActive(label.gameObject);
            label.EnsureLabelBuilt();
            if (learning != null)
                label.ApplyAppearanceFromController();

            label.SetVisible(true);
            _activeHintLabels.Add(label);
            shown++;
        }

        if (shown > 0)
            _hideRoutine = StartCoroutine(HideAfterDelay());
    }

    public void ShowHintsForObjects(IReadOnlyList<string> objectNames)
    {
        HideActiveHint();
        if (objectNames == null || objectNames.Count == 0)
            return;

        var learning = FindFirstObjectByType<EnvironmentLearningController>(FindObjectsInactive.Include);
        int shown = 0;

        for (int i = 0; i < objectNames.Count; i++)
        {
            string objectName = objectNames[i];
            if (string.IsNullOrWhiteSpace(objectName))
                continue;

            if (!TryFindWorldItemLabel(objectName, out WorldItemLabel label))
            {
                Debug.LogWarning(
                    $"Mission hint: no WorldItemLabel on '{objectName}'. " +
                    "Run Tools → Stress Trainer → Setup Tour Labels.");
                continue;
            }

            EnsureHostHierarchyActive(label.gameObject);
            label.EnsureLabelBuilt();
            if (learning != null)
                label.ApplyAppearanceFromController();

            label.SetVisible(true);
            _activeHintLabels.Add(label);
            shown++;
        }

        if (shown > 0)
            _hideRoutine = StartCoroutine(HideAfterDelay());
    }

    public void HideActiveHint()
    {
        if (_hideRoutine != null)
        {
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        for (int i = 0; i < _activeHintLabels.Count; i++)
        {
            if (_activeHintLabels[i] != null)
                _activeHintLabels[i].SetVisible(false);
        }

        _activeHintLabels.Clear();
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(1f, hintVisibleSeconds));
        HideActiveHint();
    }

    public static bool TryFindWorldItemLabel(string objectName, out WorldItemLabel label)
    {
        label = null;
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        foreach (var worldLabel in FindObjectsByType<WorldItemLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (worldLabel == null)
                continue;

            if (string.Equals(worldLabel.gameObject.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                label = worldLabel;
                return true;
            }
        }

        var exact = GameObject.Find(objectName);
        if (exact != null)
        {
            label = exact.GetComponent<WorldItemLabel>();
            if (label != null)
                return true;
        }

        var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            if (t == null || t.GetComponent<RectTransform>() != null)
                continue;

            if (!string.Equals(t.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            label = t.GetComponent<WorldItemLabel>();
            if (label != null)
                return true;
        }

        return false;
    }

    static void EnsureHostHierarchyActive(GameObject host)
    {
        if (host == null)
            return;

        Transform current = host.transform;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                current.gameObject.SetActive(true);
            current = current.parent;
        }
    }
}
