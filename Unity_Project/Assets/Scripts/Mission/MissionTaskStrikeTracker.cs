using System;
using UnityEngine;

/// <summary>
/// Per-task time limits during active simulations. Exceeding a task limit counts as one strike;
/// after <see cref="maxStrikes"/> the run ends as disqualified.
/// </summary>
public class MissionTaskStrikeTracker : MonoBehaviour
{
    [Serializable]
    public class TaskTimeLimit
    {
        public string taskKey;
        public string displayName;
        public float limitSeconds = 60f;
    }

    [Header("Rules")]
    public bool trackingEnabled = true;
    public int maxStrikes = 3;

    [Header("Simulation 1 task limits (seconds per step)")]
    public TaskTimeLimit[] sim1TaskLimits =
    {
        new TaskTimeLimit { taskKey = "sim1_collect", displayName = "Collect supplies", limitSeconds = 180f },
        new TaskTimeLimit { taskKey = "sim1_lights", displayName = "Turn off lights", limitSeconds = 50f },
        new TaskTimeLimit { taskKey = "sim1_door", displayName = "Close entrance door", limitSeconds = 45f },
        new TaskTimeLimit { taskKey = "sim1_shelter", displayName = "Reach Mamad shelter", limitSeconds = 90f },
    };

    [Header("Simulation 2 task limits (seconds per step)")]
    public TaskTimeLimit[] sim2TaskLimits =
    {
        new TaskTimeLimit { taskKey = "sim2_kit", displayName = "Collect first aid kit", limitSeconds = 120f },
        new TaskTimeLimit { taskKey = "sim2_contact", displayName = "Contact casualty", limitSeconds = 60f },
        new TaskTimeLimit { taskKey = "sim2_phone_door", displayName = "Open phone booth door", limitSeconds = 30f },
        new TaskTimeLimit { taskKey = "sim2_phone_coin", displayName = "Insert coin", limitSeconds = 20f },
        new TaskTimeLimit { taskKey = "sim2_phone_handset", displayName = "Lift receiver", limitSeconds = 20f },
        new TaskTimeLimit { taskKey = "sim2_phone_dial", displayName = "Dial 101", limitSeconds = 45f },
        new TaskTimeLimit { taskKey = "sim2_treatment", displayName = "Treat casualty", limitSeconds = 120f },
    };

    public int StrikeCount => _strikeCount;
    public int MaxStrikes => maxStrikes;
    public string LastStrikeTaskDisplayName => _lastStrikeTaskDisplayName;
    public bool IsTrackingActive => _active;
    public string CurrentTaskKey => _currentTaskKey;
    public string CurrentTaskDisplayName => GetDisplayName(_currentTaskKey);
    public float CurrentTaskElapsedSeconds => _taskElapsedSeconds;
    public float CurrentTaskLimitSeconds => GetLimitSecondsForTask(_currentTaskKey);
    public float CurrentTaskRemainingSeconds =>
        Mathf.Max(0f, CurrentTaskLimitSeconds - CurrentTaskElapsedSeconds);

    GameManager _gameManager;
    TrainingFlowController _flow;
    bool _trackingSim2;
    bool _active;
    int _strikeCount;
    string _lastStrikeTaskDisplayName;
    string _currentTaskKey;
    float _taskElapsedSeconds;

    public void BeginTracking(bool simulation2)
    {
        _gameManager ??= GetComponent<GameManager>();
        if (_gameManager == null)
            _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

        _flow ??= FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
        _trackingSim2 = simulation2;
        _active = trackingEnabled && _gameManager != null && _flow != null;
        _strikeCount = 0;
        _lastStrikeTaskDisplayName = string.Empty;
        _currentTaskKey = string.Empty;
        _taskElapsedSeconds = 0f;
    }

    public void EndTracking()
    {
        _active = false;
    }

    public string GetDisqualificationSummary()
    {
        if (_strikeCount < maxStrikes)
            return string.Empty;

        string taskPart = string.IsNullOrEmpty(_lastStrikeTaskDisplayName)
            ? "task time limits"
            : _lastStrikeTaskDisplayName;

        return $"Disqualified after {maxStrikes} task time violations (last: {taskPart}).";
    }

    public float GetLimitSecondsForTask(string taskKey)
    {
        if (string.IsNullOrEmpty(taskKey))
            return 0f;

        var limits = _trackingSim2 ? sim2TaskLimits : sim1TaskLimits;
        if (limits == null)
            return 0f;

        for (int i = 0; i < limits.Length; i++)
        {
            if (limits[i] != null && limits[i].taskKey == taskKey)
                return Mathf.Max(1f, limits[i].limitSeconds);
        }

        return 0f;
    }

    void Update()
    {
        if (!_active || _gameManager == null || _flow == null)
            return;

        if (!_flow.AllowsMissionGameplay || _flow.IsPaused)
            return;

        string taskKey = _gameManager.GetCurrentMissionTaskKey();
        if (string.IsNullOrEmpty(taskKey))
            return;

        if (taskKey != _currentTaskKey)
        {
            _currentTaskKey = taskKey;
            _taskElapsedSeconds = 0f;
        }

        float limit = GetLimitSecondsForTask(taskKey);
        if (limit <= 0f)
            return;

        _taskElapsedSeconds += Time.deltaTime;
        if (_taskElapsedSeconds < limit)
            return;

        RegisterStrike(taskKey);
    }

    void RegisterStrike(string taskKey)
    {
        _taskElapsedSeconds = 0f;
        _strikeCount++;
        _lastStrikeTaskDisplayName = GetDisplayName(taskKey);

        if (_gameManager != null)
        {
            _gameManager.ShowTransientMissionNote(
                $"Task time limit exceeded — strike {_strikeCount}/{maxStrikes} ({_lastStrikeTaskDisplayName}).",
                5f);
        }

        if (_strikeCount >= maxStrikes)
        {
            _active = false;
            if (_trackingSim2)
                _flow.FinishSim2Disqualified(_lastStrikeTaskDisplayName);
            else
                _flow.FinishSim1Disqualified(_lastStrikeTaskDisplayName);
        }
    }

    string GetDisplayName(string taskKey)
    {
        var limits = _trackingSim2 ? sim2TaskLimits : sim1TaskLimits;
        if (limits == null)
            return taskKey;

        for (int i = 0; i < limits.Length; i++)
        {
            if (limits[i] != null && limits[i].taskKey == taskKey)
                return limits[i].displayName;
        }

        return taskKey;
    }
}
