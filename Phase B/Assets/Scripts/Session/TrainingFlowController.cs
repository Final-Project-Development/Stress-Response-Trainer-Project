using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Game flow:
/// Gate (Hub) → optional Login / Register → Intro (narration) → Calibration (60s) → Simulation pick (Level Select UI) → chosen briefing → …
/// After each simulation’s results screen, flow returns to Simulation pick when <see cref="simulationPickPanel"/> is set (otherwise linear Sim 1 → Sim 2 remains).
/// Assign <see cref="simulationPickPanel"/> (Level_Select_UI) after calibration; buttons:
/// <see cref="UI_PickSimulation1AfterCalibration"/>, <see cref="UI_PickSimulation2AfterCalibration"/>,
/// <see cref="UI_PickEnvironmentLearningAfterCalibration"/> → <see cref="learnBriefingPanel"/> → <see cref="UI_BeginEnvironmentLearning"/>.
/// Physiology is simulated unless a UDP gateway is enabled later.
/// </summary>
[DefaultExecutionOrder(50)]
public class TrainingFlowController : MonoBehaviour
{
    [Serializable]
    public class ResultsTabsConfig
    {
        [Header("Tab content roots")]
        public GameObject resultTabContent;
        public GameObject recommendationsTabContent;
        public GameObject pressureGraphTabContent;

        [Header("Tab buttons (optional visual highlight)")]
        public Button resultTabButton;
        public Button recommendationsTabButton;
        public Button pressureGraphTabButton;
        public Color activeTabButtonColor = new Color(0.16f, 0.35f, 0.56f, 1f);
        public Color inactiveTabButtonColor = new Color(0.11f, 0.21f, 0.33f, 1f);
    }

    public enum ResultsTab
    {
        Result,
        Recommendations,
        PressureGraph
    }

    public enum Phase
    {
        Gate,
        Login,
        IntroNarration,
        Simulation1Calibration,
        SimulationPick,
        EnvironmentLearningBriefing,
        EnvironmentLearning,
        Simulation1MissionBriefing,
        Simulation1Active,
        Simulation1Results,
        Simulation2Briefing,
        Simulation2Active,
        Simulation2Results
    }

    public static TrainingFlowController Instance { get; private set; }

    /// <summary>Pickup, doors, shelter, phone booth, etc. only during active simulations.</summary>
    public bool AllowsMissionGameplay =>
        CurrentPhase == Phase.Simulation1Active || CurrentPhase == Phase.Simulation2Active;

    [Header("Refs")]
    public MockPhysiologySource physiology;
    public SessionStressRecorder recorder;
    public GameManager gameManager;
    [Tooltip("Per-task time limits and 3-strike disqualification. Auto-added to GameManager if empty.")]
    public MissionTaskStrikeTracker missionTaskStrikeTracker;
    public EnvironmentLearningController environmentLearningController;
    public UDPReceiver udpReceiver;

    [Header("UI roots (enable/disable per phase)")]
    public GameObject hubPanel;
    [Tooltip("Login / Register UI (opened from Hub). Assign a panel with LoginFlowPanel.")]
    public GameObject loginPanel;
    public GameObject introPanel;
    public GameObject sim1MissionBriefingPanel;
    public GameObject learnBriefingPanel;
    public GameObject sim1CalibrationPanel;
    [Tooltip("Level_Select_UI after calibration. Buttons: UI_PickSimulation1/2/EnvironmentLearning AfterCalibration.")]
    public GameObject simulationPickPanel;
    [Tooltip("EnvironmentLearningHud from the scene — design manually in Inspector (show/hide only at runtime).")]
    public GameObject environmentLearningHudPanel;
    public GameObject sim1ResultsPanel;
    public GameObject sim2BriefingPanel;
    public GameObject sim2ResultsPanel;
    public GameObject safetyWarningPanel;
    public TextMeshProUGUI safetyWarningText;
    public TextMeshProUGUI safetyWarningContinueWithAlarmText;
    public TextMeshProUGUI safetyWarningContinueWithoutAlarmText;
    [TextArea] public string stressWarningMessage = "Warning: this simulation contains stress stimuli (alarm audio, time pressure, emergency context). You can pause at any time with Esc and quit safely.";
    public string continueWithAlarmButtonLabel = "Continue with alarm";
    public string continueWithoutAlarmButtonLabel = "Continue without alarm";

    [Header("Optional: hide gameplay until mission starts")]
    public GameObject simulation1GameplayRoot;
    public GameObject simulation2GameplayRoot;

    [Header("Player — free cursor during menus so UI buttons work")]
    public SimpleFPSController playerFpsController;
    public Transform playerRoot;
    [Tooltip("Hub / menu start. If empty, enable Gate World Spawn below.")]
    public Transform gateSpawnPoint;
    [Tooltip("Use world position when Gate Spawn Point is not assigned.")]
    public bool gateSpawnUseWorldCoordinates;
    public Vector3 gateSpawnWorldPosition;
    public Vector3 gateSpawnWorldEuler;
    [Tooltip("Simulation 1 (mission) start. If empty, enable Sim 1 World Spawn below.")]
    public Transform simulation1SpawnPoint;
    [Tooltip("Use world position when Simulation 1 Spawn Point is not assigned.")]
    public bool simulation1SpawnUseWorldCoordinates;
    public Vector3 simulation1SpawnWorldPosition;
    public Vector3 simulation1SpawnWorldEuler;
    [Tooltip("Simulation 2 start (same-scene flow). If empty, enable Sim 2 World Spawn below.")]
    public Transform simulation2SpawnPoint;
    [Tooltip("Use world position when Simulation 2 Spawn Point is not assigned.")]
    public bool simulation2SpawnUseWorldCoordinates;
    public Vector3 simulation2SpawnWorldPosition;
    public Vector3 simulation2SpawnWorldEuler;
    [Tooltip("Leave empty to use Simulation 2 Spawn Point (recommended — same start as Sim 2).")]
    public Transform environmentLearningSpawnPoint;
    [Tooltip("If Environment Learning Spawn is empty, use Simulation 2 spawn.")]
    public bool environmentLearningFallbackToSim2Spawn = true;
    public bool environmentLearningSpawnUseWorldCoordinates;
    public Vector3 environmentLearningSpawnWorldPosition;
    public Vector3 environmentLearningSpawnWorldEuler;
    [Tooltip("Fallback only: use Gate Spawn when Environment Learning Spawn Point is not assigned.")]
    public bool environmentLearningUseGateSpawn;
    [Tooltip("How EnvironmentLearningSpawn Y is interpreted when placing the player.")]
    public PlayerGroundSnap.SpawnHeightMode environmentLearningSpawnHeightMode = PlayerGroundSnap.SpawnHeightMode.FeetAtMarker;

    [Header("Optional feedback")]
    public AudioSource sirenLoop;
    [Tooltip("Calm loop played when the user chooses Continue without alarm on the safety warning panel.")]
    public AudioClip calmBackgroundInsteadOfAlarmClip;
    [Tooltip("Very quiet background loop for baseline calibration (Baseline_Panel). Leave empty to reuse calmBackgroundInsteadOfAlarmClip.")]
    public AudioClip baselineCalibrationBackgroundClip;
    [Range(0f, 1f)] public float baselineCalibrationBackgroundVolume = 0.08f;
    public AudioSource narrationAudioSource;
    [Tooltip("Optional bundle of VoiceGPT clips (Window → VoiceGPT → Panel Narration Setup). Fills empty clip slots at Start.")]
    public PanelNarrationLibrary narrationLibrary;
    public AudioClip introNarrationClip;
    [Tooltip("Optional voice-over for the baseline calibration screen.")]
    public AudioClip calibrationNarrationClip;
    [Tooltip("Optional voice-over for Environment Learning briefing (LearnBriefing_Panel).")]
    public AudioClip learnBriefingNarrationClip;
    [Tooltip("Optional voice-over for Simulation 1 mission briefing (instructions before Start mission).")]
    public AudioClip missionBriefingNarrationClip;
    [Tooltip("Optional voice-over for Simulation 2 briefing panel.")]
    public AudioClip sim2BriefingNarrationClip;

    [Header("Narration pacing")]
    [Tooltip("Short pause after each spoken sentence so narration feels more natural.")]
    [Range(0.2f, 1.5f)]
    public float narrationPauseBetweenSentences = 0.55f;

    [Header("Per-sentence voice-over clips (VoiceGPT → Panel Narration Setup)")]
    public AudioClip[] introNarrationSentenceClips;
    public AudioClip[] calibrationNarrationSentenceClips;
    public AudioClip[] learnBriefingNarrationSentenceClips;
    public AudioClip[] missionBriefingNarrationSentenceClips;
    public AudioClip[] sim2BriefingNarrationSentenceClips;

    public UnityEvent onSimulation1Started;
    public UnityEvent onSimulation1Ended;

    [Header("Live stress / link")]
    [Tooltip("Optional in-simulation high-stress banner. Must NOT be the same object as Safety Warning Panel.")]
    public GameObject highStressWarningRoot;
    public GameObject gatewayDisconnectWarningRoot;
    public float gatewayStaleSeconds = 2.5f;

    [Header("Safety controls")]
    public GameObject pausePanel;
    public KeyCode pauseKey = KeyCode.Escape;
    public KeyCode quickQuitKey = KeyCode.F10;

    [Header("Copy (assign TMP or leave empty for defaults)")]
    public TextMeshProUGUI hubTitleText;
    public TextMeshProUGUI hubConnectionStatusText;
    public TextMeshProUGUI introBodyText;
    public TextMeshProUGUI missionBriefingBodyText;
    public TextMeshProUGUI learnBriefingBodyText;
    public TextMeshProUGUI calibrationStatusText;
    [Tooltip("Optional: large remaining-time readout (whole seconds or MM:SS). When set, time is not duplicated in Calibration Status Text.")]
    public TextMeshProUGUI calibrationRemainingTimeText;
    [Tooltip("Optional: live HR/HRV (e.g. HRV_DATA). When set, the physiology line is not duplicated in Calibration Status Text.")]
    public TextMeshProUGUI calibrationHrvDataText;
    [Tooltip("When using Calibration Remaining Time Text, use MM:SS instead of seconds only.")]
    public bool calibrationRemainingTimeAsMmSs = false;
    public TextMeshProUGUI resultsSummaryText;
    [Tooltip("Optional: when both Sim 1 metrics + recommendations are set, the summary is split into two columns.")]
    public TextMeshProUGUI sim1ResultsMetricsText;
    public TextMeshProUGUI sim1ResultsRecommendationsText;
    public TextMeshProUGUI sim2BriefingBodyText;
    public TextMeshProUGUI sim2ResultsSummaryText;
    [Tooltip("Optional: when both Sim 2 metrics + recommendations are set, the summary is split into two columns.")]
    public TextMeshProUGUI sim2ResultsMetricsText;
    public TextMeshProUGUI sim2ResultsRecommendationsText;

    [Header("Results panel lines (choose per simulation)")]
    [Tooltip("Which lines appear on the Result and Recommendations tabs for Simulation 1.")]
    public SimulationResultsPanelsConfig sim1ResultsPanels = SimulationResultsPanelsConfig.DefaultSim1();
    [Tooltip("Which lines appear on the Result and Recommendations tabs for Simulation 2.")]
    public SimulationResultsPanelsConfig sim2ResultsPanels = SimulationResultsPanelsConfig.DefaultSim2();

    [Header("Results panel manual layout")]
    [Tooltip("When on, Sim 1 Result/Recommendations TMP font size, alignment, and RectTransform are not changed at runtime.")]
    public bool preserveManualSim1ResultsLayout = true;
    [Tooltip("When on, Sim 2 Result/Recommendations TMP font size, alignment, and RectTransform are not changed at runtime.")]
    public bool preserveManualSim2ResultsLayout = true;

    public TextMeshProUGUI simulationActiveHudText;

    [Header("Per-task timer (Sim 1 & 2 active only)")]
    [Tooltip("timer_panel on Canvas — shows countdown for the current mission step only.")]
    public GameObject timerPanel;
    [Tooltip("TimeText TMP under timer_panel — remaining seconds for the active task.")]
    public TextMeshProUGUI simulationTimerText;
    [Tooltip("Optional title TMP (e.g. Timer label on timer_panel).")]
    public TextMeshProUGUI simulationTimerTitleText;
    [Tooltip("Title above the countdown (e.g. Timer). Task name is shown in the mission panel, not here.")]
    public string simulationTimerTitle = "Timer";
    [Tooltip("Timer text color when task remaining time is at or below urgent threshold.")]
    public bool simulationTimerUrgentColorEnabled = true;
    public float simulationTimerUrgentBelowSeconds = 15f;
    public Color simulationTimerNormalColor = Color.white;
    public Color simulationTimerUrgentColor = new Color(1f, 0.4f, 0.35f, 1f);

