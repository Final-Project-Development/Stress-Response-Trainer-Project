using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Simulation 2 — Kibbutz courtyard first aid under ongoing alarm. Uses persisted physiology from scene 0.
/// </summary>
public class Simulation2SceneController : MonoBehaviour
{
    [Header("Optional UI")]
    public TextMeshProUGUI statusText;
    public SimpleStressLineGraph hrvResultsGraph;
    public float hrvGraphMaxDisplay = 100f;

    [Header("Flow")]
    public int hubSceneBuildIndex;

    [Header("Player start (this scene only)")]
    [Tooltip("Drag an empty GameObject where the player should stand. If empty, enable World Spawn below.")]
    public Transform playerSpawnPoint;
    [Tooltip("Use world position when Player Spawn Point is not assigned.")]
    public bool playerSpawnUseWorldCoordinates;
    public Vector3 playerSpawnWorldPosition;
    public Vector3 playerSpawnWorldEuler;

    private MockPhysiologySource _physiology;
    private SessionStressRecorder _recorder;
    private GameManager _gameManager;

    void Start()
    {
        _physiology = FindFirstObjectByType<MockPhysiologySource>();
        _recorder = FindFirstObjectByType<SessionStressRecorder>();
        _gameManager = FindFirstObjectByType<GameManager>();

        ApplyPlayerSpawn();

        var alarm = GameObject.Find("MissleAlarm")?.GetComponent<AudioSource>();
        if (alarm != null)
        {
            alarm.loop = true;
            if (!alarm.isPlaying)
                alarm.Play();
        }

        if (_physiology != null)
            _physiology.StressorActive = true;

        _recorder?.Clear();
        _recorder?.BeginRecording();

        if (_gameManager != null)
            _gameManager.OnFirstAidComplete += OnFirstAidDone;

        if (statusText != null)
        {
            statusText.text =
                "Simulation 2: First aid in the open courtyard.\n" +
                "Rolling-event background audio may continue.\n" +
                "Approach the casualty, press E to start treatment, then complete steps with keys 1 -> 2 -> 3.\n" +
                "Press B to return to the hub after you finish.";
        }
    }

    void OnDestroy()
    {
        if (_gameManager != null)
            _gameManager.OnFirstAidComplete -= OnFirstAidDone;
    }

    void Update()
    {
        if (_physiology != null && _recorder != null && _physiology.BaselineLocked)
        {
            float sci = StressChangeIndexCalculator.ComputeSciPercent(_physiology.HrvBaselineMs, _physiology.CurrentHrvMs);
            _recorder.TickRecord(sci, _physiology.CurrentHrvMs);
        }

        if (Input.GetKeyDown(KeyCode.B))
            ReturnToHub();
    }

    private void OnFirstAidDone()
    {
        if (_physiology != null)
            _physiology.StressorActive = false;

        var alarm = GameObject.Find("MissleAlarm")?.GetComponent<AudioSource>();
        if (alarm != null)
            alarm.Stop();

        _recorder?.EndRecording();

        float peak = 0f;
        float mean = 0f;
        if (_recorder != null && _recorder.SciHistory.Count > 0)
        {
            float sum = 0f;
            foreach (float s in _recorder.SciHistory)
            {
                sum += s;
                if (s > peak) peak = s;
            }
            mean = sum / _recorder.SciHistory.Count;
            SessionHistoryStore.FinalizeAfterSim2(_recorder.SciHistory, _recorder.sampleIntervalSeconds);
        }

        string recovery = SessionHistoryStore.BuildPhysiologicalRecoverySummary(peak, mean);
        string tips = _recorder != null && _recorder.SciHistory.Count > 0
            ? StressRecommendations.BuildFromSciHistory(_recorder.SciHistory)
            : string.Empty;

        if (statusText != null)
        {
            statusText.text =
                "First aid complete.\n\n" +
                recovery +
                (string.IsNullOrEmpty(tips) ? string.Empty : "\n\n" + tips) +
                "\n\nPress B to return to the hub (menu).";
        }

        if (hrvResultsGraph != null && _recorder != null && _recorder.HrvHistory.Count > 0)
            hrvResultsGraph.SetFromValues(_recorder.HrvHistory, hrvGraphMaxDisplay);
    }

    public void ReturnToHub()
    {
        if (hubSceneBuildIndex >= 0 && hubSceneBuildIndex < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(hubSceneBuildIndex);
    }

    private void ApplyPlayerSpawn()
    {
        if (playerSpawnPoint == null && !playerSpawnUseWorldCoordinates)
            return;

        var fps = FindFirstObjectByType<SimpleFPSController>();
        if (fps == null) return;

        Vector3 pos;
        Quaternion rot;
        if (playerSpawnPoint != null)
        {
            pos = playerSpawnPoint.position;
            rot = playerSpawnPoint.rotation;
        }
        else
        {
            pos = playerSpawnWorldPosition;
            rot = Quaternion.Euler(playerSpawnWorldEuler);
        }

        fps.TeleportTo(pos, rot);
    }
}
