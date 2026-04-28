using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Local demo auth: stores accounts on disk (JSON). Not suitable for real security — use a backend for production.
/// </summary>
public static class LocalAuthStore
{
    private const string FileName = "local_auth_accounts.json";
    private const string CurrentLoggedInEmailKey = "local_auth_current_email";

    [Serializable]
    private class AccountDto
    {
        public string email;
        public string passwordHashHex;
    }

    [Serializable]
    private class FileDto
    {
        public List<AccountDto> accounts = new List<AccountDto>();
    }

    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>Returns whether an account exists for this email (after normalization).</summary>
    public static bool IsEmailRegistered(string email)
    {
        email = NormalizeEmail(email);
        if (string.IsNullOrEmpty(email))
            return false;
        return FindIndex(Load().accounts, email) >= 0;
    }

    public static bool TryRegister(string email, string password, out string error)
    {
        error = null;
        email = NormalizeEmail(email);
        if (string.IsNullOrEmpty(email))
        {
            error = "Please enter an email address.";
            return false;
        }

        if (string.IsNullOrEmpty(password) || password.Length < 4)
        {
            error = "Password must be at least 4 characters.";
            return false;
        }

        var data = Load();
        if (FindIndex(data.accounts, email) >= 0)
        {
            error = "This email is already registered.";
            return false;
        }

        data.accounts.Add(new AccountDto
        {
            email = email,
            passwordHashHex = HashPassword(email, password)
        });
        Save(data);
        return true;
    }

    public static bool TryLogin(string email, string password, out string error)
    {
        error = null;
        email = NormalizeEmail(email);
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            error = "Please enter email and password.";
            return false;
        }

        var data = Load();
        int i = FindIndex(data.accounts, email);
        if (i < 0)
        {
            error = "User not registered. Please register before signing in.";
            return false;
        }

        string h = HashPassword(email, password);
        if (data.accounts[i].passwordHashHex != h)
        {
            error = "Wrong password.";
            return false;
        }
        PlayerPrefs.SetString(CurrentLoggedInEmailKey, email);
        PlayerPrefs.Save();
        return true;
    }

    public static void SetLastLoggedInEmail(string email)
    {
        email = NormalizeEmail(email);
        PlayerPrefs.SetString("local_auth_last_email", email);
        PlayerPrefs.Save();
    }

    public static void ClearLastLoggedInEmail()
    {
        PlayerPrefs.DeleteKey("local_auth_last_email");
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Demo-only password recovery: replaces password with a temporary one and returns it to UI.
    /// In production this should be handled by a backend/email flow.
    /// </summary>
    public static bool TryResetPassword(string email, out string temporaryPassword, out string error)
    {
        temporaryPassword = null;
        error = null;
        email = NormalizeEmail(email);

        if (string.IsNullOrEmpty(email))
        {
            error = "Please enter your email first.";
            return false;
        }

        var data = Load();
        int i = FindIndex(data.accounts, email);
        if (i < 0)
        {
            error = "User not registered.";
            return false;
        }

        temporaryPassword = GenerateTemporaryPassword();
        data.accounts[i].passwordHashHex = HashPassword(email, temporaryPassword);
        Save(data);
        return true;
    }

    public static string GetLastLoggedInEmail()
    {
        return PlayerPrefs.GetString("local_auth_last_email", "");
    }

    public static string GetCurrentLoggedInEmail()
    {
        return PlayerPrefs.GetString(CurrentLoggedInEmailKey, "");
    }

    public static void Logout()
    {
        PlayerPrefs.DeleteKey(CurrentLoggedInEmailKey);
        PlayerPrefs.DeleteKey("local_auth_last_email");
        PlayerPrefs.Save();
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "";
        return email.Trim().ToLowerInvariant();
    }

    private static int FindIndex(List<AccountDto> list, string email)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].email == email)
                return i;
        }

        return -1;
    }

    private static FileDto Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new FileDto();
            string json = File.ReadAllText(FilePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                return new FileDto();
            var dto = JsonUtility.FromJson<FileDto>(json);
            return dto ?? new FileDto();
        }
        catch (Exception e)
        {
            Debug.LogWarning("LocalAuthStore load failed: " + e.Message);
            return new FileDto();
        }
    }

    private static void Save(FileDto data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(FilePath, json, Encoding.UTF8);
    }

    private static string HashPassword(string email, string password)
    {
        string salted = email + "::" + password + "::local_auth_v1";
        using (var sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(salted));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        const int len = 8;
        var sb = new StringBuilder(len);
        byte[] bytes = new byte[len];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        for (int i = 0; i < len; i++)
            sb.Append(chars[bytes[i] % chars.Length]);
        return sb.ToString();
    }
}
