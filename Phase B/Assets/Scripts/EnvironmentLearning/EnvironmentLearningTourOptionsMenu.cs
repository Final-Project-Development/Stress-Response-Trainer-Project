using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Options panel inside <see cref="EnvironmentLearningTourGuide"/> (Background (1)):
/// alarm toggle + start Simulation 1/2 from the current tour position.
/// </summary>
public class EnvironmentLearningTourOptionsMenu : MonoBehaviour
{
    [Header("Child object names (auto-find when empty)")]
    public string titleObjectName = "learnMenue";
    public string toggleAlarmButtonName = "addAlarm";
    public string startSim1ButtonName = "startsim1";
    public string startSim2ButtonName = "startsim2";

    [Header("Presentation")]
    public string titleText = "Tour options";
    public string alarmOnButtonLabel = "Alarm on";
    public string alarmOffButtonLabel = "Alarm off";
    public string startSim1ButtonLabel = "Simulation 1";
    public string startSim2ButtonLabel = "Simulation 2";

    readonly List<(Button button, UnityAction action)> _wiredButtons = new List<(Button, UnityAction)>();

    public void Wire()
    {
        Unwire();
        DisableMisplacedNavButtons();
        ApplyPresentation();

        WireButton(FindChildByName(transform, toggleAlarmButtonName), OnToggleAlarmClicked);
        WireButton(ResolveStartSimButton(startSim1ButtonName, pickHighestY: true), OnStartSim1Clicked);
        WireButton(ResolveStartSimButton(startSim2ButtonName, pickHighestY: false), OnStartSim2Clicked);
    }

    public void Unwire()
    {
        for (int i = 0; i < _wiredButtons.Count; i++)
        {
            if (_wiredButtons[i].button != null)
                _wiredButtons[i].button.onClick.RemoveListener(_wiredButtons[i].action);
        }

        _wiredButtons.Clear();
    }

    public void ApplyPresentation()
    {
        Transform title = FindChildByName(transform, titleObjectName);
        if (title != null)
        {
            var tmp = title.GetComponent<TextMeshProUGUI>() ?? title.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null && !string.IsNullOrWhiteSpace(titleText))
                tmp.text = titleText;
        }

        Transform alarmButton = FindChildByName(transform, toggleAlarmButtonName);
        if (alarmButton != null)
        {
            bool alarmOn = ResolveFlow() != null && ResolveFlow().IsEnvironmentLearningTourAlarmActive;
            SetButtonCaption(alarmButton, alarmOn ? alarmOnButtonLabel : alarmOffButtonLabel);
        }

        SetButtonCaption(ResolveStartSimButton(startSim1ButtonName, pickHighestY: true), startSim1ButtonLabel);
        SetButtonCaption(ResolveStartSimButton(startSim2ButtonName, pickHighestY: false), startSim2ButtonLabel);
    }

    void WireButton(Transform buttonTransform, UnityAction action)
    {
        if (buttonTransform == null || action == null)
            return;

        var button = buttonTransform.GetComponent<Button>();
        if (button == null)
            return;

        button.onClick.AddListener(action);
        _wiredButtons.Add((button, action));
    }

    Transform ResolveStartSimButton(string buttonName, bool pickHighestY)
    {
        Transform direct = FindChildByName(transform, buttonName);
        if (direct != null)
            return direct;

        if (buttonName != startSim2ButtonName)
            return null;

        var duplicateSim1Buttons = new List<Transform>();
        CollectChildrenByName(transform, startSim1ButtonName, duplicateSim1Buttons);
        if (duplicateSim1Buttons.Count < 2)
            return null;

        return pickHighestY
            ? duplicateSim1Buttons.OrderByDescending(GetAnchoredY).First()
            : duplicateSim1Buttons.OrderBy(GetAnchoredY).First();
    }

    static float GetAnchoredY(Transform transform)
    {
        var rect = transform.GetComponent<RectTransform>();
        return rect != null ? rect.anchoredPosition.y : 0f;
    }

    void DisableMisplacedNavButtons()
    {
        var navButtons = GetComponentsInChildren<EnvironmentLearningTourNavButton>(true);
        for (int i = 0; i < navButtons.Length; i++)
        {
            if (navButtons[i] != null)
                navButtons[i].enabled = false;
        }
    }

    static void SetButtonCaption(Transform buttonRoot, string label)
    {
        if (buttonRoot == null || string.IsNullOrWhiteSpace(label))
            return;

        var tmp = buttonRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
            tmp.text = label;
    }

    static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        string target = NormalizeName(childName);
        if (NormalizeName(root.name) == target)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    static void CollectChildrenByName(Transform root, string childName, List<Transform> results)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName) || results == null)
            return;

        if (NormalizeName(root.name) == NormalizeName(childName))
            results.Add(root);

        for (int i = 0; i < root.childCount; i++)
            CollectChildrenByName(root.GetChild(i), childName, results);
    }

    static string NormalizeName(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    static TrainingFlowController ResolveFlow() =>
        FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

    void OnToggleAlarmClicked() => ResolveFlow()?.UI_ToggleEnvironmentLearningAlarm();

    void OnStartSim1Clicked() => ResolveFlow()?.UI_StartSimulation1FromTour();

    void OnStartSim2Clicked() => ResolveFlow()?.UI_StartSimulation2FromTour();
}
