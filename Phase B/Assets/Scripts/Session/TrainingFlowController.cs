using System;
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
/// Assign <see cref="simulationPickPanel"/> to show after calibration; block buttons call <see cref="UI_PickSimulation1AfterCalibration"/> /
/// <see cref="UI_PickSimulation2AfterCalibration"/>. If the pick panel is unassigned, calibration goes straight to Simulation 1 mission briefing.
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
    public GameManager gameManager;
    public UDPReceiver udpReceiver;

    [Header("UI roots (enable/disable per phase)")]
    public GameObject hubPanel;
    [Tooltip("Login / Register UI (opened from Hub). Assign a panel with LoginFlowPanel.")]
    public GameObject loginPanel;
    public GameObject introPanel;
    public GameObject sim1MissionBriefingPanel;
    public GameObject sim1CalibrationPanel;
    [Tooltip("Level / simulation selection after baseline calibration. Buttons: UI_PickSimulation1AfterCalibration, UI_PickSimulation2AfterCalibration.")]
    public GameObject simulationPickPanel;
    public GameObject sim1ResultsPanel;
    public GameObject sim2BriefingPanel;
    public GameObject sim2ResultsPanel;
    public GameObject safetyWarningPanel;
    public TextMeshProUGUI safetyWarningText;
    [TextArea] public string stressWarningMessage = "Warning: this simulation contains stress stimuli (alarm audio, time pressure, emergency context). You can pause at any time with Esc and quit safely.";

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
    public TextMeshProUGUI simulationActiveHudText;
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

    [Header("UI polish")]
    public bool autoPolishUi = false;
    [Tooltip("If false, keep button labels exactly as set in the Inspector/scene.")]
    public bool applyDefaultButtonTextsAtRuntime = false;
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
    [Tooltip("If false, keep Hub title text exactly as set on the TMP in the Inspector/scene.")]
    public bool applyHubTitleFromController = false;

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
    public bool IsPaused => _paused;

    private float _calibrationTimer;
    private bool _sim2Subscribed;
    private bool _paused;
    private PendingStart _pendingStart = PendingStart.None;
    private ResultsTab _currentSim1ResultsTab = ResultsTab.Result;
    private ResultsTab _currentSim2ResultsTab = ResultsTab.Result;

    private enum PendingStart
    {
        None,
        Simulation1,
        Simulation2
    }

    void Start()
    {
        if (autoPolishUi)
            ApplyUiPolish();

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
        SetActiveSafe(pausePanel, false);
        SetSafetyWarningVisible(false);
        SetSimulation2Status(sim2BriefingBody);
    }

    public void ApplyDefaultCopyToUi()
    {
        if (applyHubTitleFromController && hubTitleText != null)
            hubTitleText.text = hubTitle;
        RefreshHubConnectionStatusText();
        if (introBodyText != null)
            introBodyText.text = introNarrationText;
        if (missionBriefingBodyText != null)
            missionBriefingBodyText.text = missionBriefingBody;
        if (sim2BriefingBodyText != null)
            sim2BriefingBodyText.text = sim2BriefingBody;

        if (applyDefaultButtonTextsAtRuntime)
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
            UpdateCalibrationBaselineUi(remaining);

            if (_calibrationTimer >= calibrationDurationSeconds)
                FinishCalibrationAndShowMissionBriefing();
        }

        UpdateGatewayDisconnectUi();
        UpdateActivePhaseHud();
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

        if (splitTimer && splitHrv)
        {
            calibrationStatusText.text = calibrationInstruction;
            return;
        }

        string timePart = $"Time remaining: {remainingSeconds:F0} s";
        string livePart =
            $"Live (demo) — HR: {physiology.CurrentHeartRate:F0} bpm | HRV: {physiology.CurrentHrvMs:F1} ms";

        if (splitTimer)
            calibrationStatusText.text = $"{calibrationInstruction}\n\n{livePart}";
        else if (splitHrv)
            calibrationStatusText.text = $"{calibrationInstruction}\n\n{timePart}";
        else
        {
            calibrationStatusText.text =
                $"{calibrationInstruction}\n\n{timePart}\n{livePart}";
        }
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
        if (missionBriefingBodyText != null && physiology != null)
        {
            missionBriefingBodyText.text =
                missionBriefingBody.TrimEnd() +
                $"\n\nBaseline locked — HRV baseline: {physiology.HrvBaselineMs:F1} ms";
        }

        ApplyPhaseUI();
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
    }

    public void UI_BeginSimulation1()
    {
        if (ShowSafetyWarningFor(PendingStart.Simulation1))
            return;

        BeginSimulation1Now();
    }

    private void BeginSimulation1Now()
    {
        StopAllNarration();
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
        ApplySimulation1ResultGraphs();

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
                AppendSimulationPickFooter(rec);

                PrepareResultsTextForManualBox(sim1ResultsMetricsText);
                PrepareResultsTextForManualBox(sim1ResultsRecommendationsText);
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
                AppendSimulationPickFooter(sb);
                PrepareResultsTextForManualBox(resultsSummaryText);
                resultsSummaryText.text = sb.ToString();
            }
        }

        ApplyPhaseUI();
        ApplySim1ResultsTab(ResultsTab.Result);
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

        CurrentPhase = Phase.Simulation2Briefing;
        SetSimulationGameplayState(false, false);
        ApplyPhaseUI();
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
    public void UI_SetPause(bool paused) => SetPaused(paused);

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
        SetActiveSafe(loginPanel, CurrentPhase == Phase.Login);
        SetActiveSafe(introPanel, CurrentPhase == Phase.IntroNarration);
        SetActiveSafe(sim1CalibrationPanel, CurrentPhase == Phase.Simulation1Calibration);
        SetActiveSafe(simulationPickPanel, CurrentPhase == Phase.SimulationPick);
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

    private void StopAllNarration()
    {
        StopIntroNarration();
        StopCalibrationNarration();
        StopMissionBriefingNarration();
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

    private void SetSafetyWarningVisible(bool visible)
    {
        SetActiveSafe(safetyWarningPanel, visible);
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
        gameManager?.ClearMissionMessages();
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
            rec.AppendLine(Sim2ResultsFooterLine());

            PrepareResultsTextForManualBox(sim2ResultsMetricsText);
            PrepareResultsTextForManualBox(sim2ResultsRecommendationsText);
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
            sb.AppendLine(Sim2ResultsFooterLinePlain());
            PrepareResultsTextForManualBox(sim2ResultsSummaryText);
            sim2ResultsSummaryText.text = sb.ToString();
        }
        else if (sim2BriefingBodyText != null)
        {
            // Avoid showing fallback status text over the custom Results panel design.
            sim2BriefingBodyText.text = string.Empty;
        }

        ApplySimulation2ResultGraphs();

        ApplyPhaseUI();
        ApplySim2ResultsTab(ResultsTab.Result);
    }

    // Wire these to the three tab buttons in the Inspector (Simulation 1 panel).
    public void UI_ShowSim1ResultTab() => ApplySim1ResultsTab(ResultsTab.Result);
    public void UI_ShowSim1RecommendationsTab() => ApplySim1ResultsTab(ResultsTab.Recommendations);
    public void UI_ShowSim1PressureGraphTab() => ApplySim1ResultsTab(ResultsTab.PressureGraph);

    // Wire these to the three tab buttons in the Inspector (Simulation 2 panel).
    public void UI_ShowSim2ResultTab() => ApplySim2ResultsTab(ResultsTab.Result);
    public void UI_ShowSim2RecommendationsTab() => ApplySim2ResultsTab(ResultsTab.Recommendations);
    public void UI_ShowSim2PressureGraphTab() => ApplySim2ResultsTab(ResultsTab.PressureGraph);

    private void ApplySimulation1ResultGraphs()
    {
        if (recorder == null)
        {
            resultsGraph?.Clear();
            sim1HrvResultsGraph?.Clear();
            return;
        }

        if (resultsGraph != null)
        {
            if (recorder.SciHistory.Count > 0)
                resultsGraph.SetFromSciPoints(recorder.SciHistory);
            else
                resultsGraph.Clear();
        }

        if (sim1HrvResultsGraph != null)
        {
            if (ReferenceEquals(sim1HrvResultsGraph, resultsGraph))
            {
                // Guard against accidental inspector wiring to the same graph object.
                return;
            }

            if (recorder.HrvHistory.Count > 0)
                sim1HrvResultsGraph.SetFromValues(recorder.HrvHistory, sim1HrvGraphMaxDisplay);
            else
                sim1HrvResultsGraph.Clear();
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

        if (sim2SciResultsGraph != null)
        {
            if (recorder.SciHistory.Count > 0)
                sim2SciResultsGraph.SetFromValues(recorder.SciHistory, sim2SciGraphMaxDisplay);
            else
                sim2SciResultsGraph.Clear();
        }

        if (sim2HrvResultsGraph != null)
        {
            if (recorder.HrvHistory.Count > 0)
                sim2HrvResultsGraph.SetFromValues(recorder.HrvHistory, sim2HrvGraphMaxDisplay);
            else
                sim2HrvResultsGraph.Clear();
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

    private void ApplyUiPolish()
    {
        StylePanel(hubPanel);
        StylePanel(loginPanel);
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
        FixPanelLayoutCollisions(loginPanel, null);
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

    private string GuessButtonTextByMethod(Button button)
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
            if (method == "UI_GoToSimulation2" || method == "UI_ReturnToSimulationPickFromResults")
                return simulationPickPanel != null ? "Choose Simulation" : "Continue To Simulation 2";
            if (method == "UI_StartSimulation2Scene")
                return "Start Simulation 2";
            if (method == "UI_OpenLogin")
                return "Login / Register";
        }

        return null;
    }

    private string GuessButtonTextByName(string buttonName)
    {
        if (string.IsNullOrEmpty(buttonName)) return null;

        string n = buttonName.ToLowerInvariant();
        if (n.Contains("start experience")) return "Start Training";
        if (n.Contains("start mission")) return "Start Simulation 1";
        if (n.Contains("movetosimulation2"))
            return simulationPickPanel != null ? "Choose Simulation" : "Continue To Simulation 2";
        if (n.Contains("btnstartsimulation2")) return "Start Simulation 2";
        if (n.Contains("continuefromintro")) return "Continue";
        return null;
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

    private static void PrepareResultsTextForManualBox(TextMeshProUGUI tmp)
    {
        if (tmp == null)
            return;

        tmp.enableWordWrapping = true;
        tmp.textWrappingMode = TextWrappingModes.Normal;
    }

    /// <summary>
    /// Keeps legacy summary text hidden when Sim 1 uses manual split tabs,
    /// without overriding the scene's manual visual design.
    /// </summary>
    private void EnforceSim1ResultsVisibilityForTab(ResultsTab activeTab)
    {
        bool splitColumns = UseSim1SplitColumns();

        // In split mode, never show the legacy combined summary text.
        if (splitColumns && resultsSummaryText != null)
            SetActiveSafe(resultsSummaryText.gameObject, false);

        // Keep manual scene sizing/layout, but enforce tab-specific visibility.
        if (!splitColumns)
            return;

        bool showResult = activeTab == ResultsTab.Result;
        bool showRecommendations = activeTab == ResultsTab.Recommendations;

        if (sim1ResultsMetricsText != null)
            SetActiveSafe(sim1ResultsMetricsText.gameObject, showResult);
        if (sim1ResultsRecommendationsText != null)
            SetActiveSafe(sim1ResultsRecommendationsText.gameObject, showRecommendations);
    }

    /// <summary>
    /// Keeps legacy summary text hidden when Sim 2 uses manual split tabs,
    /// without overriding the scene's manual visual design.
    /// </summary>
    private void EnforceSim2ResultsVisibilityForTab(ResultsTab activeTab)
    {
        bool splitColumns = UseSim2SplitColumns();

        // In split mode, never show the legacy combined summary text.
        if (splitColumns && sim2ResultsSummaryText != null)
            SetActiveSafe(sim2ResultsSummaryText.gameObject, false);

        // Keep manual scene sizing/layout, but enforce tab-specific visibility.
        if (!splitColumns)
            return;

        bool showResult = activeTab == ResultsTab.Result;
        bool showRecommendations = activeTab == ResultsTab.Recommendations;

        if (sim2ResultsMetricsText != null)
            SetActiveSafe(sim2ResultsMetricsText.gameObject, showResult);
        if (sim2ResultsRecommendationsText != null)
            SetActiveSafe(sim2ResultsRecommendationsText.gameObject, showRecommendations);
    }
}
