using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelController : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private Slider soundSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private AudioSource musicSource;

    [Header("Language")]
    [SerializeField] private TMP_Dropdown languageDropdown;

    [Header("Notification")]
    [SerializeField] private Button notificationOnButton;
    [SerializeField] private Button notificationOffButton;

    [Header("Vibration")]
    [SerializeField] private Button vibrationOnButton;
    [SerializeField] private Button vibrationOffButton;

    [Header("Panels")]
    [SerializeField] private GameObject aboutPanel;

    [Header("Actions")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button aboutButton;

    private const string KeySound = "settings.sound";
    private const string KeyMusic = "settings.music";
    private const string KeyLanguage = "settings.language";
    private const string KeyNotification = "settings.notification";
    private const string KeyVibration = "settings.vibration";

    private void Awake()
    {
        ForceEnglishOnlyLanguageOption();
        LoadValues();
        WireUi();
        ApplyVisualStates();
    }

    private void OnDisable()
    {
        PlayerPrefs.Save();
    }

    private void LoadValues()
    {
        float sound = PlayerPrefs.GetFloat(KeySound, 1f);
        float music = PlayerPrefs.GetFloat(KeyMusic, 1f);
        int language = PlayerPrefs.GetInt(KeyLanguage, 0);

        ApplySound(sound);
        ApplyMusic(music);

        if (soundSlider != null)
            soundSlider.SetValueWithoutNotify(sound);
        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(music);

        if (languageDropdown != null)
        {
            int clamped = Mathf.Clamp(language, 0, Mathf.Max(0, languageDropdown.options.Count - 1));
            languageDropdown.SetValueWithoutNotify(clamped);
        }
    }

    private void WireUi()
    {
        if (soundSlider != null)
            soundSlider.onValueChanged.AddListener(OnSoundChanged);

        if (musicSlider != null)
            musicSlider.onValueChanged.AddListener(OnMusicChanged);

        if (languageDropdown != null)
            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);

        if (notificationOnButton != null)
            notificationOnButton.onClick.AddListener(() => SetNotificationEnabled(true));
        if (notificationOffButton != null)
            notificationOffButton.onClick.AddListener(() => SetNotificationEnabled(false));

        if (vibrationOnButton != null)
            vibrationOnButton.onClick.AddListener(() => SetVibrationEnabled(true));
        if (vibrationOffButton != null)
            vibrationOffButton.onClick.AddListener(() => SetVibrationEnabled(false));

        if (closeButton != null)
            closeButton.onClick.AddListener(HideSelf);

        if (aboutButton != null)
            aboutButton.onClick.AddListener(ToggleAbout);
    }

    private void OnSoundChanged(float value)
    {
        ApplySound(value);
        PlayerPrefs.SetFloat(KeySound, value);
    }

    private void OnMusicChanged(float value)
    {
        ApplyMusic(value);
        PlayerPrefs.SetFloat(KeyMusic, value);
    }

    private void OnLanguageChanged(int index)
    {
        // Single-language build: force English only.
        PlayerPrefs.SetInt(KeyLanguage, 0);
        // Hook your localization system here if/when you add one.
    }

    private void ForceEnglishOnlyLanguageOption()
    {
        if (languageDropdown == null)
            return;

        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(new System.Collections.Generic.List<string> { "English" });
        languageDropdown.SetValueWithoutNotify(0);
        languageDropdown.interactable = false;
    }

    private void ApplySound(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
    }

    private void ApplyMusic(float value)
    {
        if (musicSource != null)
            musicSource.volume = Mathf.Clamp01(value);
    }

    private void SetNotificationEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(KeyNotification, enabled ? 1 : 0);
        ApplyVisualStates();
    }

    private void SetVibrationEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(KeyVibration, enabled ? 1 : 0);
        ApplyVisualStates();
    }

    private void ApplyVisualStates()
    {
        bool notificationEnabled = PlayerPrefs.GetInt(KeyNotification, 1) == 1;
        bool vibrationEnabled = PlayerPrefs.GetInt(KeyVibration, 1) == 1;

        SetButtonInteractivity(notificationOnButton, !notificationEnabled);
        SetButtonInteractivity(notificationOffButton, notificationEnabled);

        SetButtonInteractivity(vibrationOnButton, !vibrationEnabled);
        SetButtonInteractivity(vibrationOffButton, vibrationEnabled);
    }

    private static void SetButtonInteractivity(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    public void ShowSelf()
    {
        gameObject.SetActive(true);
        ApplyVisualStates();
    }

    public void HideSelf()
    {
        if (aboutPanel != null)
            aboutPanel.SetActive(false);
        gameObject.SetActive(false);
    }

    public void ToggleAbout()
    {
        if (aboutPanel != null)
            aboutPanel.SetActive(!aboutPanel.activeSelf);
    }
}
