using System.Security.Cryptography;
using System.Text;

public static class PasswordUtil
{
    public static string HashPassword(string plain)
    {
        if (string.IsNullOrEmpty(plain))
            return string.Empty;

        using (var sha = SHA256.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(plain);
            byte[] hash = sha.ComputeHash(bytes);

            StringBuilder sb = new StringBuilder();
            foreach (var b in hash)
                sb.Append(b.ToString("x2")); // 16진수 문자열

            return sb.ToString();
        }
    }
}