    [Header("Live watch HR chart (Samsung / Fit3 bridge)")]
    [Tooltip("WatchHrChart_Panel on Canvas — shown during Sim 1 & 2.")]
    public GameObject watchHrChartPanel;
    [Tooltip("WorkoutHeartRateChartReceiver on BioMetrics. Active during Sim 1 & 2 when watch sends UDP timeline on port 5055.")]
    public WorkoutHeartRateChartReceiver workoutHeartRateChart;

    [Header("Mission status HUD (Sim 1 & 2)")]
    [Tooltip("MissionStatus_Panel — completed step, next objective, hint button.")]
    public MissionStatusPanelController missionStatusPanel;

    [Header("Results graphs (SCI + HRV per simulation)")]
    [Tooltip("Simulation 1 — Stress Change Index over time (existing).")]
    public SimpleStressLineGraph resultsGraph;
    [Tooltip("Simulation 1 — Heart-rate variability (ms) over time. Optional second LineRenderer on results panel.")]
    public SimpleStressLineGraph sim1HrvResultsGraph;
    [Tooltip("Max HRV (ms) used to scale the Sim 1 HRV graph Y axis.")]
    public float sim1HrvGraphMaxDisplay = 120f;
    [Tooltip("Simulation 2 — HRV (ms) over time.")]
    public SimpleStressLineGraph sim2HrvResultsGraph;
    [Tooltip("Max HRV (ms) for Sim 2 graph scaling.")]
    public float sim2HrvGraphMaxDisplay = 120f;
    [Tooltip("Simulation 2 — SCI (%) over time. Optional; complements HRV graph.")]
    public SimpleStressLineGraph sim2SciResultsGraph;
    [Tooltip("Max SCI (%) for Sim 2 SCI graph Y axis.")]
    public float sim2SciGraphMaxDisplay = 80f;

    [Header("Results tabs (manual inspector wiring)")]
    [Tooltip("Simulation 1 tabs: assign tab content roots and tab buttons manually.")]
    public ResultsTabsConfig sim1ResultsTabs;
    [Tooltip("Simulation 2 tabs: assign tab content roots and tab buttons manually.")]
    public ResultsTabsConfig sim2ResultsTabs;

    [TextArea]
    public string hubTitle = "VR Stress Response Trainer";
    [Tooltip("If false, keep Hub title text exactly as set on the TMP in the Inspector/scene.")]
    public bool applyHubTitleFromController = false;

    [TextArea]
    public string hubConnectionStatusDemo =
        "Smartwatch: Not connected (simulated HR/HRV)\nAndroid gateway: Not in use";

    [TextArea]
    public string introNarrationText =
        "In recent years, many of us have experienced stress and pressure due to emergency situations and war...\n\n" +
        "This training experience is designed to help improve your ability to function under stress...\n\n" +
        "Please connect your smartwatch - in each simulation, your physiological response is measured, and at the end you receive practical recommendations for next time...";

    [Header("Intro subtitles (one paragraph at a time)")]
    [TextArea]
    public string introParagraph1 =
        "In recent years, many of us have experienced stress and pressure due to emergency situations and war.";
    [TextArea]
    public string introParagraph2 =
        "This training experience is designed to help improve your ability to function under stress.";
    [TextArea]
    public string introParagraph3 =
        "Please connect your smartwatch.";
    [TextArea]
    public string introParagraph4 =
        "In each simulation, your physiological response is measured and at the end you receive practical recommendations for next time.";
    public float introParagraph1Start = 0f;
    public float introParagraph2Start = 6f;
    public float introParagraph3Start = 12f;
    public float introParagraph4Start = 16f;

    [TextArea]
    public string calibrationInstruction =
        "Stand still and relax for 15 seconds...\n\n" +
        "We are calibrating your heart-rate metrics...\n\n" +
        "No alarm will play during this step - just breathe slowly and stay comfortable...";

    [TextArea]
    public string missionBriefingBody =
        "Simulation 1: Emergency Preparedness\n\n" +
        "Collect 5 essential supplies inside the house.\n\n" +
        "Turn off lights, close the door, and enter the Mamad shelter.";

    [TextArea]
    public string learnBriefingBody =
        "Environment Learning - City tour...\n\n" +
        "Explore important locations and objects in the training environment...\n\n" +
        "Use the left sidebar to jump to each item and read the labels in the world...\n\n" +
        "When you are ready, press Start learn...";

    [TextArea]
    public string sim2BriefingBody =
        "Simulation 2 - First aid under pressure...\n\n" +
        "1) Collect the first aid kit (press E)...\n" +
        "2) Find the wounded person and press E - then go to the public telephone and call for first aid help...\n" +
        "3) Public telephone - E open door (once), E insert coin, E pick up receiver, dial 1 then 0 then 1...\n" +
        "4) Return to the wounded person - press E on the casualty, then press 1, then 2, then 3 for treatment...\n\n" +
        "Press Start Mission when you are ready...";

    [Header("Simulation 1 — mission panel copy")]
    [TextArea] public string sim1AllItemsCollectedCompleted =
        "All supplies collected.";
    [TextArea] public string sim1LightsOffCompleted =
        "Lights turned off.";
    [TextArea] public string sim1DoorClosedCompleted =
        "Door closed.";
    [TextArea] public string sim1ShelterDoorOpenHint =
        "The entrance door is still open. Close PFB_DoorDouble before entering the Mamad.";
    [TextArea] public string sim1ObjectiveTurnOffLights =
        "Turn off the lights using the light switch inside the home.";
    [TextArea] public string sim1ObjectiveTurnOffLightsApproach =
        "Turn off the lights using the light switch inside the home.";
    [TextArea] public string sim1ObjectiveTurnOffLightsAction =
        "Press E on the light switch to turn off the lights.";
    [TextArea] public string sim1ObjectiveCloseDoor =
        "Close the entrance door before going to the Mamad shelter.";
    [TextArea] public string sim1ObjectiveCloseDoorApproach =
        "Close the entrance door before going to the Mamad shelter.";
    [TextArea] public string sim1ObjectiveCloseDoorAction =
        "Press E to close the entrance door.";
    [TextArea] public string sim1ObjectiveRunToShelter =
        "Run to the Mamad shelter outside.";
    [TextArea] public string sim1ObjectiveRunToShelterApproach =
        "Run to the Mamad shelter outside.";
    [TextArea] public string sim1ObjectiveRunToShelterAction =
        "Enter the Mamad shelter.";

    [Header("Simulation 2 — mission panel copy")]
    [TextArea] public string sim2ObjectiveFindKit =
        "Step 1: Find the first aid kit and press E to collect it.";
    [TextArea] public string sim2ObjectiveFindKitApproach =
        "Find the first aid kit in the city.";
    [TextArea] public string sim2ObjectiveFindKitAction =
        "Press E to collect the first aid kit.";
    [TextArea] public string sim2ObjectiveFindWounded =
        "Find the wounded person in the city and press E.";
    [TextArea] public string sim2ObjectiveFindWoundedApproach =
        "Find the wounded person in the city.";
    [TextArea] public string sim2ObjectiveFindWoundedAction =
        "Press E on the wounded person.";
    [TextArea] public string sim2CasualtyContactedCompleted =
        "Wounded person found.";
    [TextArea] public string sim2ObjectiveGoToPhone =
        "Go to the public telephone and open the door.";
    [TextArea] public string sim2ObjectiveGoToPhoneApproach =
        "Go to the public telephone.";
    [TextArea] public string sim2ObjectiveDialPhoneApproach =
        "Go to the telephone and dial 1, 0, 1 on the keypad.";
    [TextArea] public string sim2EmergencyReportedCompleted =
        "Emergency call placed.";
    [TextArea] public string sim2PhoneOpenDoorHint = "Press E on the booth door to open it (one time only).";
    [TextArea] public string sim2PhoneDoorOpenedCompleted = "Door opened.";
    [TextArea] public string sim2PhoneDoorOpenedObjective =
        "Press E on the coin slot, then E on the receiver.";
    [TextArea] public string sim2PhoneCoinInsertedCompleted = "Coin inserted.";
    [TextArea] public string sim2PhoneCoinInsertedObjective = "Press E on the receiver.";
    [TextArea] public string sim2PhoneDoorAlreadyOpenHint =
        "The door is already open. Press E on the coin slot.";
    [TextArea] public string sim2PhoneInsertCoinHint = "Press E on the coin slot to insert a coin.";
    [TextArea] public string sim2PhoneReceiverLiftedCompleted = "Receiver lifted.";
    [TextArea] public string sim2TreatmentStartedCompleted = "Treatment started.";
    [TextArea] public string sim2TreatmentCompleteCompleted = "Treatment complete: 1, 2, 3.";
    [TextArea] public string sim2NeedContactCasualtyBeforePhoneHint =
        "Find the wounded person first and press E on the casualty.";
    [TextArea] public string sim2AlreadyReportedHint =
        "Call complete. Return to the wounded person — press E, then 1, 2, 3.";
    [TextArea] public string sim2TreatWoundedHint =
        "Step 4: Return to the wounded. Press E to start treatment, then press 1, then 2, then 3.";
    [TextArea] public string sim2TreatWoundedApproach =
        "Return to the wounded person for treatment.";
    [TextArea] public string sim2TreatWoundedPressEAction =
        "Press E on the wounded person to start treatment.";
    [TextArea] public string sim2TreatWoundedPress1Action = "Press 1.";
    [TextArea] public string sim2TreatWoundedPress2Action = "Press 2.";
    [TextArea] public string sim2TreatWoundedPress3Action = "Press 3.";
    [TextArea] public string sim2CompletedHint =
        "First aid complete. Simulation 2 mission finished.";
    [TextArea] public string sim2NeedKitHint =
        "Find the first aid kit in the city before treating the wounded.";

    public string BuildSim1CollectObjective(IReadOnlyList<string> remainingDisplayNames, int collected, int total)
    {
        if (remainingDisplayNames == null || remainingDisplayNames.Count == 0)
            return sim1ObjectiveTurnOffLightsApproach;

        return BuildSim1CollectApproachObjective(remainingDisplayNames, collected, total);
    }

    public string BuildSim1CollectApproachObjective(IReadOnlyList<string> remainingDisplayNames, int collected, int total)
    {
        if (remainingDisplayNames == null || remainingDisplayNames.Count == 0)
            return sim1ObjectiveTurnOffLightsApproach;

        string remaining = string.Join(", ", remainingDisplayNames);
        if (collected <= 0)
            return $"Collect {total} supplies inside the home.\nRemaining: {remaining}.";

        return $"Collect supplies inside the home.\nRemaining: {remaining}. Progress: {collected}/{total}.";
    }

    public string BuildSim1CollectActionObjective(string itemDisplayName)
    {
        if (string.IsNullOrWhiteSpace(itemDisplayName))
            return "Press E to collect the item.";

        return $"Press E to collect {itemDisplayName.Trim()}.";
    }

    public float calibrationDurationSeconds = 60f;
    public bool runSimulation2InSameScene = true;
    public int simulation2SceneIndex = 1;

    public Phase CurrentPhase { get; private set; } = Phase.Gate;
    public bool IsPaused => _paused;
    public bool IsSafetyWarningVisible => safetyWarningPanel != null && safetyWarningPanel.activeSelf;

    /// <summary>Elapsed seconds in the current Sim 1/2 run (pauses with game pause).</summary>
    public float SimulationStressElapsedSeconds => _simulationStressTimer;

    private float _calibrationTimer;
    private float _simulationStressTimer;
    private Phase _simulationStressTimerPhase = Phase.Gate;
    private bool _simulationFinishHandled;
    private bool _sim2Subscribed;
    private bool _paused;
    private bool _useCalmBackgroundInsteadOfAlarm;
    private AudioClip _sirenDefaultClip;
    private float _sirenVolumeBeforeBaseline;
    private bool _baselineCalibrationBackgroundPlaying;
    private PendingStart _pendingStart = PendingStart.None;
    private ResultsTab _currentSim1ResultsTab = ResultsTab.Result;
    private ResultsTab _currentSim2ResultsTab = ResultsTab.Result;

