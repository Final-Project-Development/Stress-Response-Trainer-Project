using UnityEngine;

public class WoundedMan : MonoBehaviour
{
    [Header("First aid key sequence")]
    public KeyCode step1Key = KeyCode.Alpha1;
    public KeyCode step2Key = KeyCode.Alpha2;
    public KeyCode step3Key = KeyCode.Alpha3;

    [Header("Wounded animation (Mixamo)")]
    [Tooltip("Animator on the wounded character. If empty, tries to find one on this object/children.")]
    public Animator woundedAnimator;
    [Tooltip("Trigger fired when player presses E to start/remind first aid.")]
    public string startAidTrigger = "FirstAidStart";
    [Tooltip("Trigger fired when step 1 key is pressed correctly.")]
    public string step1Trigger = "FirstAidStep1";
    [Tooltip("Trigger fired when step 2 key is pressed correctly.")]
    public string step2Trigger = "FirstAidStep2";
    [Tooltip("Trigger fired when step 3 key is pressed correctly.")]
    public string step3Trigger = "FirstAidStep3";
    [Tooltip("Optional trigger fired when all steps are complete.")]
    public string completeTrigger = "FirstAidComplete";

    [Header("Simulation 2 prerequisites")]
    public bool requireFirstAidKit = true;
    public bool requireEmergencyReport = true;

    private GameManager gameManager;
    private bool helped = false;
    private bool treatmentStarted = false;
    private int currentStep = 0;
    private const float Sim2WoundedHintDuration = 7f;

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();

        if (woundedAnimator == null)
            woundedAnimator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (!treatmentStarted || helped)
            return;

        KeyCode expected = GetExpectedKey();
        if (Input.GetKeyDown(expected))
        {
            if (currentStep == 0)
                PlayAnimationTrigger(step1Trigger);
            else if (currentStep == 1)
                PlayAnimationTrigger(step2Trigger);
            else
                PlayAnimationTrigger(step3Trigger);

            currentStep++;
            if (currentStep >= 3)
                CompleteTreatment();
            else
                ShowStepInstruction();
            return;
        }

        if (Input.GetKeyDown(step1Key) || Input.GetKeyDown(step2Key) || Input.GetKeyDown(step3Key))
            ShowWrongKeyMessage(expected);
    }

    public void OnFirstAid()
    {
        if (helped)
            return;

        if (requireFirstAidKit && gameManager != null && !gameManager.HasFirstAidKit())
        {
            var flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
            string msg = flow != null ? flow.sim2NeedKitHint : "Find the first aid kit in the city before treating the wounded.";
            gameManager.ShowTransientMissionNote(msg, Sim2WoundedHintDuration);
            return;
        }

        if (requireEmergencyReport && gameManager != null && !gameManager.HasReportedEmergency())
        {
            var flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
            if (!gameManager.HasContactedCasualty())
            {
                gameManager.OnCasualtyApproached();
            }
            else if (!gameManager.HasMissionStatusPanel())
            {
                string msg = flow != null
                    ? flow.sim2ObjectiveGoToPhone
                    : "Go to the public telephone and open the door.";
                gameManager.ShowMissionMessage(msg, Sim2WoundedHintDuration);
            }
            else
            {
                gameManager.RefreshSimulation2MissionObjective();
            }

            return;
        }

        if (!treatmentStarted)
        {
            treatmentStarted = true;
            currentStep = 0;
            PlayAnimationTrigger(startAidTrigger);
            ShowStepInstruction();
            return;
        }

        PlayAnimationTrigger(startAidTrigger);
        ShowStepInstruction();
    }

    private KeyCode GetExpectedKey()
    {
        if (currentStep == 0) return step1Key;
        if (currentStep == 1) return step2Key;
        return step3Key;
    }

    private void ShowStepInstruction()
    {
        var flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
        string completed = BuildTreatmentCompletedLine(flow);
        string objective = BuildTreatmentObjectiveLine();
        if (gameManager != null)
            gameManager.SetMissionPanelProgress(completed, objective);

        Debug.Log($"{completed} | {objective}");
    }

    private string BuildTreatmentCompletedLine(TrainingFlowController flow)
    {
        if (currentStep <= 0)
            return flow != null ? flow.sim2TreatmentStartedCompleted : "Treatment started.";

        return $"Entered: {GetTreatmentEntered(currentStep)}";
    }

    private string BuildTreatmentObjectiveLine()
    {
        KeyCode expected = GetExpectedKey();
        string remaining = GetTreatmentRemaining(currentStep);
        if (string.IsNullOrEmpty(remaining))
            return $"Press {FormatKey(expected)}.";

        return $"Press {FormatKey(expected)}. Remaining: {remaining}";
    }

    private static string GetTreatmentEntered(int completedSteps)
    {
        if (completedSteps >= 3)
            return "1, 2, 3";
        if (completedSteps == 2)
            return "1, 2";
        if (completedSteps == 1)
            return "1";
        return "—";
    }

    private static string GetTreatmentRemaining(int stepIndex)
    {
        if (stepIndex == 0)
            return "2, 3";
        if (stepIndex == 1)
            return "3";
        return string.Empty;
    }

    private void ShowWrongKeyMessage(KeyCode expected)
    {
        string remaining = GetTreatmentRemaining(currentStep);
        string objective = string.IsNullOrEmpty(remaining)
            ? $"Wrong key. Press {FormatKey(expected)}."
            : $"Wrong key. Press {FormatKey(expected)}. Remaining: {remaining}";

        if (gameManager != null)
            gameManager.SetMissionPanelProgress(null, objective);
        else
            gameManager?.ShowMissionMessage(objective, 3f);

        Debug.Log(objective);
    }

    private void CompleteTreatment()
    {
        helped = true;
        treatmentStarted = false;
        PlayAnimationTrigger(completeTrigger);

        if (gameManager != null)
            gameManager.OnFirstAidFinished();

        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = Color.green;

        Debug.Log("First aid sequence completed.");
    }

    public void ResetTreatment()
    {
        helped = false;
        treatmentStarted = false;
        currentStep = 0;
    }

    private void PlayAnimationTrigger(string triggerName)
    {
        if (woundedAnimator == null || string.IsNullOrWhiteSpace(triggerName))
            return;

        woundedAnimator.SetTrigger(triggerName);
    }

    private static string FormatKey(KeyCode key)
    {
        if (key == KeyCode.Alpha1) return "1";
        if (key == KeyCode.Alpha2) return "2";
        if (key == KeyCode.Alpha3) return "3";
        if (key == KeyCode.Alpha4) return "4";
        if (key == KeyCode.Alpha5) return "5";
        if (key == KeyCode.Alpha6) return "6";
        if (key == KeyCode.Alpha7) return "7";
        if (key == KeyCode.Alpha8) return "8";
        if (key == KeyCode.Alpha9) return "9";
        if (key == KeyCode.Alpha0) return "0";
        return key.ToString();
    }
}
