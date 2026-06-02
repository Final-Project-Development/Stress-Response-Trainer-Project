using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wire TMP inputs + buttons from the Login / Register UI to <see cref="LocalAuthStore"/> and <see cref="TrainingFlowController"/>.
/// </summary>
public class LoginFlowPanel : MonoBehaviour
{
    [Header("Training flow")]
    [SerializeField] private TrainingFlowController trainingFlow;

    [Header("Login form")]
    [SerializeField] private TMP_InputField loginEmail;
    [SerializeField] private TMP_InputField loginPassword;
    [SerializeField] private Button loginSubmitButton;
    [SerializeField] private Toggle rememberMeToggle;
    [SerializeField] private Button forgotPasswordButton;

    [Header("Register form")]
    [SerializeField] private TMP_InputField registerEmail;
    [SerializeField] private TMP_InputField registerPassword;
    [SerializeField] private TMP_InputField registerPasswordConfirm;
    [SerializeField] private Button registerSubmitButton;

    [Header("Navigation")]
    [SerializeField] private GameObject loginFormRoot;
    [SerializeField] private GameObject registerFormRoot;
    [SerializeField] private Button showRegisterButton;
    [SerializeField] private Button showLoginButton;
    [SerializeField] private Button cancelButton;

    [Header("Feedback")]
    [SerializeField] private TMP_Text statusText;

    void Awake()
    {
        if (trainingFlow == null)
            trainingFlow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        if (loginSubmitButton != null)
            loginSubmitButton.onClick.AddListener(OnLoginClicked);
        if (forgotPasswordButton != null)
            forgotPasswordButton.onClick.AddListener(OnForgotPasswordClicked);
        if (registerSubmitButton != null)
            registerSubmitButton.onClick.AddListener(OnRegisterClicked);
        if (showRegisterButton != null)
            showRegisterButton.onClick.AddListener(() => ShowRegister(true));
        if (showLoginButton != null)
            showLoginButton.onClick.AddListener(() => ShowRegister(false));
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);
    }

    void OnEnable()
    {
        ClearStatus();
        ShowRegister(false);
        PrefillEmail();
    }

    private void PrefillEmail()
    {
        string last = LocalAuthStore.GetLastLoggedInEmail();
        bool hasSaved = !string.IsNullOrEmpty(last);
        if (loginEmail != null)
            loginEmail.text = hasSaved ? last : "";
        if (rememberMeToggle != null)
            rememberMeToggle.isOn = hasSaved;
    }

    public void ShowRegister(bool register)
    {
        if (loginFormRoot != null)
            loginFormRoot.SetActive(!register);
        if (registerFormRoot != null)
            registerFormRoot.SetActive(register);
        ClearStatus();
    }

    private void OnLoginClicked()
    {
        string email = loginEmail != null ? loginEmail.text : "";
        string pass = loginPassword != null ? loginPassword.text : "";

        if (!LocalAuthStore.TryLogin(email, pass, out string err))
        {
            SetStatus(err, true);
            return;
        }

        if (rememberMeToggle != null && rememberMeToggle.isOn)
            LocalAuthStore.SetLastLoggedInEmail(email);
        else
            LocalAuthStore.ClearLastLoggedInEmail();

        SetStatus("", false);
        trainingFlow?.UI_CompleteLoginAndStartIntro();
    }

    private void OnForgotPasswordClicked()
    {
        string email = loginEmail != null ? loginEmail.text : "";
        if (LocalAuthStore.TryResetPassword(email, out string tempPass, out string err))
        {
            SetStatus($"Temporary password: {tempPass}", false);
            if (loginPassword != null)
                loginPassword.text = "";
        }
        else
        {
            SetStatus(err, true);
        }
    }

    private void OnRegisterClicked()
    {
        string email = registerEmail != null ? registerEmail.text : "";
        string pass = registerPassword != null ? registerPassword.text : "";
        string pass2 = registerPasswordConfirm != null ? registerPasswordConfirm.text : "";
        if (pass != pass2)
        {
            SetStatus("Passwords do not match.", true);
            return;
        }

        if (LocalAuthStore.TryRegister(email, pass, out string err))
        {
            SetStatus("Registration successful. You can sign in now.", false);
            ShowRegister(false);
            if (loginEmail != null) loginEmail.text = email;
            if (loginPassword != null) loginPassword.text = "";
        }
        else
            SetStatus(err, true);
    }

    private void OnCancelClicked()
    {
        trainingFlow?.UI_CancelLogin();
    }

    private void ClearStatus()
    {
        if (statusText != null)
            statusText.text = "";
    }

    private void SetStatus(string msg, bool isError)
    {
        if (statusText == null) return;
        statusText.text = msg;
        statusText.color = isError ? new Color(1f, 0.4f, 0.4f) : new Color(0.7f, 0.95f, 0.75f);
        if (statusText is TextMeshProUGUI tmp)
            tmp.isRightToLeftText = ContainsHebrew(msg);
    }

    private static bool ContainsHebrew(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (char c in s)
        {
            if (c >= '\u0590' && c <= '\u05FF')
                return true;
        }
        return false;
    }
}