    private enum PendingStart
    {
        None,
        Simulation1,
        Simulation2,
        Simulation1FromTour,
        Simulation2FromTour
    }

    private bool _tourAlarmActive;
    private PanelNarrationSequencePlayer _narrationPlayer;

    void Awake()
    {
        Instance = this;
        if (sirenLoop != null)
            _sirenDefaultClip = sirenLoop.clip;
        if (environmentLearningController == null)
            environmentLearningController = FindFirstObjectByType<EnvironmentLearningController>(FindObjectsInactive.Include);

        if (workoutHeartRateChart == null)
            workoutHeartRateChart = FindFirstObjectByType<WorkoutHeartRateChartReceiver>(FindObjectsInactive.Include);

        if (watchHrChartPanel == null && workoutHeartRateChart != null)
            watchHrChartPanel = workoutHeartRateChart.chartPanelRoot;

        if (missionStatusPanel == null)
            missionStatusPanel = FindFirstObjectByType<MissionStatusPanelController>(FindObjectsInactive.Include);

        if (gameManager != null && missionStatusPanel != null)
            gameManager.missionStatusPanel = missionStatusPanel;

        EnsureMissionTaskStrikeTracker();
        HideEnvironmentLearningTourSidebar();
    }

    void EnsureMissionTaskStrikeTracker()
    {
        if (gameManager == null)
            return;

        if (missionTaskStrikeTracker == null)
            missionTaskStrikeTracker = gameManager.GetComponent<MissionTaskStrikeTracker>();

        if (missionTaskStrikeTracker == null)
            missionTaskStrikeTracker = gameManager.gameObject.AddComponent<MissionTaskStrikeTracker>();
    }

    void EnsureNarrationPlayer()
    {
        _narrationPlayer = new PanelNarrationSequencePlayer(
            this,
            narrationAudioSource,
            narrationPauseBetweenSentences);
    }

    void OnDestroy()
    {
        StopBaselineCalibrationBackgroundIfPlaying();

        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        ApplyNarrationFromLibrary();
        EnsureNarrationPlayer();

        ApplyCurrentResultsTabs();

        ApplyDefaultCopyToUi();
        ApplyPhaseUI();
        SetSimulationGameplayState(false, false);
        MovePlayerToSpawn(gateSpawnPoint, gateSpawnUseWorldCoordinates, gateSpawnWorldPosition, gateSpawnWorldEuler);
        if (physiology != null)
            physiology.StressorActive = false;
        StopSiren();
        SetActiveSafe(highStressWarningRoot, false);
        SetActiveSafe(gatewayDisconnectWarningRoot, false);
        SetHudVisible(false);
        SetActiveSafe(timerPanel, false);
        SetActiveSafe(watchHrChartPanel, false);
        SetWorkoutHeartRateChartActive(false);
        if (missionStatusPanel != null)
            missionStatusPanel.SetPanelVisible(false);
        SetActiveSafe(pausePanel, false);
        SetSafetyWarningVisible(false);
        SetSimulation2Status(sim2BriefingBody);
        environmentLearningController?.EndLearning();
    }

    public void ApplyDefaultCopyToUi()
    {
        if (applyHubTitleFromController && hubTitleText != null)
            hubTitleText.text = hubTitle;
        RefreshHubConnectionStatusText();
        if (introBodyText != null)
            introBodyText.text = introNarrationText;
        if (missionBriefingBodyText != null && !UsesVisualSim1Briefing())
            missionBriefingBodyText.text = missionBriefingBody;
        if (learnBriefingBodyText != null)
            learnBriefingBodyText.text = learnBriefingBody;
        if (sim2BriefingBodyText != null)
            sim2BriefingBodyText.text = sim2BriefingBody;
        if (simulationTimerTitleText != null)
            simulationTimerTitleText.text = simulationTimerTitle;
    }

    void Update()
    {
        HandleSafetyKeys();

        if (CurrentPhase == Phase.IntroNarration)
            UpdateIntroSubtitleByNarrationTime();

        if (CurrentPhase == Phase.Simulation1Calibration && physiology != null)
        {
            _calibrationTimer += Time.deltaTime;
            float remaining = Mathf.Max(0f, calibrationDurationSeconds - _calibrationTimer);
            UpdateCalibrationBaselineUi(remaining);

            if (_calibrationTimer >= calibrationDurationSeconds)
                FinishCalibrationAndShowMissionBriefing();
        }

        UpdateGatewayDisconnectUi();
        UpdateActivePhaseHud();
        UpdateSimulationStressTimer();
    }

    /// <summary>From Hub — opens Login / Register panel.</summary>
    public void UI_OpenLogin()
    {
        CurrentPhase = Phase.Login;
        ApplyPhaseUI();
    }

    /// <summary>Called from <see cref="LoginFlowPanel"/> after successful login — returns to Hub only.</summary>
    public void UI_LoginSuccess()
    {
        CurrentPhase = Phase.Gate;
        RefreshHubConnectionStatusText();
        ApplyPhaseUI();
    }

    /// <summary>After successful login from the Login panel — refresh Hub status for later, then open Intro.</summary>
    public void UI_CompleteLoginAndStartIntro()
    {
        RefreshHubConnectionStatusText();
        UI_StartIntro();
    }

    private void RefreshHubConnectionStatusText()
    {
        if (hubConnectionStatusText == null) return;
        string email = LocalAuthStore.GetLastLoggedInEmail();
        if (!string.IsNullOrEmpty(email))
            hubConnectionStatusText.text = $"Signed in: {email}\n" + hubConnectionStatusDemo;
        else
            hubConnectionStatusText.text = hubConnectionStatusDemo;
    }

    /// <summary>Close login panel and return to Hub without signing in.</summary>
    public void UI_CancelLogin()
    {
        StopAllNarration();
        CurrentPhase = Phase.Gate;
        ApplyPhaseUI();
    }

    /// <summary>Gate start button — opens intro panel and optional narration.</summary>
    public void UI_StartSimulation1()
    {
        UI_MenuPickSimulation1Flow();
    }

    /// <summary>Hub — start intro (simulation choice happens after calibration when <see cref="simulationPickPanel"/> is set).</summary>
    public void UI_MenuPickSimulation1Flow() => UI_StartIntro();

    /// <summary>Legacy hub binding; same as <see cref="UI_MenuPickSimulation1Flow"/>.</summary>
    public void UI_MenuPickSimulation2Flow() => UI_StartIntro();

    /// <summary>Level-select block — Simulation 1 (after calibration).</summary>
    public void UI_PickSimulation1AfterCalibration()
    {
        if (CurrentPhase != Phase.SimulationPick) return;
        ShowSimulation1MissionBriefingAfterCalibration();
    }

    /// <summary>Level-select block — Simulation 2, skip Simulation 1 briefing (after calibration).</summary>
    public void UI_PickSimulation2AfterCalibration()
    {
        if (CurrentPhase != Phase.SimulationPick) return;
        ShowSimulation2BriefingAfterCalibration();
    }

    /// <summary>Level_Select_UI — show learn briefing, then city tour with labels on important objects.</summary>
    public void UI_PickEnvironmentLearningAfterCalibration()
    {
        if (CurrentPhase != Phase.SimulationPick) return;
        ShowEnvironmentLearningBriefingAfterCalibration();
    }

    /// <summary>Learn briefing panel — Start learn button.</summary>
    public void UI_BeginEnvironmentLearning()
    {
        if (CurrentPhase != Phase.EnvironmentLearningBriefing) return;
        BeginEnvironmentLearning();
    }

    /// <summary>Leave learn briefing and return to Level_Select_UI.</summary>
    public void UI_CancelLearnBriefing()
    {
        if (CurrentPhase != Phase.EnvironmentLearningBriefing) return;
        StopAllNarration();
        CurrentPhase = Phase.SimulationPick;
        SetSimulationGameplayState(false, false);
        ApplyPhaseUI();
    }

    /// <summary>Leave the tour and return to Level_Select_UI.</summary>
    public void UI_EndEnvironmentLearning()
    {
        if (CurrentPhase != Phase.EnvironmentLearning) return;
        StopTourAlarmIfPlaying();
        environmentLearningController?.EndLearning();
        gameManager?.RestoreExitDoorAfterEnvironmentLearning();
        CurrentPhase = Phase.SimulationPick;
        SetEnvironmentLearningTourPropsVisible(false);
        SetSimulationGameplayState(false, false);
        ApplyPhaseUI();
    }

    /// <summary>Tour sidebar — toggle siren while exploring the city.</summary>
    public void UI_ToggleEnvironmentLearningAlarm()
    {
        if (CurrentPhase != Phase.EnvironmentLearning)
            return;

        if (_tourAlarmActive)
            StopTourAlarmIfPlaying();
        else
        {
            _useCalmBackgroundInsteadOfAlarm = false;
            PlaySiren();
            _tourAlarmActive = true;
        }

        environmentLearningController?.tourGuide?.RefreshOptionsMenuPresentation();
    }

    /// <summary>Tour sidebar — start Simulation 1 from the player's current position.</summary>
    public void UI_StartSimulation1FromTour()
    {
        if (CurrentPhase != Phase.EnvironmentLearning)
            return;

        if (ShowSafetyWarningFor(PendingStart.Simulation1FromTour))
            return;

        BeginSimulation1FromTourNow();
    }

    /// <summary>Tour sidebar — start Simulation 2 from the player's current position.</summary>
    public void UI_StartSimulation2FromTour()
    {
        if (CurrentPhase != Phase.EnvironmentLearning)
            return;

        if (ShowSafetyWarningFor(PendingStart.Simulation2FromTour))
            return;

        BeginSimulation2FromTourNow();
    }

    public void UI_StartIntro()
    {
        StopAllNarration();
        if (introPanel == null)
        {
            UI_ContinueFromIntro();
            return;
        }
        CurrentPhase = Phase.IntroNarration;
        ShowIntroParagraph(0f);
        PlayIntroNarration();
        ApplyPhaseUI();
    }

    public void UI_ContinueFromIntro()
    {
        StopAllNarration();
        CurrentPhase = Phase.Simulation1Calibration;
        _calibrationTimer = 0f;
        PlayCalibrationNarration();
        physiology?.StartBaselineCapture();
        ApplyPhaseUI();
    }

    /// <summary>Legacy hub button name in scene — same as <see cref="UI_StartSimulation1"/>.</summary>
    public void UI_OpenSimulation1() => UI_StartSimulation1();

    /// <summary>Legacy — starts calibration directly.</summary>
    public void UI_StartBaseline() => UI_ContinueFromIntro();

    private void UpdateCalibrationBaselineUi(float remainingSeconds)
    {
        bool splitTimer = calibrationRemainingTimeText != null;
        bool splitHrv = calibrationHrvDataText != null;

        if (splitTimer)
            calibrationRemainingTimeText.text = FormatCalibrationRemainingDisplay(remainingSeconds);

        if (splitHrv && physiology != null)
        {
            calibrationHrvDataText.text =
                $"HR: {physiology.CurrentHeartRate:F0} bpm\n" +
                $"HRV: {physiology.CurrentHrvMs:F1} ms";
        }

        if (calibrationStatusText == null)
            return;

        if (_narrationPlayer != null && _narrationPlayer.IsPlaying)
            return;

        if (splitTimer && splitHrv)
        {
            calibrationStatusText.text = calibrationInstruction;
            return;
        }

        string timePart = $"Time remaining: {remainingSeconds:F0} s";

        if (splitTimer || splitHrv)
            calibrationStatusText.text = calibrationInstruction;
        else
            calibrationStatusText.text = $"{calibrationInstruction}\n\n{timePart}";
    }

    private string FormatCalibrationRemainingDisplay(float remainingSeconds)
    {
        if (calibrationRemainingTimeAsMmSs)
        {
            int sec = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
            int m = sec / 60;
            int s = sec % 60;
            return $"{m:00}:{s:00}";
        }

        return Mathf.CeilToInt(remainingSeconds).ToString();
    }

    private void FinishCalibrationAndShowMissionBriefing()
    {
        StopCalibrationNarration();
        physiology?.StopBaselineCaptureAndLock();
        if (physiology != null)
            SessionHistoryStore.BeginSession(physiology.HrvBaselineMs);

        if (simulationPickPanel != null)
        {
            CurrentPhase = Phase.SimulationPick;
            ApplyPhaseUI();
            return;
        }

        ShowSimulation1MissionBriefingAfterCalibration();
    }

