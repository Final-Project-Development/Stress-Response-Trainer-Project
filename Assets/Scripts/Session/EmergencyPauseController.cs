using UnityEngine;

/// <summary>
/// FR 9: emergency pause / exit. Default: Escape opens pause; second press or Resume closes.
/// </summary>
public class EmergencyPauseController : MonoBehaviour
{
    public GameObject pausePanel;
    public KeyCode toggleKey = KeyCode.Escape;

    private bool _paused;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            SetPaused(!_paused);
    }

    public void SetPaused(bool pause)
    {
        _paused = pause;
        Time.timeScale = pause ? 0f : 1f;
        if (pausePanel != null)
            pausePanel.SetActive(pause);
    }

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
}
