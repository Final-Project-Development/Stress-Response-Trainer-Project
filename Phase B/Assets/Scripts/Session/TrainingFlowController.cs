using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Game flow:
/// Gate → Intro (narration) → Calibration (60s) → Simulation 1 briefing → Simulation 1 active
/// → Results 1 → Simulation 2 briefing → Simulation 2 scene.
/// Physiology is simulated unless a UDP gateway is enabled later.
/// </summary>
[DefaultExecutionOrder(50)]
public class TrainingFlowController : MonoBehaviour
{
    public enum Phase
    {
        Gate,
        IntroNarration,
        Simulation1Calibration,
        Simulation1MissionBriefing,
        Simulation1Active,
        Simulation1Results,
        Simulation2Briefing,
        Simulation2Active,
        Simulation2Results
    }

    [Header("Refs")]
    public MockPhysiologySource physiology;
    public SessionStressRecorder recorder;
    public SimpleStressLineGraph resultsGraph;
    public GameManager gameManager;
    public UDPReceiver udpReceiver;

    [Header("UI roots (enable/disable per phase)")]
    public GameObject hubPanel;
    public GameObject introPanel;
    public GameObject sim1MissionBriefingPanel;
    public GameObject sim1CalibrationPanel;
    public GameObject sim1ResultsPanel;
    public GameObject sim2BriefingPanel;
    public GameObject sim2ResultsPanel;
    public GameObject safetyWarningPanel;
    public TextMeshProUGUI safetyWarningText;
    [TextArea] public string stressWarningMessage = "Warning: this simulation contains stress stimuli (alarm audio, time pressure, emergency context). You can pause at any time with Esc and quit safely.";

    [Header("Safety warning presentation")]
    [Tooltip("If true, the warning is a centered card instead of stretching across the whole canvas.")]
    public bool safetyWarningUseCenterCard = true;
    public Vector2 safetyWarningCardSize = new Vector2(680f, 420f);
    public Color safetyWarningCardColor = new Color(0.32f, 0.11f, 0.13f, 0.98f);
    [Tooltip("Dark overlay behind the card so the scene stays visible but de-emphasized.")]
    public bool safetyWarningShowDimBackdrop = true;
    public Color safetyWarningDimColor = new Color(0f, 0f, 0f, 0.55f);

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

    [Header("Optional feedback")]
    public AudioSource sirenLoop;
    public AudioSource narrationAudioSource;
    public AudioClip introNarrationClip;
    [Tooltip("Optional voice-over for the baseline calibration screen.")]
    public AudioClip calibrationNarrationClip;
    [Tooltip("Optional voice-over for Simulation 1 mission briefing (instructions before Start mission).")]
    public AudioClip missionBriefingNarrationClip;
    public UnityEvent onSimulation1Started;
    public UnityEvent onSimulation1Ended;

    [Header("Live stress / link")]
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
    public TextMeshProUGUI calibrationStatusText;
    public TextMeshProUGUI resultsSummaryText;
    [Tooltip("Optional: when both Sim 1 metrics + recommendations are set, the summary is split into two columns.")]
    public TextMeshProUGUI sim1ResultsMetricsText;
    public TextMeshProUGUI sim1ResultsRecommendationsText;
    public TextMeshProUGUI sim2BriefingBodyText;
    public TextMeshProUGUI sim2ResultsSummaryText;
    [Tooltip("Optional: when both Sim 2 metrics + recommendations are set, the summary is split into two columns.")]
    public TextMeshProUGUI sim2ResultsMetricsText;
    public TextMeshProUGUI sim2ResultsRecommendationsText;
    public TextMeshProUGUI simulationActiveHudText;
    public SimpleStressLineGraph sim2HrvResultsGraph;
    public float sim2HrvGraphMaxDisplay = 100f;

    [Header("Results two-column layout")]
    [Tooltip("Horizontal gap between the metrics and recommendations columns (world space in canvas units).")]
    public float resultsColumnGap = 20f;

    [Header("Results screen readability")]
    [Tooltip("Darken full-screen results panels and add card backgrounds behind column text.")]
    public bool applyResultsReadabilityStyle = true;
    [Tooltip("Full-panel tint (simulates a modal overlay over the 3D view).")]
    public Color resultsScreenDimColor = new Color(0.02f, 0.04f, 0.08f, 0.82f);
    [Tooltip("Background behind each metrics / recommendations column.")]
    public Color resultsCardColor = new Color(0.07f, 0.09f, 0.13f, 0.96f);
    public Color resultsColumnTextColor = new Color(0.93f, 0.95f, 1f, 1f);
    [Tooltip("Extra horizontal inset so text does not touch screen edges (increase if text clips on world-space canvas).")]
    public float resultsExtraSideMargin = 72f;
    [Tooltip("Extra space from the top so columns sit below the SCI graph / header area (Sim 1).")]
    public float resultsSim1TopInset = 260f;
    [Tooltip("Extra space from the top for Sim 2 results (HRV graph).")]
    public float resultsSim2TopInset = 220f;
    [Tooltip("Padding between card edge and text inside each column.")]
    public float resultsCardInnerPadding = 20f;

    [Header("UI polish")]
    public bool autoPolishUi = false;
    public Color panelTint = new Color(0.07f, 0.11f, 0.16f, 0.84f);
    public Color buttonColor = new Color(0.16f, 0.35f, 0.56f, 1f);
    public Color buttonTextColor = Color.white;
    public Vector2 buttonPreferredSize = new Vector2(340f, 72f);
    public float bodyTextSize = 32f;
    public float buttonTextSize = 30f;
    public float panelSidePadding = 80f;
    public float panelTopPadding = 120f;
    public float panelBottomPadding = 180f;
    public float buttonBottomY = 70f;
    public float buttonSpacingY = 86f;

