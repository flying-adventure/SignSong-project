using System.IO;
using UnityEngine;
using System.Linq;

public static class UserDatabase
{
    private static UserDatabaseData _data;
    private static bool _loaded = false;

    private static string FilePath =>
        Path.Combine(Application.persistentDataPath, "users.json");

    // 앱 시작 시 한 번만 호출해두면 좋음
    public static void Load()
    {
        if (_loaded) return;

        if (File.Exists(FilePath))
        {
            string json = File.ReadAllText(FilePath);
            _data = JsonUtility.FromJson<UserDatabaseData>(json);
            if (_data == null) _data = new UserDatabaseData();
        }
        else
        {
            _data = new UserDatabaseData();
        }

        _loaded = true;
        Debug.Log($"[UserDatabase] Loaded. users count = {_data.users.Count}");
    }

    private static void Save()
    {
        string json = JsonUtility.ToJson(_data, true);
        File.WriteAllText(FilePath, json);
        Debug.Log("[UserDatabase] Saved to " + FilePath);
    }

    // 회원가입
    public static bool Register(string username, string password, out string message)
    {
        Load();

        username = username.Trim();

        if (string.IsNullOrEmpty(username))
        {
            message = "Enter ID pls.";
            return false;
        }

        if (string.IsNullOrEmpty(password))
        {
            message = "Enter password.";
            return false;
        }

        if (_data.users.Any(u => u.username == username))
        {
            message = "ID already exists.";
            return false;
        }

        var record = new UserRecord
        {
            username = username,
            passwordHash = PasswordUtil.HashPassword(password),
            createdAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        _data.users.Add(record);
        Save();

        message = "SignUp Done!";
        return true;
    }

    // 로그인 검증
    public static bool ValidateLogin(string username, string password, out string message)
    {
        Load();

        username = username.Trim();

        var user = _data.users.FirstOrDefault(u => u.username == username);
        if (user == null)
        {
            message = "this id does not exist.";
            return false;
        }

        string hash = PasswordUtil.HashPassword(password);
        if (user.passwordHash != hash)
        {
            message = "wrong password.";
            return false;
        }

        message = "login successful.";
        return true;
    }
}