    private void ShowSimulation1MissionBriefingAfterCalibration()
    {
        CurrentPhase = Phase.Simulation1MissionBriefing;
        if (missionBriefingBodyText != null && !UsesVisualSim1Briefing())
            missionBriefingBodyText.text = missionBriefingBody.TrimEnd();

        ApplyPhaseUI();
        RefreshVisualBriefingPanels();
        PlayMissionBriefingNarration();
    }

    private void ShowSimulation2BriefingAfterCalibration()
    {
        if (sim2BriefingPanel == null)
        {
            UI_StartSimulation2Scene();
            return;
        }

        StopAllNarration();
        CurrentPhase = Phase.Simulation2Briefing;
        SetSimulationGameplayState(false, false);
        ApplyPhaseUI();
        SetSimulation2Status(sim2BriefingBody);
        RefreshVisualBriefingPanels();
        PlaySim2BriefingNarration();
    }

    private void ShowEnvironmentLearningBriefingAfterCalibration()
    {
        if (learnBriefingPanel == null)
        {
            BeginEnvironmentLearning();
            return;
        }

        StopAllNarration();
        CurrentPhase = Phase.EnvironmentLearningBriefing;
        if (learnBriefingBodyText != null)
            learnBriefingBodyText.text = learnBriefingBody.TrimEnd();
        SetSimulationGameplayState(false, false);
        ApplyPhaseUI();
        PlayLearnBriefingNarration();
    }

    public void UI_BeginSimulation1()
    {
        if (ShowSafetyWarningFor(PendingStart.Simulation1))
            return;

        BeginSimulation1Now();
    }

    private void BeginSimulation1Now()
    {
        BeginSimulation1Core(teleportToSpawn: true);
    }

    private void BeginSimulation1FromTourNow()
    {
        BeginSimulation1Core(teleportToSpawn: false);
    }

    private void BeginSimulation1Core(bool teleportToSpawn)
    {
        StopAllNarration();
        if (!teleportToSpawn)
        {
            StopTourAlarmIfPlaying();
            environmentLearningController?.EndLearning();
            SetActiveSafe(environmentLearningHudPanel, false);
        }

        CurrentPhase = Phase.Simulation1Active;
        SetSimulationGameplayState(true, false);
        if (teleportToSpawn)
            MovePlayerToSpawn(simulation1SpawnPoint, simulation1SpawnUseWorldCoordinates, simulation1SpawnWorldPosition, simulation1SpawnWorldEuler);

        recorder?.BeginRecording();
        if (physiology != null)
            physiology.StressorActive = true;
        PlaySiren();
        onSimulation1Started?.Invoke();
        ApplyPhaseUI();
        SetHudVisible(true);

        gameManager?.PrepareSimulation1Mission();
        ResetSimulationRunState();
        missionTaskStrikeTracker?.BeginTracking(simulation2: false);

        if (gameManager != null)
            gameManager.OnAllItemsCollected += HandleSim1Complete;
    }

    public void FinishSim1Disqualified(string lastTaskDisplayName)
    {
        string reason = string.IsNullOrEmpty(lastTaskDisplayName)
            ? "Disqualified after 3 task time violations."
            : $"Disqualified after 3 task time violations (last: {lastTaskDisplayName}).";
        FinishSim1Run(missionCompleted: false, timedOut: false, disqualified: true, disqualificationReason: reason);
    }

    public void FinishSim2Disqualified(string lastTaskDisplayName)
    {
        string reason = string.IsNullOrEmpty(lastTaskDisplayName)
            ? "Disqualified after 3 task time violations."
            : $"Disqualified after 3 task time violations (last: {lastTaskDisplayName}).";
        FinishSim2Run(missionCompleted: false, timedOut: false, disqualified: true, disqualificationReason: reason);
    }

    private void HandleSim1Complete() => FinishSim1Run(missionCompleted: true, timedOut: false);

    private void FinishSim1Run(
        bool missionCompleted,
        bool timedOut,
        bool disqualified = false,
        string disqualificationReason = null)
    {
        if (CurrentPhase != Phase.Simulation1Active || _simulationFinishHandled)
            return;

        _simulationFinishHandled = true;

        if (gameManager != null)
            gameManager.OnAllItemsCollected -= HandleSim1Complete;

        if (physiology != null)
            physiology.StressorActive = false;
        StopSiren();
        recorder?.EndRecording();
        onSimulation1Ended?.Invoke();
        SetActiveSafe(highStressWarningRoot, false);
        SetHudVisible(false);
        SetSimulationGameplayState(false, false);
        gameManager?.ClearMissionMessages();
        missionTaskStrikeTracker?.EndTracking();

        var outcome = BuildSim1RunOutcome(missionCompleted, timedOut, disqualified, disqualificationReason);
        CurrentPhase = Phase.Simulation1Results;
        SafeApplySimulation1ResultGraphs();

        if (physiology != null && recorder != null)
        {
            float peakSci = recorder.SciHistory.Count > 0 ? MaxSci(recorder.SciHistory) : 0f;
            float meanSci = recorder.SciHistory.Count > 0 ? MeanSci(recorder.SciHistory) : 0f;
            var peakBand = StressChangeIndexCalculator.Classify(peakSci);
            SessionHistoryStore.UpdateAfterSim1(
                recorder.SciHistory,
                physiology.HrvBaselineMs,
                outcome,
                recorder.sampleIntervalSeconds);
            string sim1Recommendations = StressRecommendations.BuildRecommendationsTabOnly(
                recorder.SciHistory,
                StressRecommendations.SimulationStage.Sim1,
                outcome,
                sim1ResultsPanels);

            if (UseSim1SplitColumns())
            {
                if (resultsSummaryText != null)
                    resultsSummaryText.gameObject.SetActive(false);

                string metrics = StressRecommendations.BuildResultsTabMetrics(
                    StressRecommendations.SimulationStage.Sim1,
                    peakSci,
                    meanSci,
                    outcome,
                    baselineHrvMs: physiology.HrvBaselineMs,
                    sciHistory: recorder.SciHistory,
                    sampleIntervalSeconds: recorder.sampleIntervalSeconds,
                    display: sim1ResultsPanels);

                LayoutSim1ResultsPanels();
                sim1ResultsMetricsText.text = metrics;
                sim1ResultsRecommendationsText.text = sim1Recommendations;
            }
            else if (resultsSummaryText != null)
            {
                resultsSummaryText.gameObject.SetActive(true);
                var sb = new StringBuilder();
                sb.AppendLine("Simulation 1 — Results");
                sb.AppendLine();
                if (outcome != null && outcome.disqualified)
                    sb.AppendLine("Disqualified — too many task time violations.");
                else if (outcome != null && outcome.timedOut && outcome.timeLimitSeconds > 0f)
                    sb.AppendLine("Mission not finished in time.");
                sb.AppendLine($"Baseline HRV: {physiology.HrvBaselineMs:F1} ms");
                sb.AppendLine($"Peak SCI: {peakSci:F1}% ({StressChangeIndexCalculator.BandLabel(peakBand)})");
                sb.AppendLine($"Average SCI: {meanSci:F1}%");
                sb.AppendLine();
                sb.AppendLine("Recommendations:");
                sb.AppendLine(sim1Recommendations);
                PrepareResultsPanelText(resultsSummaryText, preserveManualSim1ResultsLayout);
                resultsSummaryText.text = sb.ToString();
            }
        }

        ApplyPhaseUI();
        ApplySim1ResultsTab(ResultsTab.Result);
    }

    private SimulationRunOutcome BuildSim1RunOutcome(
        bool missionCompleted,
        bool timedOut,
        bool disqualified = false,
        string disqualificationReason = null)
    {
        float elapsed = _simulationStressTimer;
        float progress = missionCompleted ? 1f : gameManager?.GetSim1MissionProgress01() ?? 0f;
        float highStressSeconds = recorder != null
            ? StressRecommendations.ComputeHighStressSeconds(recorder.SciHistory, recorder.sampleIntervalSeconds)
            : 0f;
        int strikes = missionTaskStrikeTracker != null ? missionTaskStrikeTracker.StrikeCount : 0;
        if (disqualified && string.IsNullOrEmpty(disqualificationReason) && missionTaskStrikeTracker != null)
            disqualificationReason = missionTaskStrikeTracker.GetDisqualificationSummary();
        return SimulationRunOutcome.Create(
            elapsed,
            timeLimitSeconds: 0f,
            missionCompleted,
            timedOut: false,
            progress,
            highStressSeconds,
            disqualified,
            strikes,
            disqualificationReason);
    }

    private SimulationRunOutcome BuildSim2RunOutcome(
        bool missionCompleted,
        bool timedOut,
        bool disqualified = false,
        string disqualificationReason = null)
    {
        float elapsed = _simulationStressTimer;
        float progress = missionCompleted ? 1f : gameManager?.GetSim2MissionProgress01() ?? 0f;
        float highStressSeconds = recorder != null
            ? StressRecommendations.ComputeHighStressSeconds(recorder.SciHistory, recorder.sampleIntervalSeconds)
            : 0f;
        int strikes = missionTaskStrikeTracker != null ? missionTaskStrikeTracker.StrikeCount : 0;
        if (disqualified && string.IsNullOrEmpty(disqualificationReason) && missionTaskStrikeTracker != null)
            disqualificationReason = missionTaskStrikeTracker.GetDisqualificationSummary();
        return SimulationRunOutcome.Create(
            elapsed,
            timeLimitSeconds: 0f,
            missionCompleted,
            timedOut: false,
            progress,
            highStressSeconds,
            disqualified,
            strikes,
            disqualificationReason);
    }

    private void ResetSimulationRunState()
    {
        _simulationFinishHandled = false;
        _simulationStressTimer = 0f;
        _simulationStressTimerPhase = CurrentPhase;
        missionTaskStrikeTracker?.EndTracking();
    }

    private static float MaxSci(System.Collections.Generic.IReadOnlyList<float> list)
    {
        float m = list[0];
        for (int i = 1; i < list.Count; i++)
            if (list[i] > m) m = list[i];
        return m;
    }

    private static float MeanSci(System.Collections.Generic.IReadOnlyList<float> list)
    {
        float s = 0f;
        for (int i = 0; i < list.Count; i++)
            s += list[i];
        return list.Count > 0 ? s / list.Count : 0f;
    }

    /// <summary>
    /// Wired from Simulation 1 results — opens level-select when assigned; otherwise legacy jump to Simulation 2 briefing.
    /// From Simulation 2 results use <see cref="UI_ReturnToSimulationPickFromResults"/> (same behavior).
    /// </summary>
    public void UI_GoToSimulation2() => UI_ReturnToSimulationPickFromResults();

    /// <summary>
    /// Returns to <see cref="simulationPickPanel"/> after viewing results when it is assigned.
    /// Legacy fallback: Simulation 1 results → Simulation 2 briefing; Simulation 2 results → hub.
    /// </summary>
    public void UI_ReturnToSimulationPickFromResults()
    {
        StopAllNarration();
        StopActiveSimulationAudio();
        if (simulationPickPanel != null)
        {
            CurrentPhase = Phase.SimulationPick;
            SetSimulationGameplayState(false, false);
            ApplyPhaseUI();
            return;
        }

        if (CurrentPhase == Phase.Simulation2Results)
        {
            UI_BackToHub();
            return;
        }

        GoToSimulation2BriefingDirect();
    }

    private void GoToSimulation2BriefingDirect()
    {
        if (sim2BriefingPanel == null)
        {
            UI_StartSimulation2Scene();
            return;
        }

        StopAllNarration();
        CurrentPhase = Phase.Simulation2Briefing;
        SetSimulationGameplayState(false, false);
        ApplyPhaseUI();
        SetSimulation2Status(sim2BriefingBody);
        RefreshVisualBriefingPanels();
        PlaySim2BriefingNarration();
    }

    public void UI_StartSimulation2Scene()
    {
        StopAllNarration();
        if (ShowSafetyWarningFor(PendingStart.Simulation2))
            return;

        StartSimulation2Now();
    }