    [TextArea]
    public string hubTitle = "VR Stress Response Trainer";

    [TextArea]
    public string hubConnectionStatusDemo =
        "Smartwatch: Not connected (simulated HR/HRV)\nAndroid gateway: Not in use";

    [TextArea]
    public string introNarrationText =
        "In recent years, many of us have experienced stress and pressure due to emergency situations and war.\n\n" +
        "This training experience is designed to help improve your ability to function under stress.\n\n" +
        "Please connect your smartwatch. In each simulation, your physiological response is measured and at the end you receive practical recommendations for next time.";

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
        "Stand still and relax. We are calibrating your heart-rate metrics.\n\n" +
        "Take a calm view of the space (home / courtyard). No alarm will play during this step.";

    [TextArea]
    public string missionBriefingBody =
        "Simulation 1 — Emergency preparedness\n\n" +
        "A loud continuous siren will start when the mission begins.\n\n" +
        "Collect three essential items inside the home:\n" +
        "• Water\n" +
        "• First aid kit\n" +
        "• Emergency bag\n\n" +
        "After collecting all 3 items, run to the Mamad (shelter) outside.\n\n" +
        "When you are ready, press Start mission.";

    [TextArea]
    public string sim2BriefingBody =
        "Simulation 2 — First aid under pressure\n\n" +
        "You are now in an outdoor courtyard with ongoing alarm conditions.\n" +
        "Approach the casualty and complete first aid.\n\n" +
        "Maintain controlled breathing and focus on one action at a time.\n\n" +
        "Press Start Simulation 2 when ready.";

    public float calibrationDurationSeconds = 60f;
    public bool runSimulation2InSameScene = true;
    public int simulation2SceneIndex = 1;

    public Phase CurrentPhase { get; private set; } = Phase.Gate;

    private float _calibrationTimer;
    private bool _sim2Subscribed;
    private bool _paused;
    private PendingStart _pendingStart = PendingStart.None;
    private GameObject _safetyWarningDimBackdrop;

    private enum PendingStart
    {
        None,
        Simulation1,
        Simulation2
    }

    void Start()
    {
        EnsureRuntimeResultsSplitTexts();

        if (autoPolishUi)
            ApplyUiPolish();

        SetupResultsColumnLayouts();

        ApplySafetyWarningCardLayout();

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
        SetActiveSafe(pausePanel, false);
        SetSafetyWarningVisible(false);
        SetSimulation2Status(sim2BriefingBody);
    }

    public void ApplyDefaultCopyToUi()
    {
        if (hubTitleText != null)
            hubTitleText.text = hubTitle;
        if (hubConnectionStatusText != null)
            hubConnectionStatusText.text = hubConnectionStatusDemo;
        if (introBodyText != null)
            introBodyText.text = introNarrationText;
        if (missionBriefingBodyText != null)
            missionBriefingBodyText.text = missionBriefingBody;
        if (sim2BriefingBodyText != null)
            sim2BriefingBodyText.text = sim2BriefingBody;

        ApplyDefaultButtonTexts();

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
            if (calibrationStatusText != null)
            {
                calibrationStatusText.text =
                    $"{calibrationInstruction}\n\n" +
                    $"Time remaining: {remaining:F0} s\n" +
                    $"Live (demo) — HR: {physiology.CurrentHeartRate:F0} bpm | HRV: {physiology.CurrentHrvMs:F1} ms";
            }

            if (_calibrationTimer >= calibrationDurationSeconds)
                FinishCalibrationAndShowMissionBriefing();
        }

