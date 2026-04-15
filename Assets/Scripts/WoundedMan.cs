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

    private GameManager gameManager;
    private bool helped = false;
    private bool treatmentStarted = false;
    private int currentStep = 0;

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
            {
                CompleteTreatment();
            }
            else
            {
                ShowStepInstruction();
            }
            return;
        }

        // If user pressed one of the treatment keys but not the expected one, show guidance.
        if (Input.GetKeyDown(step1Key) || Input.GetKeyDown(step2Key) || Input.GetKeyDown(step3Key))
            ShowWrongKeyMessage(expected);
    }

    public void OnFirstAid() 
    {
        if (helped)
            return;

        if (!treatmentStarted)
        {
            treatmentStarted = true;
            currentStep = 0;
            PlayAnimationTrigger(startAidTrigger);
            ShowStepInstruction();
            return;
        }

        // If E is pressed again during treatment, remind the player what key is expected.
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
        KeyCode expected = GetExpectedKey();
        string stepName = currentStep == 0 ? "Step 1/3" : currentStep == 1 ? "Step 2/3" : "Step 3/3";
        string msg = $"Treat wounded: {stepName}. Press [{FormatKey(expected)}].";
        if (gameManager != null)
            gameManager.ShowMissionMessage(msg, 4.5f);
        Debug.Log(msg);
    }

    private void ShowWrongKeyMessage(KeyCode expected)
    {
        string msg = $"Wrong key. Press [{FormatKey(expected)}] for this step.";
        if (gameManager != null)
            gameManager.ShowMissionMessage(msg, 3f);
        Debug.Log(msg);
    }

    private void CompleteTreatment()
    {
        helped = true;
        treatmentStarted = false;
        PlayAnimationTrigger(completeTrigger);

        if (gameManager != null)
            gameManager.OnFirstAidFinished();

        // simple feedback: change color (optional)
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = Color.green;

        Debug.Log("First aid sequence completed.");
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