    private void StartSimulation2Now()
    {
        if (runSimulation2InSameScene)
        {
            StartSimulation2InSameScene();
            return;
        }

        if (simulation2SceneIndex >= 0 && simulation2SceneIndex < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(simulation2SceneIndex);
    }

    public void UI_BackToHub()
    {
        StopAllNarration();
        StopActiveSimulationAudio();
        environmentLearningController?.EndLearning();
        CurrentPhase = Phase.Gate;
        recorder?.Clear();
        physiology?.StartBaselineCapture();
        physiology?.StopBaselineCaptureAndLock();
        UnsubscribeSimulation2IfNeeded();
        SetSimulationGameplayState(false, false);
        MovePlayerToSpawn(gateSpawnPoint, gateSpawnUseWorldCoordinates, gateSpawnWorldPosition, gateSpawnWorldEuler);
        SetActiveSafe(highStressWarningRoot, false);
        SetActiveSafe(gatewayDisconnectWarningRoot, false);
        SetSafetyWarningVisible(false);
        SetHudVisible(false);
        ApplyPhaseUI();
    }

    public void UI_ConfirmSafetyWarning()
    {
        _useCalmBackgroundInsteadOfAlarm = false;
        ConfirmSafetyWarningAndStart();
    }

    public void UI_ConfirmSafetyWarningWithoutAlarm()
    {
        _useCalmBackgroundInsteadOfAlarm = true;
        ConfirmSafetyWarningAndStart();
    }

    private void ConfirmSafetyWarningAndStart()
    {
        SetSafetyWarningVisible(false);
        var action = _pendingStart;
        _pendingStart = PendingStart.None;

        if (action == PendingStart.Simulation1)
            BeginSimulation1Now();
        else if (action == PendingStart.Simulation2)
            StartSimulation2Now();
        else if (action == PendingStart.Simulation1FromTour)
            BeginSimulation1FromTourNow();
        else if (action == PendingStart.Simulation2FromTour)
            BeginSimulation2FromTourNow();
    }

    public void UI_CancelSafetyWarning()
    {
        _pendingStart = PendingStart.None;
        _useCalmBackgroundInsteadOfAlarm = false;
        SetSafetyWarningVisible(false);
    }

    public void UI_TogglePause() => SetPaused(!_paused);
    public void UI_Resume() => SetPaused(false);
    public void UI_SetPause(bool paused) => SetPaused(paused);

    /// <summary>Stops narration, siren/background loops, and brief gameplay voice lines.</summary>
    public void UI_StopAllAudio()
    {
        StopAllNarration();
        StopSiren();
        StopBaselineCalibrationBackgroundIfPlaying();

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

        if (gameManager != null)
        {
            if (gameManager.voiceAudioSource != null)
                gameManager.voiceAudioSource.Stop();
            if (gameManager.objectiveSuccessAudioSource != null)
                gameManager.objectiveSuccessAudioSource.Stop();
        }
    }

    public void UI_QuitApplication()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void LateUpdate()
    {
        bool activeTrainingPhase = CurrentPhase == Phase.Simulation1Active || CurrentPhase == Phase.Simulation2Active;
        if (!activeTrainingPhase || physiology == null || recorder == null) return;
        if (!physiology.BaselineLocked) return;

        float sci = StressChangeIndexCalculator.ComputeSciPercent(physiology.HrvBaselineMs, physiology.CurrentHrvMs);
        recorder.TickRecord(sci, physiology.CurrentHrvMs);

        var band = StressChangeIndexCalculator.Classify(sci);
        UpdateHighStressWarningUi(band);
    }

    private void UpdateHighStressWarningUi(StressChangeIndexCalculator.StressBand band)
    {
        if (highStressWarningRoot == null)
            return;

        // Inspector miswire: same panel as pre-simulation safety consent must not pop mid-mission.
        if (highStressWarningRoot == safetyWarningPanel)
            return;

        SetActiveSafe(highStressWarningRoot, band == StressChangeIndexCalculator.StressBand.High);
    }

    private void UpdateActivePhaseHud()
    {
        bool activeTrainingPhase = CurrentPhase == Phase.Simulation1Active || CurrentPhase == Phase.Simulation2Active;
        if (!activeTrainingPhase || physiology == null || !physiology.BaselineLocked)
            return;

        if (simulationActiveHudText == null) return;

        float sci = StressChangeIndexCalculator.ComputeSciPercent(physiology.HrvBaselineMs, physiology.CurrentHrvMs);
        var band = StressChangeIndexCalculator.Classify(sci);
        simulationActiveHudText.text =
            $"Stress level: {StressChangeIndexCalculator.BandLabel(band).ToUpperInvariant()}\n" +
            $"SCI: {sci:F1}%\n" +
            $"HR: {physiology.CurrentHeartRate:F0} bpm | HRV: {physiology.CurrentHrvMs:F1} ms";
    }

    private void SetHudVisible(bool on)
    {
        if (simulationActiveHudText != null)
            simulationActiveHudText.gameObject.SetActive(on);
    }

    private void UpdateSimulationStressTimer()
    {
        bool active = CurrentPhase == Phase.Simulation1Active || CurrentPhase == Phase.Simulation2Active;
        if (!active)
        {
            _simulationStressTimerPhase = CurrentPhase;
            return;
        }

        if (CurrentPhase != _simulationStressTimerPhase)
        {
            _simulationStressTimer = 0f;
            _simulationStressTimerPhase = CurrentPhase;
        }

        if (!_paused)
            _simulationStressTimer += Time.deltaTime;

        RefreshPerTaskTimerDisplay();
    }

    private void RefreshPerTaskTimerDisplay()
    {
        if (simulationTimerText == null)
            return;

        EnsureMissionTaskStrikeTracker();
        var tracker = missionTaskStrikeTracker;
        bool showTaskTimer = tracker != null &&
                             tracker.trackingEnabled &&
                             tracker.IsTrackingActive &&
                             !string.IsNullOrEmpty(tracker.CurrentTaskKey);

        if (showTaskTimer)
        {
            float remaining = tracker.CurrentTaskRemainingSeconds;
            simulationTimerText.text = FormatMmSs(remaining);

            if (simulationTimerTitleText != null)
                simulationTimerTitleText.text = simulationTimerTitle;

            if (!simulationTimerUrgentColorEnabled)
            {
                simulationTimerText.color = simulationTimerNormalColor;
                return;
            }

            float urgentThreshold = Mathf.Min(
                simulationTimerUrgentBelowSeconds,
                tracker.CurrentTaskLimitSeconds * 0.25f);
            simulationTimerText.color = remaining <= urgentThreshold
                ? simulationTimerUrgentColor
                : simulationTimerNormalColor;
            return;
        }

        simulationTimerText.text = "--:--";
        if (simulationTimerTitleText != null)
            simulationTimerTitleText.text = simulationTimerTitle;

        simulationTimerText.color = simulationTimerNormalColor;
    }

    private static string FormatMmSs(float seconds)
    {
        int sec = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int m = sec / 60;
        int s = sec % 60;
        return $"{m:00}:{s:00}";
    }

    private void UpdateGatewayDisconnectUi()
    {
        if (gatewayDisconnectWarningRoot == null || udpReceiver == null) return;
        if (!udpReceiver.expectGatewayTraffic)
        {
            SetActiveSafe(gatewayDisconnectWarningRoot, false);
            return;
        }

        bool relevant = CurrentPhase == Phase.Simulation1Calibration || CurrentPhase == Phase.Simulation1Active;
        bool stale = udpReceiver.ReceivedAnyPacket && udpReceiver.SecondsSinceLastPacket > gatewayStaleSeconds;
        SetActiveSafe(gatewayDisconnectWarningRoot, relevant && stale);
    }

    private void ApplyPhaseUI()
    {
        SetActiveSafe(hubPanel, CurrentPhase == Phase.Gate);
        SetActiveSafe(loginPanel, CurrentPhase == Phase.Login);
        SetActiveSafe(introPanel, CurrentPhase == Phase.IntroNarration);
        SetActiveSafe(sim1CalibrationPanel, CurrentPhase == Phase.Simulation1Calibration);
        bool showLevelSelect = CurrentPhase == Phase.SimulationPick;
        SetActiveSafe(simulationPickPanel, showLevelSelect);
        if (showLevelSelect)
            ScrollLevelSelectToTop();
        SetActiveSafe(learnBriefingPanel, CurrentPhase == Phase.EnvironmentLearningBriefing);
        SetActiveSafe(environmentLearningHudPanel, CurrentPhase == Phase.EnvironmentLearning);
        if (CurrentPhase != Phase.EnvironmentLearning)
            HideEnvironmentLearningTourSidebar();
        SetActiveSafe(sim1MissionBriefingPanel, CurrentPhase == Phase.Simulation1MissionBriefing);
        SetActiveSafe(sim1ResultsPanel, CurrentPhase == Phase.Simulation1Results);
        SetActiveSafe(sim2BriefingPanel, CurrentPhase == Phase.Simulation2Briefing);
        SetActiveSafe(sim2ResultsPanel, CurrentPhase == Phase.Simulation2Results);
        bool showStressTimer = CurrentPhase == Phase.Simulation1Active || CurrentPhase == Phase.Simulation2Active;
        bool showHrChartReceiver = CurrentPhase == Phase.Simulation1Calibration || showStressTimer;
        SetActiveSafe(timerPanel, showStressTimer);
        SetActiveSafe(watchHrChartPanel, showStressTimer);
        if (missionStatusPanel != null)
            missionStatusPanel.SetPanelVisible(showStressTimer);
        if (showStressTimer)
            RefreshPerTaskTimerDisplay();
        SetWorkoutHeartRateChartActive(showHrChartReceiver);
        UpdateBaselineCalibrationBackgroundAudio();
        ApplyPlayerInteractionMode();
    }

    private void UpdateBaselineCalibrationBackgroundAudio()
    {
        bool inBaselineCalibration = CurrentPhase == Phase.Simulation1Calibration;
        if (inBaselineCalibration)
            PlayBaselineCalibrationBackground();
        else
            StopBaselineCalibrationBackgroundIfPlaying();
    }

    private void PlayBaselineCalibrationBackground()
    {
        if (sirenLoop == null || _baselineCalibrationBackgroundPlaying)
            return;

        AudioClip calibrationClip = baselineCalibrationBackgroundClip != null
            ? baselineCalibrationBackgroundClip
            : calmBackgroundInsteadOfAlarmClip;
        if (calibrationClip == null)
            return;

        _sirenVolumeBeforeBaseline = sirenLoop.volume;
        sirenLoop.clip = calibrationClip;
        sirenLoop.loop = true;
        sirenLoop.volume = Mathf.Clamp01(baselineCalibrationBackgroundVolume);
        if (!sirenLoop.isPlaying)
            sirenLoop.Play();

        _baselineCalibrationBackgroundPlaying = true;
    }

    private void StopBaselineCalibrationBackgroundIfPlaying()
    {
        if (sirenLoop == null || !_baselineCalibrationBackgroundPlaying)
            return;

        sirenLoop.Stop();
        sirenLoop.volume = _sirenVolumeBeforeBaseline;
        if (_sirenDefaultClip != null)
            sirenLoop.clip = _sirenDefaultClip;
        _baselineCalibrationBackgroundPlaying = false;
    }

    void OnDisable()
    {
        StopBaselineCalibrationBackgroundIfPlaying();

        if (playerFpsController != null)
            playerFpsController.SetUiMenuMode(true);
    }

    /// <summary>Unlock cursor for menu phases; lock only during active simulations.</summary>
    private void ApplyPlayerInteractionMode()
    {
        if (playerFpsController == null)
            return;

        var navigation = FindFirstObjectByType<UINavigationManager>(FindObjectsInactive.Include);
        if (navigation != null)
        {
            navigation.ApplyPlayerCursorMode();
            return;
        }

        bool menuPhase = CurrentPhase != Phase.Simulation1Active && CurrentPhase != Phase.Simulation2Active;
        bool activeSimulation = !menuPhase;
        playerFpsController.SetOverlayUiOpen(false);
        playerFpsController.SetUiMenuMode(menuPhase);
        playerFpsController.SetSimulationToolbarMode(activeSimulation);

        if (missionStatusPanel != null)
        {
            var region = missionStatusPanel.GetPanelScreenRegion();
            if (region != null)
                playerFpsController.SetMissionStatusPanelRegion(region);
        }
    }

    private static void SetActiveSafe(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on)
            go.SetActive(on);
    }

    private void SetWorkoutHeartRateChartActive(bool on)
    {
        if (workoutHeartRateChart == null)
            return;

        if (workoutHeartRateChart.enabled != on)
            workoutHeartRateChart.enabled = on;

        if (!on)
            return;

        workoutHeartRateChart.SetChartUiMode(
            CurrentPhase == Phase.Simulation1Calibration
                ? WorkoutHeartRateChartReceiver.ChartUiMode.Baseline
                : WorkoutHeartRateChartReceiver.ChartUiMode.Simulation);
    }

