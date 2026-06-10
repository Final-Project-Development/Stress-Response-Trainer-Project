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

    [Header("Interaction range")]
    [Tooltip("How far the player can press E on the casualty, and 1/2/3 during treatment.")]
    public float interactDistance = 6f;
    [Tooltip("Lower = easier to register without aiming at a specific body part.")]
    public float interactFacingThreshold = 0.2f;

    private GameManager gameManager;
    private bool helped = false;
    private bool treatmentStarted = false;
    private int currentStep = 0;
    private Collider _interactCollider;
    private const float Sim2WoundedHintDuration = 7f;

    public float InteractDistance => interactDistance;

    public bool IsTreatmentActive => treatmentStarted && !helped;

    public int TreatmentStepIndex => currentStep;

    void Awake()
    {
        if (woundedAnimator == null)
            woundedAnimator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
    }

    void OnEnable()
    {
        EnsureBodyInteractCollider();
    }

    void Update()
    {
        if (!treatmentStarted || helped)
            return;

        if (!IsPlayerWithinInteractRange())
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
        {
            if (gameManager.HasMissionStatusPanel())
            {
                gameManager.SetMissionCompletedLine(completed);
                gameManager.RefreshSimulation2MissionObjective();
            }
            else
            {
                gameManager.SetMissionPanelProgress(completed, objective);
            }
        }

        Debug.Log($"{completed} | {objective}");
    }

    public string GetProximityTreatmentObjective(TrainingFlowController flow)
    {
        if (flow == null)
            return BuildTreatmentObjectiveLine();

        if (currentStep <= 0)
            return flow.sim2TreatWoundedPress1Action;
        if (currentStep == 1)
            return flow.sim2TreatWoundedPress2Action;
        return flow.sim2TreatWoundedPress3Action;
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
        {
            if (gameManager.HasMissionStatusPanel())
                gameManager.RefreshSimulation2MissionObjective();
            else
                gameManager.SetMissionPanelProgress(null, objective);
        }
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

    public Vector3 GetInteractCenter()
    {
        EnsureBodyInteractCollider();
        if (_interactCollider != null)
            return _interactCollider.bounds.center;

        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return transform.position;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds.center;
    }

    public bool IsPlayerWithinInteractRange()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return true;

        return IsWithinInteractRange(cam.transform.position);
    }

    public bool IsWithinInteractRange(Vector3 observerPosition)
    {
        float maxDist = interactDistance;
        float maxDistSqr = maxDist * maxDist;

        Bounds bounds = GetInteractBounds();
        if (bounds.SqrDistance(observerPosition) <= maxDistSqr)
            return true;

        return (GetInteractCenter() - observerPosition).sqrMagnitude <= maxDistSqr;
    }

    public bool IsFacingForInteract(Camera cam)
    {
        if (cam == null)
            return false;

        if (!IsWithinInteractRange(cam.transform.position))
            return false;

        Bounds bounds = GetInteractBounds();
        Vector3 eyePosition = cam.transform.position;
        Vector3 viewDirection = cam.transform.forward;

        Vector3 closestOnBounds = bounds.ClosestPoint(eyePosition + viewDirection * interactDistance);
        Vector3 toClosest = closestOnBounds - eyePosition;
        if (toClosest.sqrMagnitude > 0.0001f
            && Vector3.Dot(viewDirection, toClosest.normalized) >= interactFacingThreshold)
            return true;

        Vector3 toCenter = bounds.center - eyePosition;
        if (toCenter.sqrMagnitude <= 0.0001f)
            return true;

        return Vector3.Dot(viewDirection, toCenter.normalized) >= interactFacingThreshold;
    }

    private Bounds GetInteractBounds()
    {
        EnsureBodyInteractCollider();
        if (_interactCollider != null)
            return _interactCollider.bounds;

        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return new Bounds(transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private void EnsureBodyInteractCollider()
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                worldBounds.Encapsulate(renderers[i].bounds);
        }

        Transform volume = transform.Find("InteractVolume");
        if (volume == null)
        {
            var go = new GameObject("InteractVolume");
            volume = go.transform;
            volume.SetParent(transform, false);
        }

        var box = volume.GetComponent<BoxCollider>();
        if (box == null)
            box = volume.gameObject.AddComponent<BoxCollider>();

        box.isTrigger = false;
        box.center = transform.InverseTransformPoint(worldBounds.center);
        Vector3 localSize = transform.InverseTransformVector(worldBounds.size);
        box.size = new Vector3(
            Mathf.Abs(localSize.x) * 1.2f,
            Mathf.Abs(localSize.y) * 1.2f,
            Mathf.Abs(localSize.z) * 1.2f);

        _interactCollider = box;

        var legacyCapsule = GetComponent<CapsuleCollider>();
        if (legacyCapsule != null && legacyCapsule != _interactCollider)
            legacyCapsule.enabled = false;
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