        UpdateGatewayDisconnectUi();
        UpdateActivePhaseHud();
    }

    /// <summary>Gate start button — opens intro panel and optional narration.</summary>
    public void UI_StartSimulation1()
    {
        UI_StartIntro();
    }

    public void UI_StartIntro()
    {
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
        CurrentPhase = Phase.Simulation1Calibration;
        _calibrationTimer = 0f;
        StopIntroNarration();
        PlayCalibrationNarration();
        physiology?.StartBaselineCapture();
        ApplyPhaseUI();
    }

    /// <summary>Legacy hub button name in scene — same as <see cref="UI_StartSimulation1"/>.</summary>
    public void UI_OpenSimulation1() => UI_StartSimulation1();

    /// <summary>Legacy — starts calibration directly.</summary>
    public void UI_StartBaseline() => UI_ContinueFromIntro();

    private void FinishCalibrationAndShowMissionBriefing()
    {
        StopCalibrationNarration();
        physiology?.StopBaselineCaptureAndLock();
        if (physiology != null)
            SessionHistoryStore.BeginSession(physiology.HrvBaselineMs);

        CurrentPhase = Phase.Simulation1MissionBriefing;
        if (missionBriefingBodyText != null && physiology != null)
        {
            missionBriefingBodyText.text =
                missionBriefingBody.TrimEnd() +
                $"\n\nBaseline locked — HRV baseline: {physiology.HrvBaselineMs:F1} ms";
        }

        ApplyPhaseUI();
        PlayMissionBriefingNarration();
    }

    public void UI_BeginSimulation1()
    {
        if (ShowSafetyWarningFor(PendingStart.Simulation1))
            return;

        BeginSimulation1Now();
    }

    private void BeginSimulation1Now()
    {
        StopCalibrationNarration();
        StopMissionBriefingNarration();
        CurrentPhase = Phase.Simulation1Active;
        SetSimulationGameplayState(true, false);
        MovePlayerToSpawn(simulation1SpawnPoint, simulation1SpawnUseWorldCoordinates, simulation1SpawnWorldPosition, simulation1SpawnWorldEuler);
        recorder?.BeginRecording();
        if (physiology != null)
            physiology.StressorActive = true;
        PlaySiren();
        onSimulation1Started?.Invoke();
        ApplyPhaseUI();
        SetHudVisible(true);

        if (gameManager != null)
            gameManager.OnAllItemsCollected += HandleSim1Complete;
    }

    private void HandleSim1Complete()
    {
        if (CurrentPhase != Phase.Simulation1Active) return;

        if (gameManager != null)
            gameManager.OnAllItemsCollected -= HandleSim1Complete;

        if (physiology != null)
            physiology.StressorActive = false;
        StopSiren();
        recorder?.EndRecording();
        onSimulation1Ended?.Invoke();
        SetActiveSafe(highStressWarningRoot, false);
        SetHudVisible(false);

        CurrentPhase = Phase.Simulation1Results;
        if (resultsGraph != null && recorder != null)
            resultsGraph.SetFromSciPoints(recorder.SciHistory);

        if (physiology != null && recorder != null)
        {
            float peakSci = recorder.SciHistory.Count > 0 ? MaxSci(recorder.SciHistory) : 0f;
            float meanSci = recorder.SciHistory.Count > 0 ? MeanSci(recorder.SciHistory) : 0f;
            var peakBand = StressChangeIndexCalculator.Classify(peakSci);
            SessionHistoryStore.UpdateAfterSim1(recorder.SciHistory, physiology.HrvBaselineMs, recorder.sampleIntervalSeconds);
            string nextStage = StressRecommendations.BeforeNextStageBreathingTip();

            if (UseSim1SplitColumns())
            {
                if (resultsSummaryText != null)
                    resultsSummaryText.gameObject.SetActive(false);

                var metrics = new StringBuilder();
                metrics.AppendLine("<b>Results</b>");
                metrics.AppendLine();
                metrics.AppendLine("<color=#B8D4EE>Simulation 1</color>");
                metrics.AppendLine();
                metrics.AppendLine($"Baseline HRV: {physiology.HrvBaselineMs:F1} ms (your calm reference).");
                metrics.AppendLine(
                    "SCI (Stress Change Index) measures how far current HRV drifts below that baseline — higher % means a larger stress shift.");
                metrics.AppendLine();
                metrics.AppendLine($"Peak SCI: {peakSci:F1}% ({StressChangeIndexCalculator.BandLabel(peakBand)})");
                metrics.AppendLine($"Average SCI: {meanSci:F1}%");
                metrics.AppendLine($"Samples: {recorder.SciHistory.Count}");

                var rec = new StringBuilder();
                rec.AppendLine("<b>Recommendations</b>");
                rec.AppendLine();
                rec.AppendLine(StressRecommendations.BuildBehavioralTips(recorder.SciHistory));
                rec.AppendLine();
                rec.AppendLine(nextStage);

                sim1ResultsMetricsText.text = metrics.ToString().TrimEnd();
                sim1ResultsRecommendationsText.text = rec.ToString().TrimEnd();
            }
            else if (resultsSummaryText != null)
            {
                resultsSummaryText.gameObject.SetActive(true);
                string tips = StressRecommendations.BuildFromSciHistory(recorder.SciHistory);

                var sb = new StringBuilder();
                sb.AppendLine("Simulation 1 — Results");
                sb.AppendLine();
                sb.AppendLine($"Baseline HRV: {physiology.HrvBaselineMs:F1} ms (your calm reference).");
                sb.AppendLine(
                    "SCI (Stress Change Index) measures how far current HRV drifts below that baseline — higher % means a larger stress shift.");
                sb.AppendLine();
                sb.AppendLine($"Peak SCI: {peakSci:F1}% ({StressChangeIndexCalculator.BandLabel(peakBand)})");
                sb.AppendLine($"Average SCI: {meanSci:F1}%");
                sb.AppendLine($"Samples: {recorder.SciHistory.Count}");
                sb.AppendLine();
                sb.AppendLine("Recommendations:");
                sb.AppendLine(tips);
                sb.AppendLine();
                sb.AppendLine(nextStage);
                resultsSummaryText.text = sb.ToString();
            }
        }

        ApplyPhaseUI();
        FinalizeResultsScreenPresentation(true);
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

    public void UI_GoToSimulation2()
    {
        if (sim2BriefingPanel == null)
        {
            UI_StartSimulation2Scene();
            return;
        }
        CurrentPhase = Phase.Simulation2Briefing;
        SetSimulationGameplayState(false, false);
        ApplyPhaseUI();
    }

    public void UI_StartSimulation2Scene()
    {
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
        StopCalibrationNarration();
        StopMissionBriefingNarration();
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
        SetSafetyWarningVisible(false);
        var action = _pendingStart;
        _pendingStart = PendingStart.None;

        if (action == PendingStart.Simulation1)
            BeginSimulation1Now();
        else if (action == PendingStart.Simulation2)
            StartSimulation2Now();
    }

    public void UI_CancelSafetyWarning()
    {
        _pendingStart = PendingStart.None;
        SetSafetyWarningVisible(false);
    }

    public void UI_TogglePause() => SetPaused(!_paused);
    public void UI_Resume() => SetPaused(false);

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
        SetActiveSafe(introPanel, CurrentPhase == Phase.IntroNarration);
        SetActiveSafe(sim1CalibrationPanel, CurrentPhase == Phase.Simulation1Calibration);
        SetActiveSafe(sim1MissionBriefingPanel, CurrentPhase == Phase.Simulation1MissionBriefing);
        SetActiveSafe(sim1ResultsPanel, CurrentPhase == Phase.Simulation1Results);
        SetActiveSafe(sim2BriefingPanel, CurrentPhase == Phase.Simulation2Briefing);
        SetActiveSafe(sim2ResultsPanel, CurrentPhase == Phase.Simulation2Results);
        ApplyPlayerInteractionMode();
    }

    void OnDisable()
    {
        if (playerFpsController != null)
            playerFpsController.SetUiMenuMode(true);
    }

    /// <summary>Unlock cursor for menu phases; lock only during active simulations.</summary>
    private void ApplyPlayerInteractionMode()
    {
        if (playerFpsController == null) return;
        bool menuPhase = CurrentPhase != Phase.Simulation1Active && CurrentPhase != Phase.Simulation2Active;
        playerFpsController.SetUiMenuMode(menuPhase);
    }

    private static void SetActiveSafe(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on)
            go.SetActive(on);
    }

    private bool UseSim1SplitColumns() =>
        sim1ResultsMetricsText != null && sim1ResultsRecommendationsText != null;

    private bool UseSim2SplitColumns() =>
        sim2ResultsMetricsText != null && sim2ResultsRecommendationsText != null;

    private void SetupResultsColumnLayouts()
    {
        ApplyResultsPanelsRootDim();

        if (UseSim1SplitColumns() && sim1ResultsPanel != null)
        {
            ConfigureResultsSplitColumns(sim1ResultsMetricsText, sim1ResultsRecommendationsText, resultsSim1TopInset, sim1ResultsPanel.transform);
            if (resultsSummaryText != null)
                resultsSummaryText.gameObject.SetActive(false);
        }

        if (UseSim2SplitColumns() && sim2ResultsPanel != null)
        {
            ConfigureResultsSplitColumns(sim2ResultsMetricsText, sim2ResultsRecommendationsText, resultsSim2TopInset, sim2ResultsPanel.transform);
            if (sim2ResultsSummaryText != null)
                sim2ResultsSummaryText.gameObject.SetActive(false);
        }

        if (applyResultsReadabilityStyle)
        {
            if (!UseSim1SplitColumns() && resultsSummaryText != null)
                ApplyReadabilityToResultsText(resultsSummaryText);
            if (!UseSim2SplitColumns() && sim2ResultsSummaryText != null)
                ApplyReadabilityToResultsText(sim2ResultsSummaryText);
        }
    }

    /// <summary>
    /// If split column TMPs are not wired in the Inspector, clone the existing summary text so the two-column layout works without manual scene edits.
    /// </summary>
    private void EnsureRuntimeResultsSplitTexts()
    {
        if (sim1ResultsMetricsText == null && sim1ResultsRecommendationsText == null
            && resultsSummaryText != null && sim1ResultsPanel != null)
        {
            sim1ResultsMetricsText = Instantiate(resultsSummaryText, sim1ResultsPanel.transform);
            sim1ResultsMetricsText.gameObject.name = "ResultsMetricsColumn";
            sim1ResultsRecommendationsText = Instantiate(resultsSummaryText, sim1ResultsPanel.transform);
            sim1ResultsRecommendationsText.gameObject.name = "ResultsRecommendationsColumn";
            sim1ResultsMetricsText.transform.SetAsLastSibling();
            sim1ResultsRecommendationsText.transform.SetAsLastSibling();
            resultsSummaryText.gameObject.SetActive(false);
        }

        if (sim2ResultsMetricsText == null && sim2ResultsRecommendationsText == null
            && sim2ResultsSummaryText != null && sim2ResultsPanel != null)
        {
            sim2ResultsMetricsText = Instantiate(sim2ResultsSummaryText, sim2ResultsPanel.transform);
            sim2ResultsMetricsText.gameObject.name = "Sim2ResultsMetricsColumn";
            sim2ResultsRecommendationsText = Instantiate(sim2ResultsSummaryText, sim2ResultsPanel.transform);
            sim2ResultsRecommendationsText.gameObject.name = "Sim2ResultsRecommendationsColumn";
            sim2ResultsMetricsText.transform.SetAsLastSibling();
            sim2ResultsRecommendationsText.transform.SetAsLastSibling();
            sim2ResultsSummaryText.gameObject.SetActive(false);
        }
    }

    private void ApplyResultsPanelsRootDim()
    {
        if (!applyResultsReadabilityStyle)
            return;

        DimResultsPanelRoot(sim1ResultsPanel);
        DimResultsPanelRoot(sim2ResultsPanel);
    }

    private void DimResultsPanelRoot(GameObject panelRoot)
    {
        if (panelRoot == null)
            return;

        var img = panelRoot.GetComponent<Image>();
        if (img != null)
        {
            img.color = resultsScreenDimColor;
            img.raycastTarget = true;
        }
    }

    private void ApplyReadabilityToResultsText(TextMeshProUGUI tmp)
    {
        if (tmp == null || !applyResultsReadabilityStyle)
            return;

        tmp.color = resultsColumnTextColor;
    }

    private void ConfigureResultsSplitColumns(
        TextMeshProUGUI leftColumn,
        TextMeshProUGUI rightColumn,
        float extraTopInset,
        Transform panelRoot)
    {
        if (leftColumn == null || rightColumn == null || panelRoot == null)
            return;

        float halfGap = resultsColumnGap * 0.5f;
        float side = panelSidePadding + resultsExtraSideMargin;
        float top = panelTopPadding + extraTopInset;

        DestroyLegacyCardBackdropsUnder(panelRoot);

        var leftOuterMin = new Vector2(side, panelBottomPadding);
        var leftOuterMax = new Vector2(-halfGap, -top);
        var rightOuterMin = new Vector2(halfGap, panelBottomPadding);
        var rightOuterMax = new Vector2(-side, -top);

        PlaceColumnInCard(leftColumn, panelRoot, leftOuterMin, leftOuterMax, true);
        PlaceColumnInCard(rightColumn, panelRoot, rightOuterMin, rightOuterMax, false);

        ApplyReadabilityToResultsText(leftColumn);
        ApplyReadabilityToResultsText(rightColumn);

        if (leftColumn.transform.parent != null)
            leftColumn.transform.parent.SetAsLastSibling();
        if (rightColumn.transform.parent != null)
            rightColumn.transform.parent.SetAsLastSibling();

        BringPanelButtonsToFront(panelRoot);
    }

    private void DestroyLegacyCardBackdropsUnder(Transform panelRoot)
    {
        if (panelRoot == null)
            return;

        const string legacyBackdropSuffix = "_CardBackdrop";
        for (int i = panelRoot.childCount - 1; i >= 0; i--)
        {
            var ch = panelRoot.GetChild(i);
            if (ch.name.EndsWith(legacyBackdropSuffix, StringComparison.Ordinal))
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(ch.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(ch.gameObject);
            }
        }
    }

    /// <summary>
    /// Puts column text inside a card with a fixed rect + RectMask2D so TMP wraps inside the column and cannot bleed into the other column.
    /// </summary>
    private void PlaceColumnInCard(
        TextMeshProUGUI tmp,
        Transform panelRoot,
        Vector2 outerOffsetMin,
        Vector2 outerOffsetMax,
        bool leftHalfOfScreen)
    {
        if (tmp == null || panelRoot == null)
            return;

        float pad = resultsCardInnerPadding;
        const string cardSuffix = "_ColumnCard";

        Transform cardT = tmp.transform.parent;
        GameObject cardGo;
        RectTransform cardRt;

        if (cardT != null && cardT.name.EndsWith(cardSuffix, StringComparison.Ordinal) && cardT.parent == panelRoot)
        {
            cardGo = cardT.gameObject;
            cardRt = cardGo.GetComponent<RectTransform>();
            var imgEx = cardGo.GetComponent<Image>();
            if (imgEx != null)
                imgEx.color = applyResultsReadabilityStyle ? resultsCardColor : new Color(0.08f, 0.1f, 0.14f, 0.94f);
            if (cardGo.GetComponent<RectMask2D>() == null)
                cardGo.AddComponent<RectMask2D>();
        }
        else
        {
            if (tmp.transform.parent != panelRoot)
                tmp.transform.SetParent(panelRoot, false);

            cardGo = new GameObject(tmp.name + cardSuffix);
            cardRt = cardGo.AddComponent<RectTransform>();
            cardGo.transform.SetParent(panelRoot, false);
            tmp.transform.SetParent(cardRt, false);

            var img = cardGo.AddComponent<Image>();
            img.sprite = GetUiWhiteSprite();
            img.type = Image.Type.Simple;
            img.color = applyResultsReadabilityStyle ? resultsCardColor : new Color(0.08f, 0.1f, 0.14f, 0.94f);
            img.raycastTarget = false;
            cardGo.AddComponent<RectMask2D>();
        }

        cardRt.anchorMin = leftHalfOfScreen ? new Vector2(0f, 0f) : new Vector2(0.5f, 0f);
        cardRt.anchorMax = leftHalfOfScreen ? new Vector2(0.5f, 1f) : new Vector2(1f, 1f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.offsetMin = outerOffsetMin;
        cardRt.offsetMax = outerOffsetMax;

        var textRt = tmp.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.offsetMin = new Vector2(pad, pad);
        textRt.offsetMax = new Vector2(-pad, -pad);
        textRt.localScale = Vector3.one;

        tmp.enableWordWrapping = true;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.richText = true;
        tmp.margin = Vector4.zero;
        tmp.alignment = TextAlignmentOptions.TopLeft;
    }

    private static Sprite _uiWhiteSprite;

    private static Sprite GetUiWhiteSprite()
    {
        if (_uiWhiteSprite == null)
        {
            var tex = Texture2D.whiteTexture;
            _uiWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        return _uiWhiteSprite;
    }

    /// <summary>Re-apply dim/cards after the results panel becomes active (layout was first computed while hidden).</summary>
    private void FinalizeResultsScreenPresentation(bool simulation1)
    {
        if (applyResultsReadabilityStyle)
            ApplyResultsPanelsRootDim();
        if (simulation1)
        {
            if (UseSim1SplitColumns() && sim1ResultsPanel != null)
                ConfigureResultsSplitColumns(sim1ResultsMetricsText, sim1ResultsRecommendationsText, resultsSim1TopInset, sim1ResultsPanel.transform);
            else if (resultsSummaryText != null)
                ApplyReadabilityToResultsText(resultsSummaryText);
        }
        else
        {
            if (UseSim2SplitColumns() && sim2ResultsPanel != null)
                ConfigureResultsSplitColumns(sim2ResultsMetricsText, sim2ResultsRecommendationsText, resultsSim2TopInset, sim2ResultsPanel.transform);
            else if (sim2ResultsSummaryText != null)
                ApplyReadabilityToResultsText(sim2ResultsSummaryText);
        }

        BringPanelButtonsToFront(simulation1 ? sim1ResultsPanel?.transform : sim2ResultsPanel?.transform);
        Canvas.ForceUpdateCanvases();
    }

    private static void BringPanelButtonsToFront(Transform panelRoot)
    {
        if (panelRoot == null)
            return;

        var buttons = panelRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
                buttons[i].transform.SetAsLastSibling();
        }
    }

    private void SetSimulationGameplayState(bool simulation1On, bool simulation2On)
    {
        if (simulation1GameplayRoot != null)
            simulation1GameplayRoot.SetActive(simulation1On);

        if (simulation2GameplayRoot != null)
            simulation2GameplayRoot.SetActive(simulation2On);
    }

    private void PlaySiren()
    {
        if (sirenLoop == null) return;
        sirenLoop.loop = true;
        if (!sirenLoop.isPlaying)
            sirenLoop.Play();
    }

    private void StopSiren()
    {
        if (sirenLoop == null) return;
        sirenLoop.Stop();
    }

    private void PlayIntroNarration()
    {
        if (narrationAudioSource == null || introNarrationClip == null) return;
        narrationAudioSource.loop = false;
        narrationAudioSource.clip = introNarrationClip;
        narrationAudioSource.Play();
    }

    private void StopIntroNarration()
    {
        if (narrationAudioSource == null) return;
        narrationAudioSource.Stop();
    }

    private void PlayCalibrationNarration()
    {
        if (narrationAudioSource == null || calibrationNarrationClip == null) return;
        narrationAudioSource.loop = false;
        narrationAudioSource.clip = calibrationNarrationClip;
        narrationAudioSource.Play();
    }

    private void StopCalibrationNarration()
    {
        if (narrationAudioSource == null) return;
        if (narrationAudioSource.clip == calibrationNarrationClip)
            narrationAudioSource.Stop();
    }

    private void PlayMissionBriefingNarration()
    {
        if (narrationAudioSource == null || missionBriefingNarrationClip == null) return;
        narrationAudioSource.loop = false;
        narrationAudioSource.clip = missionBriefingNarrationClip;
        narrationAudioSource.Play();
    }

    private void StopMissionBriefingNarration()
    {
        if (narrationAudioSource == null) return;
        if (narrationAudioSource.clip == missionBriefingNarrationClip)
            narrationAudioSource.Stop();
    }

    private void UpdateIntroSubtitleByNarrationTime()
    {
        if (introBodyText == null)
            return;

        float t = 0f;
        if (narrationAudioSource != null && narrationAudioSource.isPlaying)
            t = narrationAudioSource.time;

        ShowIntroParagraph(t);
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
        {
            playerRoot.SetPositionAndRotation(pos, rot);
        }
    }

    private void StartSimulation2InSameScene()
    {
        CurrentPhase = Phase.Simulation2Active;
        SetSimulationGameplayState(false, true);
        if (simulation2SpawnPoint != null)
            MovePlayerToSpawn(simulation2SpawnPoint, false, default, default);
        else if (simulation2SpawnUseWorldCoordinates)
            MovePlayerToSpawn(null, true, simulation2SpawnWorldPosition, simulation2SpawnWorldEuler);
        else if (simulation1SpawnPoint != null)
            MovePlayerToSpawn(simulation1SpawnPoint, false, default, default);
        else if (simulation1SpawnUseWorldCoordinates)
            MovePlayerToSpawn(null, true, simulation1SpawnWorldPosition, simulation1SpawnWorldEuler);
        recorder?.Clear();
        recorder?.BeginRecording();
        if (physiology != null)
            physiology.StressorActive = true;
        PlaySiren();
        SetHudVisible(true);
        SetSimulation2Status("Simulation 2 started. Approach the wounded man and press E to provide first aid.");
        SubscribeSimulation2IfNeeded();
        ApplyPhaseUI();
    }

    private void ApplySafetyWarningCardLayout()
    {
        if (!safetyWarningUseCenterCard || safetyWarningPanel == null)
            return;

        var cardRt = safetyWarningPanel.GetComponent<RectTransform>();
        if (cardRt == null)
            return;

        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = safetyWarningCardSize;
        cardRt.anchoredPosition = Vector2.zero;

        var img = safetyWarningPanel.GetComponent<Image>();
        if (img != null)
            img.color = safetyWarningCardColor;

        if (safetyWarningShowDimBackdrop)
            EnsureSafetyWarningDimBackdrop(cardRt.parent);

        if (safetyWarningText != null)
        {
            var tr = safetyWarningText.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.anchoredPosition = new Vector2(0f, 48f);
            tr.sizeDelta = new Vector2(safetyWarningCardSize.x - 48f, 220f);
            safetyWarningText.enableWordWrapping = true;
            safetyWarningText.textWrappingMode = TextWrappingModes.Normal;
            safetyWarningText.alignment = TextAlignmentOptions.Center;
            safetyWarningText.margin = Vector4.zero;
        }

        var buttons = safetyWarningPanel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var brt = buttons[i].GetComponent<RectTransform>();
            if (brt == null)
                continue;

            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(240f, 52f);
            string n = buttons[i].gameObject.name.ToLowerInvariant();
            if (n.Contains("continue"))
                brt.anchoredPosition = new Vector2(0f, -52f);
            else if (n.Contains("cancel"))
                brt.anchoredPosition = new Vector2(0f, -126f);
        }
    }

    private void EnsureSafetyWarningDimBackdrop(Transform panelParent)
    {
        if (panelParent == null || safetyWarningPanel == null)
            return;

        if (_safetyWarningDimBackdrop == null)
        {
            _safetyWarningDimBackdrop = new GameObject("SafetyWarningDimBackdrop");
            _safetyWarningDimBackdrop.transform.SetParent(panelParent, false);
            var dimRt = _safetyWarningDimBackdrop.AddComponent<RectTransform>();
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;
            var dimImg = _safetyWarningDimBackdrop.AddComponent<Image>();
            dimImg.sprite = GetUiWhiteSprite();
            dimImg.color = safetyWarningDimColor;
            dimImg.raycastTarget = true;
        }

        int panelIdx = safetyWarningPanel.transform.GetSiblingIndex();
        _safetyWarningDimBackdrop.transform.SetSiblingIndex(panelIdx);
    }

    private void SetSafetyWarningVisible(bool visible)
    {
        SetActiveSafe(safetyWarningPanel, visible);
        if (_safetyWarningDimBackdrop != null)
            SetActiveSafe(_safetyWarningDimBackdrop, visible && safetyWarningShowDimBackdrop);
    }

    private bool ShowSafetyWarningFor(PendingStart startAction)
    {
        if (safetyWarningPanel == null)
            return false;

        _pendingStart = startAction;
        if (safetyWarningText != null)
            safetyWarningText.text = stressWarningMessage;
        SetSafetyWarningVisible(true);
        return true;
    }

    private void HandleSafetyKeys()
    {
        if (Input.GetKeyDown(pauseKey))
            SetPaused(!_paused);

        if (Input.GetKeyDown(quickQuitKey))
            UI_QuitApplication();
    }

    private void SetPaused(bool paused)
    {
        _paused = paused;
        Time.timeScale = paused ? 0f : 1f;
        SetActiveSafe(pausePanel, paused);
        if (paused)
            SetSafetyWarningVisible(false);
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

    private void HandleSimulation2Complete()
    {
        if (CurrentPhase != Phase.Simulation2Active)
            return;

        UnsubscribeSimulation2IfNeeded();
        if (physiology != null)
            physiology.StressorActive = false;
        StopSiren();
        recorder?.EndRecording();
        SetSimulationGameplayState(false, false);
        SetHudVisible(false);
        SetActiveSafe(highStressWarningRoot, false);
        CurrentPhase = Phase.Simulation2Results;

        float peakSci = 0f;
        float meanSci = 0f;
        if (recorder != null && recorder.SciHistory.Count > 0)
        {
            peakSci = MaxSci(recorder.SciHistory);
            meanSci = MeanSci(recorder.SciHistory);
            SessionHistoryStore.FinalizeAfterSim2(recorder.SciHistory, recorder.sampleIntervalSeconds);
        }

        string tips = recorder != null && recorder.SciHistory.Count > 0
            ? StressRecommendations.BuildFromSciHistory(recorder.SciHistory)
            : "Complete another Simulation 2 run to generate tailored guidance.";
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

            var metrics = new StringBuilder();
            metrics.AppendLine("<b>Results</b>");
            metrics.AppendLine();
            metrics.AppendLine("<color=#B8D4EE>Simulation 2</color>");
            metrics.AppendLine();
            metrics.AppendLine($"Peak SCI: {peakSci:F1}%");
            metrics.AppendLine($"Average SCI: {meanSci:F1}%");
            metrics.AppendLine();
            metrics.AppendLine("HRV summary (this simulation only):");
            metrics.AppendLine($"Min HRV: {minHrv:F1} ms");
            metrics.AppendLine($"Max HRV: {maxHrv:F1} ms");
            metrics.AppendLine($"Avg HRV: {avgHrv:F1} ms");
            metrics.AppendLine($"Samples: {(recorder != null ? recorder.HrvHistory.Count : 0)}");

            var rec = new StringBuilder();
            rec.AppendLine("<b>Recommendations</b>");
            rec.AppendLine();
            rec.AppendLine(tips);
            rec.AppendLine();
            rec.AppendLine("Press <b>Back To Hub</b> when ready.");

            sim2ResultsMetricsText.text = metrics.ToString().TrimEnd();
            sim2ResultsRecommendationsText.text = rec.ToString().TrimEnd();
        }
        else if (sim2ResultsSummaryText != null)
        {
            sim2ResultsSummaryText.gameObject.SetActive(true);
            var sb = new StringBuilder();
            sb.AppendLine("Simulation 2 — Results");
            sb.AppendLine();
            sb.AppendLine($"Peak SCI: {peakSci:F1}%");
            sb.AppendLine($"Average SCI: {meanSci:F1}%");
            sb.AppendLine();
            sb.AppendLine("HRV summary (this simulation only):");
            sb.AppendLine($"Min HRV: {minHrv:F1} ms");
            sb.AppendLine($"Max HRV: {maxHrv:F1} ms");
            sb.AppendLine($"Avg HRV: {avgHrv:F1} ms");
            sb.AppendLine($"Samples: {(recorder != null ? recorder.HrvHistory.Count : 0)}");
            sb.AppendLine();
            sb.AppendLine("Recommendations:");
            sb.AppendLine(tips);
            sb.AppendLine();
            sb.AppendLine("Press Back To Hub when ready.");
            sim2ResultsSummaryText.text = sb.ToString();
        }
        else
        {
            SetSimulation2Status("First aid completed. Results ready. Press Back To Hub.");
        }

        if (sim2HrvResultsGraph != null && recorder != null && recorder.HrvHistory.Count > 0)
            sim2HrvResultsGraph.SetFromValues(recorder.HrvHistory, sim2HrvGraphMaxDisplay);
        else
            Debug.LogWarning("Sim2 HRV graph was not rendered. Check sim2HrvResultsGraph reference and recorded HRV samples.");

        ApplyPhaseUI();
        FinalizeResultsScreenPresentation(false);
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

    private void ApplyUiPolish()
    {
        StylePanel(hubPanel);
        StylePanel(introPanel);
        StylePanel(sim1MissionBriefingPanel);
        StylePanel(sim1CalibrationPanel);
        StylePanel(sim1ResultsPanel);
        StylePanel(sim2BriefingPanel);
        StylePanel(sim2ResultsPanel);

        if (introBodyText != null) introBodyText.fontSize = bodyTextSize;
        if (missionBriefingBodyText != null) missionBriefingBodyText.fontSize = bodyTextSize;
        if (calibrationStatusText != null) calibrationStatusText.fontSize = bodyTextSize;
        if (resultsSummaryText != null) resultsSummaryText.fontSize = bodyTextSize - 2f;
        if (sim1ResultsMetricsText != null) sim1ResultsMetricsText.fontSize = bodyTextSize - 2f;
        if (sim1ResultsRecommendationsText != null) sim1ResultsRecommendationsText.fontSize = bodyTextSize - 2f;
        if (sim2BriefingBodyText != null) sim2BriefingBodyText.fontSize = bodyTextSize;
        if (sim2ResultsSummaryText != null) sim2ResultsSummaryText.fontSize = bodyTextSize - 2f;
        if (sim2ResultsMetricsText != null) sim2ResultsMetricsText.fontSize = bodyTextSize - 2f;
        if (sim2ResultsRecommendationsText != null) sim2ResultsRecommendationsText.fontSize = bodyTextSize - 2f;

        FixPanelLayoutCollisions(hubPanel, hubConnectionStatusText);
        FixPanelLayoutCollisions(introPanel, introBodyText);
        FixPanelLayoutCollisions(sim1MissionBriefingPanel, missionBriefingBodyText);
        FixPanelLayoutCollisions(sim1CalibrationPanel, calibrationStatusText);
        FixPanelLayoutCollisions(sim1ResultsPanel, UseSim1SplitColumns() ? null : resultsSummaryText);
        FixPanelLayoutCollisions(sim2BriefingPanel, sim2BriefingBodyText);
        if (!UseSim2SplitColumns())
            FixPanelLayoutCollisions(sim2ResultsPanel, sim2ResultsSummaryText);
    }

    private void StylePanel(GameObject panelRoot)
    {
        if (panelRoot == null) return;

        var panelImage = panelRoot.GetComponent<Image>();
        if (panelImage != null)
            panelImage.color = panelTint;

        var buttons = panelRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            StyleButton(buttons[i]);
    }

    private void StyleButton(Button button)
    {
        if (button == null) return;

        var image = button.GetComponent<Image>();
        if (image != null)
            image.color = buttonColor;

        var rect = button.GetComponent<RectTransform>();
        if (rect != null && rect.sizeDelta.y < buttonPreferredSize.y)
            rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, buttonPreferredSize.x), buttonPreferredSize.y);

        var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.color = buttonTextColor;
            label.fontSize = buttonTextSize;
            label.alignment = TextAlignmentOptions.Center;
        }
    }

    private void ApplyDefaultButtonTexts()
    {
        var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null) continue;

            string byMethod = GuessButtonTextByMethod(button);
            if (!string.IsNullOrEmpty(byMethod))
            {
                label.text = byMethod;
                continue;
            }

            string byName = GuessButtonTextByName(button.gameObject.name);
            if (!string.IsNullOrEmpty(byName))
                label.text = byName;
        }
    }

    private void FixPanelLayoutCollisions(GameObject panelRoot, TextMeshProUGUI bodyText)
    {
        if (panelRoot == null)
            return;

        // 1) Keep main text in a safe area (top/middle), leaving room for buttons at the bottom.
        if (bodyText != null)
        {
            var textRt = bodyText.GetComponent<RectTransform>();
            if (textRt != null)
            {
                textRt.anchorMin = new Vector2(0f, 0f);
                textRt.anchorMax = new Vector2(1f, 1f);
                textRt.pivot = new Vector2(0.5f, 0.5f);
                textRt.offsetMin = new Vector2(panelSidePadding, panelBottomPadding);
                textRt.offsetMax = new Vector2(-panelSidePadding, -panelTopPadding);
            }
        }

        // 2) Stack all panel buttons from bottom center upward.
        var buttons = panelRoot.GetComponentsInChildren<Button>(true);
        float currentY = buttonBottomY;
        for (int i = 0; i < buttons.Length; i++)
        {
            var rt = buttons[i].GetComponent<RectTransform>();
            if (rt == null) continue;

            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, currentY);
            rt.sizeDelta = buttonPreferredSize;

            currentY += buttonSpacingY;
        }
    }

    private void SetButtonText(string buttonObjectName, string text)
    {
        var buttonGo = GameObject.Find(buttonObjectName);
        if (buttonGo == null) return;

        var label = buttonGo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null) return;
        label.text = text;
    }

    private static string GuessButtonTextByMethod(Button button)
    {
        int count = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            string method = button.onClick.GetPersistentMethodName(i);
            if (string.IsNullOrEmpty(method)) continue;

            if (method == "UI_StartSimulation1" || method == "UI_OpenSimulation1")
                return "Start Training";
            if (method == "UI_StartIntro")
                return "Start Intro";
            if (method == "UI_ContinueFromIntro" || method == "UI_StartBaseline")
                return "Continue";
            if (method == "UI_BeginSimulation1")
                return "Start Simulation 1";
            if (method == "UI_GoToSimulation2")
                return "Continue To Simulation 2";
            if (method == "UI_StartSimulation2Scene")
                return "Start Simulation 2";
            if (method == "UI_BackToHub")
                return "Back To Hub";
        }

        return null;
    }

    private static string GuessButtonTextByName(string buttonName)
    {
        if (string.IsNullOrEmpty(buttonName)) return null;

        string n = buttonName.ToLowerInvariant();
        if (n.Contains("start experience")) return "Start Training";
        if (n.Contains("start mission")) return "Start Simulation 1";
        if (n.Contains("movetosimulation2")) return "Continue To Simulation 2";
        if (n.Contains("btnstartsimulation2")) return "Start Simulation 2";
        if (n.Contains("continuefromintro")) return "Continue";
        if (n.Contains("back")) return "Back To Hub";
        return null;
    }
}