    void HideEnvironmentLearningTourSidebar()
    {
        if (environmentLearningController != null)
        {
            var guide = environmentLearningController.tourGuide;
            if (guide == null)
                guide = environmentLearningController.GetComponent<EnvironmentLearningTourGuide>();

            if (guide != null)
            {
                guide.EnsureSidebarHidden();
                return;
            }
        }

        EnvironmentLearningTourGuide.HideSidebarInScene();
    }

    private bool UseSim1SplitColumns() =>
        sim1ResultsMetricsText != null && sim1ResultsRecommendationsText != null;

    private bool UseSim2SplitColumns() =>
        sim2ResultsMetricsText != null && sim2ResultsRecommendationsText != null;

    private void SetSimulationGameplayState(bool simulation1On, bool simulation2On)
    {
        if (simulation1GameplayRoot != null)
            simulation1GameplayRoot.SetActive(simulation1On);

        if (simulation2GameplayRoot != null)
            simulation2GameplayRoot.SetActive(simulation2On);
    }

    /// <summary>
    /// Shows first-aid kit, wounded NPC, and other tour props while keeping mission gameplay blocked.
    /// </summary>
    private void SetEnvironmentLearningTourPropsVisible(bool visible)
    {
        if (simulation1GameplayRoot != null)
            simulation1GameplayRoot.SetActive(visible);

        if (simulation2GameplayRoot != null)
            simulation2GameplayRoot.SetActive(visible);
    }

    private void PlaySiren()
    {
        if (sirenLoop == null) return;

        if (_useCalmBackgroundInsteadOfAlarm && calmBackgroundInsteadOfAlarmClip != null)
            sirenLoop.clip = calmBackgroundInsteadOfAlarmClip;
        else if (_sirenDefaultClip != null)
            sirenLoop.clip = _sirenDefaultClip;

        sirenLoop.loop = true;
        if (!sirenLoop.isPlaying)
            sirenLoop.Play();
    }

    private void StopSiren()
    {
        if (sirenLoop == null) return;
        sirenLoop.Stop();
        if (_sirenDefaultClip != null)
            sirenLoop.clip = _sirenDefaultClip;
        _useCalmBackgroundInsteadOfAlarm = false;
    }

    private void StopActiveSimulationAudio()
    {
        if (CurrentPhase == Phase.Simulation1Active && gameManager != null)
            gameManager.OnAllItemsCollected -= HandleSim1Complete;

        if (CurrentPhase == Phase.Simulation1Active || CurrentPhase == Phase.Simulation2Active)
        {
            if (physiology != null)
                physiology.StressorActive = false;
        }

        StopSiren();
    }

    private void ApplyNarrationFromLibrary()
    {
        if (narrationLibrary == null)
            return;

        if (introNarrationClip == null && narrationLibrary.introClip != null)
            introNarrationClip = narrationLibrary.introClip;
        if (calibrationNarrationClip == null && narrationLibrary.calibrationClip != null)
            calibrationNarrationClip = narrationLibrary.calibrationClip;
        if (learnBriefingNarrationClip == null && narrationLibrary.learnBriefingClip != null)
            learnBriefingNarrationClip = narrationLibrary.learnBriefingClip;
        if (missionBriefingNarrationClip == null && narrationLibrary.sim1MissionBriefingClip != null)
            missionBriefingNarrationClip = narrationLibrary.sim1MissionBriefingClip;
        if (sim2BriefingNarrationClip == null && narrationLibrary.sim2BriefingClip != null)
            sim2BriefingNarrationClip = narrationLibrary.sim2BriefingClip;

        if ((introNarrationSentenceClips == null || introNarrationSentenceClips.Length == 0) &&
            narrationLibrary.introSentenceClips != null && narrationLibrary.introSentenceClips.Length > 0)
            introNarrationSentenceClips = narrationLibrary.introSentenceClips;
        if ((calibrationNarrationSentenceClips == null || calibrationNarrationSentenceClips.Length == 0) &&
            narrationLibrary.calibrationSentenceClips != null && narrationLibrary.calibrationSentenceClips.Length > 0)
            calibrationNarrationSentenceClips = narrationLibrary.calibrationSentenceClips;
        if ((learnBriefingNarrationSentenceClips == null || learnBriefingNarrationSentenceClips.Length == 0) &&
            narrationLibrary.learnBriefingSentenceClips != null && narrationLibrary.learnBriefingSentenceClips.Length > 0)
            learnBriefingNarrationSentenceClips = narrationLibrary.learnBriefingSentenceClips;
        if ((missionBriefingNarrationSentenceClips == null || missionBriefingNarrationSentenceClips.Length == 0) &&
            narrationLibrary.sim1MissionBriefingSentenceClips != null && narrationLibrary.sim1MissionBriefingSentenceClips.Length > 0)
            missionBriefingNarrationSentenceClips = narrationLibrary.sim1MissionBriefingSentenceClips;
        if ((sim2BriefingNarrationSentenceClips == null || sim2BriefingNarrationSentenceClips.Length == 0) &&
            narrationLibrary.sim2BriefingSentenceClips != null && narrationLibrary.sim2BriefingSentenceClips.Length > 0)
            sim2BriefingNarrationSentenceClips = narrationLibrary.sim2BriefingSentenceClips;
    }

    private void PlayIntroNarration()
    {
        _narrationPlayer?.Play(
            introNarrationClip,
            introNarrationSentenceClips,
            introNarrationText,
            introBodyText,
            GetIntroSubtitleLines());
    }

    private void StopIntroNarration() => StopPanelNarration(stopAnyClip: true);

    private void PlayCalibrationNarration()
    {
        _narrationPlayer?.Play(
            calibrationNarrationClip,
            calibrationNarrationSentenceClips,
            calibrationInstruction,
            calibrationStatusText,
            null);
    }

    private void StopCalibrationNarration() => StopPanelNarration(calibrationNarrationClip);

    private void PlayLearnBriefingNarration()
    {
        // Keep the full learn-briefing body visible; narration plays per sentence without replacing the text.
        _narrationPlayer?.Play(
            learnBriefingNarrationClip,
            learnBriefingNarrationSentenceClips,
            learnBriefingBody,
            null,
            null);
    }

    private void StopLearnBriefingNarration() => StopPanelNarration(learnBriefingNarrationClip);

    private void RefreshVisualBriefingPanels()
    {
        if (sim1MissionBriefingPanel != null)
        {
            var sim1Briefing = sim1MissionBriefingPanel.GetComponent<SimulationBriefingPanelController>();
            if (sim1Briefing != null)
                sim1Briefing.Refresh();
        }

        if (sim2BriefingPanel != null)
        {
            var sim2Briefing = sim2BriefingPanel.GetComponent<SimulationBriefingPanelController>();
            if (sim2Briefing != null)
                sim2Briefing.Refresh();
        }
    }

    bool UsesVisualSim1Briefing() =>
        sim1MissionBriefingPanel != null &&
        sim1MissionBriefingPanel.GetComponent<SimulationBriefingPanelController>() != null;

    private void PlayMissionBriefingNarration()
    {
        _narrationPlayer?.Play(
            missionBriefingNarrationClip,
            missionBriefingNarrationSentenceClips,
            missionBriefingBody,
            missionBriefingBodyText,
            null);
    }

    private void StopMissionBriefingNarration() => StopPanelNarration(missionBriefingNarrationClip);

    private void PlaySim2BriefingNarration()
    {
        _narrationPlayer?.Play(
            sim2BriefingNarrationClip,
            sim2BriefingNarrationSentenceClips,
            sim2BriefingBody,
            sim2BriefingBodyText,
            null);
    }

    private void StopSim2BriefingNarration() => StopPanelNarration(sim2BriefingNarrationClip);

    private void StopPanelNarration(AudioClip clip = null, bool stopAnyClip = false)
    {
        if (_narrationPlayer != null && (_narrationPlayer.IsPlaying || stopAnyClip))
        {
            _narrationPlayer.Stop();
            return;
        }

        StopNarrationIfPlaying(clip, stopAnyClip);
    }

    private void PlayNarrationClip(AudioClip clip)
    {
        if (narrationAudioSource == null || clip == null)
            return;

        narrationAudioSource.loop = false;
        narrationAudioSource.clip = clip;
        narrationAudioSource.Play();
    }

    private void StopNarrationIfPlaying(AudioClip clip, bool stopAnyClip = false)
    {
        if (narrationAudioSource == null)
            return;

        if (stopAnyClip || narrationAudioSource.clip == clip)
            narrationAudioSource.Stop();
    }

    private void StopAllNarration()
    {
        _narrationPlayer?.Stop();
        if (narrationAudioSource != null)
            narrationAudioSource.Stop();
    }

    private void UpdateIntroSubtitleByNarrationTime()
    {
        if (UsesIntroSentenceNarration())
            return;

        if (introBodyText == null)
            return;

        float t = 0f;
        if (narrationAudioSource != null && narrationAudioSource.isPlaying)
            t = narrationAudioSource.time;

        ShowIntroParagraph(t);
    }

    private bool UsesIntroSentenceNarration() =>
        introNarrationSentenceClips != null && introNarrationSentenceClips.Length > 1;

    private string[] GetIntroSubtitleLines()
    {
        var raw = new[]
        {
            introParagraph1,
            introParagraph2,
            introParagraph3,
            introParagraph4
        };

        int count = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(raw[i]))
                count++;
        }

        if (count == 0)
            return raw;

        var lines = new string[count];
        int index = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(raw[i]))
                continue;
            lines[index++] = raw[i];
        }

        return lines;
    }

    private void ShowIntroParagraph(float timeSeconds)
    {
        if (introBodyText == null)
            return;

        if (timeSeconds >= introParagraph4Start)
            introBodyText.text = introParagraph4;
        else if (timeSeconds >= introParagraph3Start)
            introBodyText.text = introParagraph3;
        else if (timeSeconds >= introParagraph2Start)
            introBodyText.text = introParagraph2;
        else
            introBodyText.text = introParagraph1;
    }

    private void BeginEnvironmentLearning()
    {
        StopAllNarration();
        CurrentPhase = Phase.EnvironmentLearning;
        SetEnvironmentLearningTourPropsVisible(true);
        gameManager?.ApplyEnvironmentLearningDoorLayout();

        // Teleport before ApplyPhaseUI (movement/gravity off) — same timing as Simulation 2 briefing → start.
        PlacePlayerAtEnvironmentLearningSpawn();

        if (physiology != null)
            physiology.StressorActive = false;
        StopSiren();
        _tourAlarmActive = false;
        SetHudVisible(false);
        environmentLearningController?.BeginLearning();
        ApplyPhaseUI();
    }

    /// <summary>
    /// Uses FlowManager Environment Learning Spawn (or Sim 2 fallback), same teleport as Simulation 2.
    /// </summary>
    private void PlacePlayerAtEnvironmentLearningSpawn()
    {
        ResolveEnvironmentLearningSpawn();
        ResolvePlayerRoot();

        if (environmentLearningSpawnUseWorldCoordinates)
            MovePlayerToSpawn(null, true, environmentLearningSpawnWorldPosition, environmentLearningSpawnWorldEuler);
        else if (environmentLearningSpawnPoint != null)
            MovePlayerToSpawn(environmentLearningSpawnPoint, false, default, default);
        else if (environmentLearningUseGateSpawn)
            MovePlayerToSpawn(
                gateSpawnPoint,
                gateSpawnUseWorldCoordinates,
                gateSpawnWorldPosition,
                gateSpawnWorldEuler);
        else
        {
            Debug.LogWarning(
                "Environment Learning: assign Environment Learning Spawn Point on FlowManager (e.g. Simulation2SpawnPoint).");
            return;
        }

        if (playerRoot == null)
            return;

        Physics.SyncTransforms();

        var cc = playerRoot.GetComponent<CharacterController>();
        if (cc != null && !cc.isGrounded)
            PlayerGroundSnap.TrySnapToGround(playerRoot);

        playerRoot.GetComponent<SimpleFPSController>()?.ResetVerticalVelocity();
    }

    private void ResolvePlayerRoot()
    {
        if (playerRoot != null)
            return;

        if (playerFpsController != null)
            playerRoot = playerFpsController.transform;
        else
        {
            var fps = FindFirstObjectByType<SimpleFPSController>(FindObjectsInactive.Include);
            if (fps != null)
                playerRoot = fps.transform;
        }
    }

    private void ResolveEnvironmentLearningSpawn()
    {
        if (environmentLearningSpawnPoint != null)
            return;

        if (environmentLearningFallbackToSim2Spawn && simulation2SpawnPoint != null)
        {
            environmentLearningSpawnPoint = simulation2SpawnPoint;
            return;
        }

        var found = GameObject.Find("EnvironmentLearningSpawn");
        if (found != null)
            environmentLearningSpawnPoint = found.transform;
    }

    private void ScrollLevelSelectToTop()
    {
        if (simulationPickPanel == null)
            return;

        var levelUi = simulationPickPanel.GetComponent<LevelSelectUI>();
        if (levelUi == null)
            levelUi = simulationPickPanel.GetComponentInChildren<LevelSelectUI>(true);

        if (levelUi != null)
        {
            levelUi.ApplyCopy();
            levelUi.ScrollToTop();
        }
    }

    /// <summary>
    /// Prefer a scene <see cref="Transform"/>; if it is missing and <paramref name="useWorldCoordinates"/> is set, use world position + euler angles.
    /// </summary>
    private void MovePlayerToSpawn(Transform spawnTransform, bool useWorldCoordinates, Vector3 worldPosition, Vector3 worldEuler)
    {
        if (spawnTransform == null && !useWorldCoordinates)
            return;

        if (playerRoot == null)
        {
            var fps = FindFirstObjectByType<SimpleFPSController>();
            if (fps != null)
                playerRoot = fps.transform;
        }

        if (playerRoot == null) return;

        Vector3 pos;
        Quaternion rot;
        if (spawnTransform != null)
        {
            pos = spawnTransform.position;
            rot = spawnTransform.rotation;
        }
        else
        {
            pos = worldPosition;
            rot = Quaternion.Euler(worldEuler);
        }

        var fpsController = playerRoot.GetComponent<SimpleFPSController>();
        if (fpsController != null)
            fpsController.TeleportTo(pos, rot);
        else
            playerRoot.SetPositionAndRotation(pos, rot);
    }

    private void StartSimulation2InSameScene()
    {
        StartSimulation2InSameSceneCore(teleportToSpawn: true);
    }

    private void BeginSimulation2FromTourNow()
    {
        if (!runSimulation2InSameScene)
        {
            Debug.LogWarning(
                "Start Simulation 2 from tour keeps your current position only when runSimulation2InSameScene is enabled.");
            StartSimulation2Now();
            return;
        }

        StartSimulation2InSameSceneCore(teleportToSpawn: false);
    }

    private void StartSimulation2InSameSceneCore(bool teleportToSpawn)
    {
        if (!teleportToSpawn)
        {
            StopTourAlarmIfPlaying();
            environmentLearningController?.EndLearning();
            SetActiveSafe(environmentLearningHudPanel, false);
        }

        CurrentPhase = Phase.Simulation2Active;
        SetSimulationGameplayState(false, true);
        if (teleportToSpawn)
        {
            if (simulation2SpawnPoint != null)
                MovePlayerToSpawn(simulation2SpawnPoint, false, default, default);
            else if (simulation2SpawnUseWorldCoordinates)
                MovePlayerToSpawn(null, true, simulation2SpawnWorldPosition, simulation2SpawnWorldEuler);
            else if (simulation1SpawnPoint != null)
                MovePlayerToSpawn(simulation1SpawnPoint, false, default, default);
            else if (simulation1SpawnUseWorldCoordinates)
                MovePlayerToSpawn(null, true, simulation1SpawnWorldPosition, simulation1SpawnWorldEuler);
        }

        recorder?.Clear();
        recorder?.BeginRecording();
        if (physiology != null)
            physiology.StressorActive = true;
        PlaySiren();
        SetHudVisible(true);
        SetSimulation2Status(sim2ObjectiveFindKit);
        gameManager?.PrepareSimulation2Mission();
        ResetSimulationRunState();
        missionTaskStrikeTracker?.BeginTracking(simulation2: true);
        SubscribeSimulation2IfNeeded();
        ApplyPhaseUI();
    }

    public bool IsEnvironmentLearningTourAlarmActive => _tourAlarmActive;

    private void StopTourAlarmIfPlaying()
    {
        if (!_tourAlarmActive)
            return;

        StopSiren();
        _tourAlarmActive = false;
    }

    private void SetSafetyWarningVisible(bool visible)
    {
        SetActiveSafe(safetyWarningPanel, visible);
        if (playerFpsController != null)
            playerFpsController.SetOverlayUiOpen(visible);

        var navigation = FindFirstObjectByType<UINavigationManager>(FindObjectsInactive.Include);
        navigation?.ApplyPlayerCursorMode();
    }

    private bool ShowSafetyWarningFor(PendingStart startAction)
    {
        if (safetyWarningPanel == null)
            return false;

        _pendingStart = startAction;
        if (safetyWarningText != null)
            safetyWarningText.text = stressWarningMessage;
        ApplySafetyWarningButtonLabels();
        SetSafetyWarningVisible(true);
        return true;
    }

    private void ApplySafetyWarningButtonLabels()
    {
        if (safetyWarningContinueWithAlarmText != null)
            safetyWarningContinueWithAlarmText.text = continueWithAlarmButtonLabel;
        if (safetyWarningContinueWithoutAlarmText != null)
            safetyWarningContinueWithoutAlarmText.text = continueWithoutAlarmButtonLabel;
    }

    private void HandleSafetyKeys()
    {
        if (CurrentPhase == Phase.EnvironmentLearning && WasPauseKeyPressed())
        {
            UI_EndEnvironmentLearning();
            return;
        }

        if (WasPauseKeyPressed())
        {
            var navigation = FindFirstObjectByType<UINavigationManager>(FindObjectsInactive.Include);
            if (navigation != null && navigation.ConsumeEscapeForOverlay())
            {
                navigation.ApplyPlayerCursorMode();
                return;
            }

            SetPaused(!_paused);
        }

        if (Input.GetKeyDown(quickQuitKey))
            UI_QuitApplication();
    }

    private bool WasPauseKeyPressed()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            return true;

        return pauseKey != KeyCode.None && pauseKey != KeyCode.Escape && Input.GetKeyDown(pauseKey);
    }

    private void SetPaused(bool paused)
    {
        _paused = paused;
        Time.timeScale = paused ? 0f : 1f;
        SetActiveSafe(pausePanel, paused);
        if (paused)
            SetSafetyWarningVisible(false);

        var navigation = FindFirstObjectByType<UINavigationManager>(FindObjectsInactive.Include);
        navigation?.ApplyPlayerCursorMode();
    }

    private void SubscribeSimulation2IfNeeded()
    {
        if (_sim2Subscribed || gameManager == null)
            return;

        gameManager.OnFirstAidComplete += HandleSimulation2Complete;
        _sim2Subscribed = true;
    }

    private void UnsubscribeSimulation2IfNeeded()
    {
        if (!_sim2Subscribed || gameManager == null)
            return;

        gameManager.OnFirstAidComplete -= HandleSimulation2Complete;
        _sim2Subscribed = false;
    }

    private void HandleSimulation2Complete() => FinishSim2Run(missionCompleted: true, timedOut: false);

    private void FinishSim2Run(
        bool missionCompleted,
        bool timedOut,
        bool disqualified = false,
        string disqualificationReason = null)
    {
        if (CurrentPhase != Phase.Simulation2Active || _simulationFinishHandled)
            return;

        _simulationFinishHandled = true;
        UnsubscribeSimulation2IfNeeded();
        if (physiology != null)
            physiology.StressorActive = false;
        StopSiren();
        recorder?.EndRecording();
        gameManager?.ClearMissionMessages();
        missionTaskStrikeTracker?.EndTracking();
        SetSimulationGameplayState(false, false);
        SetHudVisible(false);
        SetActiveSafe(highStressWarningRoot, false);

        var outcome = BuildSim2RunOutcome(missionCompleted, timedOut, disqualified, disqualificationReason);
        CurrentPhase = Phase.Simulation2Results;

        float peakSci = 0f;
        float meanSci = 0f;
        if (recorder != null && recorder.SciHistory.Count > 0)
        {
            peakSci = MaxSci(recorder.SciHistory);
            meanSci = MeanSci(recorder.SciHistory);
            SessionHistoryStore.FinalizeAfterSim2(recorder.SciHistory, outcome, recorder.sampleIntervalSeconds);
        }
        else if (timedOut || disqualified)
        {
            SessionHistoryStore.FinalizeAfterSim2(null, outcome, recorder?.sampleIntervalSeconds ?? 0.4f);
        }

        string sim2Recommendations = recorder != null && recorder.SciHistory.Count > 0
            ? StressRecommendations.BuildRecommendationsTabOnly(
                recorder.SciHistory,
                StressRecommendations.SimulationStage.Sim2,
                outcome,
                sim2ResultsPanels)
            : StressRecommendations.BuildRecommendationsTabOnly(
                null,
                StressRecommendations.SimulationStage.Sim2,
                outcome,
                sim2ResultsPanels);
        float minHrv = 0f;
        float maxHrv = 0f;
        float avgHrv = 0f;
        if (recorder != null && recorder.HrvHistory.Count > 0)
        {
            minHrv = MinValue(recorder.HrvHistory);
            maxHrv = MaxValue(recorder.HrvHistory);
            avgHrv = MeanValue(recorder.HrvHistory);
        }

        if (UseSim2SplitColumns())
        {
            if (sim2ResultsSummaryText != null)
                sim2ResultsSummaryText.gameObject.SetActive(false);

            string metrics = StressRecommendations.BuildResultsTabMetrics(
                StressRecommendations.SimulationStage.Sim2,
                peakSci,
                meanSci,
                outcome,
                minHrvMs: minHrv,
                maxHrvMs: maxHrv,
                avgHrvMs: avgHrv,
                sciHistory: recorder?.SciHistory,
                sampleIntervalSeconds: recorder?.sampleIntervalSeconds ?? 0.4f,
                display: sim2ResultsPanels);

            LayoutSim2ResultsPanels();
            sim2ResultsMetricsText.text = metrics;
            sim2ResultsRecommendationsText.text = sim2Recommendations;
        }
        else if (sim2ResultsSummaryText != null)
        {
            sim2ResultsSummaryText.gameObject.SetActive(true);
            var sb = new StringBuilder();
            sb.AppendLine("Simulation 2 — Results");
            sb.AppendLine();
            if (outcome != null && outcome.disqualified)
                sb.AppendLine("Disqualified — too many task time violations.");
            else if (outcome != null && outcome.timedOut && outcome.timeLimitSeconds > 0f)
                sb.AppendLine("Mission not finished in time.");
            sb.AppendLine($"Peak SCI: {peakSci:F1}%");
            sb.AppendLine($"Average SCI: {meanSci:F1}%");
            sb.AppendLine();
            sb.AppendLine("Recommendations:");
            sb.AppendLine(sim2Recommendations);
            PrepareResultsPanelText(sim2ResultsSummaryText, preserveManualSim2ResultsLayout);
            sim2ResultsSummaryText.text = sb.ToString();
        }
        else if (sim2BriefingBodyText != null)
        {
            // Avoid showing fallback status text over the custom Results panel design.
            sim2BriefingBodyText.text = string.Empty;
        }

        SafeApplySimulation2ResultGraphs();

        ApplyPhaseUI();
        ApplySim2ResultsTab(ResultsTab.Result);
    }

    private void SafeApplySimulation1ResultGraphs()
    {
        try
        {
            ApplySimulation1ResultGraphs();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Simulation 1 results graphs failed: {ex.Message}\n{ex.StackTrace}");
            resultsGraph?.Clear();
            sim1HrvResultsGraph?.Clear();
        }
    }

    private void SafeApplySimulation2ResultGraphs()
    {
        try
        {
            ApplySimulation2ResultGraphs();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Simulation 2 results graphs failed: {ex.Message}\n{ex.StackTrace}");
            sim2SciResultsGraph?.Clear();
            sim2HrvResultsGraph?.Clear();
        }
    }

    // Wire these to the three tab buttons in the Inspector (Simulation 1 panel).
    public void UI_ShowSim1ResultTab() => ApplySim1ResultsTab(ResultsTab.Result);
    public void UI_ShowSim1RecommendationsTab() => ApplySim1ResultsTab(ResultsTab.Recommendations);
    public void UI_ShowSim1PressureGraphTab()
    {
        ApplySim1ResultsTab(ResultsTab.PressureGraph);
        SafeApplySimulation1ResultGraphs();
    }

    // Wire these to the three tab buttons in the Inspector (Simulation 2 panel).
    public void UI_ShowSim2ResultTab() => ApplySim2ResultsTab(ResultsTab.Result);
    public void UI_ShowSim2RecommendationsTab() => ApplySim2ResultsTab(ResultsTab.Recommendations);
    public void UI_ShowSim2PressureGraphTab()
    {
        ApplySim2ResultsTab(ResultsTab.PressureGraph);
        SafeApplySimulation2ResultGraphs();
    }

    private void ApplySimulation1ResultGraphs()
    {
        if (recorder == null)
        {
            resultsGraph?.Clear();
            sim1HrvResultsGraph?.Clear();
            return;
        }

        float interval = recorder.sampleIntervalSeconds;

        if (resultsGraph != null)
        {
            if (recorder.SciHistory.Count > 0)
            {
                resultsGraph.chartTitle = "SCI";
                resultsGraph.useFixedYRange = true;
                resultsGraph.fixedYMin = 0f;
                resultsGraph.fixedYMax = resultsGraph.maxSciDisplay;
                resultsGraph.SetFromSciPointsWithMarkers(recorder.SciHistory, recorder.MissionMarkers, interval);
                resultsGraph.SetInfoText(string.Empty);
                resultsGraph.RefreshRenderToFitLayout();
            }
            else
            {
                resultsGraph.Clear();
            }
        }

        if (sim1HrvResultsGraph != null)
        {
            if (ReferenceEquals(sim1HrvResultsGraph, resultsGraph))
            {
                // Guard against accidental inspector wiring to the same graph object.
                return;
            }

            if (recorder.HrvHistory.Count > 0)
            {
                sim1HrvResultsGraph.chartTitle = "HRV";
                sim1HrvResultsGraph.SetFromValues(recorder.HrvHistory, sim1HrvGraphMaxDisplay, interval);
                sim1HrvResultsGraph.SetInfoText(string.Empty);
            }
            else
            {
                sim1HrvResultsGraph.Clear();
            }
        }
    }

    private void ApplySimulation2ResultGraphs()
    {
        if (recorder == null)
        {
            sim2SciResultsGraph?.Clear();
            sim2HrvResultsGraph?.Clear();
            return;
        }

        float interval = recorder.sampleIntervalSeconds;
        bool sharedSciAndHrvGraph = sim2SciResultsGraph != null
            && sim2HrvResultsGraph != null
            && ReferenceEquals(sim2SciResultsGraph, sim2HrvResultsGraph);

        if (sim2SciResultsGraph != null)
        {
            if (recorder.SciHistory.Count > 0)
            {
                sim2SciResultsGraph.chartTitle = "SCI";
                sim2SciResultsGraph.useFixedYRange = true;
                sim2SciResultsGraph.fixedYMin = 0f;
                sim2SciResultsGraph.fixedYMax = sim2SciGraphMaxDisplay;
                sim2SciResultsGraph.SetFromSciPointsWithMarkers(
                    recorder.SciHistory,
                    sim2SciGraphMaxDisplay,
                    recorder.MissionMarkers,
                    interval);
                sim2SciResultsGraph.SetInfoText(string.Empty);
                sim2SciResultsGraph.RefreshRenderToFitLayout();
            }
            else if (sharedSciAndHrvGraph && recorder.HrvHistory.Count > 0)
            {
                sim2SciResultsGraph.chartTitle = "HRV";
                sim2SciResultsGraph.useFixedYRange = false;
                sim2SciResultsGraph.SetFromValues(recorder.HrvHistory, sim2HrvGraphMaxDisplay, interval);
                sim2SciResultsGraph.SetInfoText(string.Empty);
                sim2SciResultsGraph.RefreshRenderToFitLayout();
            }
            else
            {
                sim2SciResultsGraph.Clear();
            }
        }

        if (sim2HrvResultsGraph != null)
        {
            if (sharedSciAndHrvGraph)
            {
                // Single results chart object — SCI (with mission markers) already applied above.
                return;
            }

            if (recorder.HrvHistory.Count > 0)
            {
                sim2HrvResultsGraph.chartTitle = "HRV";
                sim2HrvResultsGraph.SetFromValues(recorder.HrvHistory, sim2HrvGraphMaxDisplay, interval);
                sim2HrvResultsGraph.SetInfoText(string.Empty);
            }
            else
            {
                sim2HrvResultsGraph.Clear();
            }
        }

        if (recorder.SciHistory.Count == 0 && recorder.HrvHistory.Count == 0)
            Debug.LogWarning("Simulation 2 results: no SCI/HRV samples recorded. Check recorder and active simulation.");
    }

    private static float MinValue(System.Collections.Generic.IReadOnlyList<float> list)
    {
        if (list == null || list.Count == 0) return 0f;
        float m = list[0];
        for (int i = 1; i < list.Count; i++)
            if (list[i] < m) m = list[i];
        return m;
    }

    private static float MaxValue(System.Collections.Generic.IReadOnlyList<float> list)
    {
        if (list == null || list.Count == 0) return 0f;
        float m = list[0];
        for (int i = 1; i < list.Count; i++)
            if (list[i] > m) m = list[i];
        return m;
    }

    private static float MeanValue(System.Collections.Generic.IReadOnlyList<float> list)
    {
        if (list == null || list.Count == 0) return 0f;
        float s = 0f;
        for (int i = 0; i < list.Count; i++) s += list[i];
        return s / list.Count;
    }

    private void SetSimulation2Status(string text)
    {
        if (sim2BriefingBodyText != null)
            sim2BriefingBodyText.text = text;
    }

    private void AppendSimulationPickFooter(StringBuilder sb)
    {
        if (sb == null || simulationPickPanel == null)
            return;
        sb.AppendLine();
        sb.AppendLine(
            "When you are ready, use <b>Choose Simulation</b> to open the scenario menu (Simulation 1 — supplies, Simulation 2 — first aid).");
    }

    private string Sim2ResultsFooterLine()
    {
        return simulationPickPanel != null
            ? "When ready, use <b>Choose Simulation</b> to pick another scenario."
            : "Press <b>Back To Hub</b> when ready.";
    }

    private string Sim2ResultsFooterLinePlain()
    {
        return simulationPickPanel != null
            ? "Use Choose Simulation on this screen to pick another scenario."
            : "Press Back To Hub when ready.";
    }

    private void ApplyCurrentResultsTabs()
    {
        ApplySim1ResultsTab(_currentSim1ResultsTab);
        ApplySim2ResultsTab(_currentSim2ResultsTab);
    }

    private void ApplySim1ResultsTab(ResultsTab tab)
    {
        _currentSim1ResultsTab = tab;
        ApplyResultsTabState(sim1ResultsTabs, tab);
        EnforceSim1ResultsVisibilityForTab(tab);
    }

    private void ApplySim2ResultsTab(ResultsTab tab)
    {
        _currentSim2ResultsTab = tab;
        ApplyResultsTabState(sim2ResultsTabs, tab);
        EnforceSim2ResultsVisibilityForTab(tab);
    }

    private static void ApplyResultsTabState(ResultsTabsConfig config, ResultsTab activeTab)
    {
        if (config == null)
            return;

        SetActiveSafe(config.resultTabContent, activeTab == ResultsTab.Result);
        SetActiveSafe(config.recommendationsTabContent, activeTab == ResultsTab.Recommendations);
        SetActiveSafe(config.pressureGraphTabContent, activeTab == ResultsTab.PressureGraph);

        ApplyTabButtonVisual(config.resultTabButton, activeTab == ResultsTab.Result, config);
        ApplyTabButtonVisual(config.recommendationsTabButton, activeTab == ResultsTab.Recommendations, config);
        ApplyTabButtonVisual(config.pressureGraphTabButton, activeTab == ResultsTab.PressureGraph, config);
    }

    private static void ApplyTabButtonVisual(Button button, bool active, ResultsTabsConfig config)
    {
        if (button == null)
            return;

        var image = button.GetComponent<Image>();
        if (image != null)
            image.color = active ? config.activeTabButtonColor : config.inactiveTabButtonColor;
    }

    private void EnforceSim1ResultsVisibilityForTab(ResultsTab activeTab)
    {
        bool splitColumns = UseSim1SplitColumns();

        if (splitColumns && resultsSummaryText != null)
            SetActiveSafe(resultsSummaryText.gameObject, false);

        if (!splitColumns)
            return;

        bool showResult = activeTab == ResultsTab.Result;
        bool showRecommendations = activeTab == ResultsTab.Recommendations;
        bool showGraph = activeTab == ResultsTab.PressureGraph;

        if (sim1ResultsMetricsText != null)
            SetActiveSafe(sim1ResultsMetricsText.gameObject, showResult);
        if (sim1ResultsRecommendationsText != null)
            SetActiveSafe(sim1ResultsRecommendationsText.gameObject, showRecommendations);

        GameObject graphRoot = sim1ResultsTabs != null ? sim1ResultsTabs.pressureGraphTabContent : null;
        if (graphRoot == null && resultsGraph != null)
            graphRoot = resultsGraph.gameObject;
        if (graphRoot != null)
            SetActiveSafe(graphRoot, showGraph);
    }

    /// <summary>
    /// Keeps legacy summary text hidden when Sim 2 uses manual split tabs,
    /// without overriding the scene's manual visual design.
    /// </summary>
    private void EnforceSim2ResultsVisibilityForTab(ResultsTab activeTab)
    {
        bool splitColumns = UseSim2SplitColumns();

        if (splitColumns && sim2ResultsSummaryText != null)
            SetActiveSafe(sim2ResultsSummaryText.gameObject, false);

        if (!splitColumns)
            return;

        bool showResult = activeTab == ResultsTab.Result;
        bool showRecommendations = activeTab == ResultsTab.Recommendations;
        bool showGraph = activeTab == ResultsTab.PressureGraph;

        if (sim2ResultsMetricsText != null)
            SetActiveSafe(sim2ResultsMetricsText.gameObject, showResult);
        if (sim2ResultsRecommendationsText != null)
            SetActiveSafe(sim2ResultsRecommendationsText.gameObject, showRecommendations);

        GameObject graphRoot = sim2ResultsTabs != null ? sim2ResultsTabs.pressureGraphTabContent : null;
        if (graphRoot == null && sim2SciResultsGraph != null)
            graphRoot = sim2SciResultsGraph.gameObject;
        if (graphRoot != null)
            SetActiveSafe(graphRoot, showGraph);
    }

    const float ResultsPanelFontSize = 18f;
    const float ResultsPanelPadding = 24f;

    void LayoutSim1ResultsPanels()
    {
        if (!preserveManualSim1ResultsLayout)
        {
            PrepareResultsPanelText(sim1ResultsMetricsText, false);
            PrepareResultsPanelText(sim1ResultsRecommendationsText, false);

            GameObject graphRoot = sim1ResultsTabs != null ? sim1ResultsTabs.pressureGraphTabContent : null;
            if (graphRoot == null && resultsGraph != null)
                graphRoot = resultsGraph.gameObject;
            PrepareResultsPanelRect(graphRoot != null ? graphRoot.transform as RectTransform : null);
        }
    }

    void LayoutSim2ResultsPanels()
    {
        if (!preserveManualSim2ResultsLayout)
        {
            PrepareResultsPanelText(sim2ResultsMetricsText, false);
            PrepareResultsPanelText(sim2ResultsRecommendationsText, false);

            GameObject graphRoot = sim2ResultsTabs != null ? sim2ResultsTabs.pressureGraphTabContent : null;
            if (graphRoot == null && sim2SciResultsGraph != null)
                graphRoot = sim2SciResultsGraph.gameObject;
            PrepareResultsPanelRect(graphRoot != null ? graphRoot.transform as RectTransform : null);
        }
    }

    void PrepareResultsPanelText(TextMeshProUGUI tmp, bool preserveManualLayout)
    {
        if (tmp == null || preserveManualLayout)
            return;

        tmp.enableWordWrapping = true;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Truncate;
        tmp.enableAutoSizing = false;
        tmp.fontSize = ResultsPanelFontSize;
        tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;
        tmp.verticalAlignment = VerticalAlignmentOptions.Top;
        tmp.margin = new Vector4(8f, 8f, 8f, 8f);
        PrepareResultsPanelRect(tmp.rectTransform);
    }

    static void PrepareResultsPanelRect(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = new Vector2(ResultsPanelPadding, ResultsPanelPadding);
        rect.offsetMax = new Vector2(-ResultsPanelPadding, -ResultsPanelPadding);
    }
}
