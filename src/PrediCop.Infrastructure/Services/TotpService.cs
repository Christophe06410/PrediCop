using System.Text;
using System.Text.Json;
using OtpNet;
using PrediCop.Core.Interfaces;

namespace PrediCop.Infrastructure.Services;

public class TotpService : ITotpService
{
    private const int RecoveryCodeCount = 8;
    private const int RecoveryCodeLength = 8;
    private const string RecoveryCodeChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    public string GenerateQrCodeUri(string email, string tenantName, string secret)
    {
        var issuer = Uri.EscapeDataString($"PrediCop ({tenantName})");
        var account = Uri.EscapeDataString(email);
        var encodedSecret = Uri.EscapeDataString(secret);
        return $"otpauth://totp/{issuer}:{account}?secret={encodedSecret}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";
    }

    public bool VerifyCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
            return false;

        try
        {
            var keyBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(keyBytes);
            return totp.VerifyTotp(DateTime.UtcNow, code, out _, new VerificationWindow(1, 1));
        }
        catch
        {
            return false;
        }
    }

    public List<string> GenerateRecoveryCodes()
    {
        var codes = new List<string>(RecoveryCodeCount);
        var random = new Random();
        for (int i = 0; i < RecoveryCodeCount; i++)
        {
            var sb = new StringBuilder(RecoveryCodeLength);
            for (int j = 0; j < RecoveryCodeLength; j++)
                sb.Append(RecoveryCodeChars[random.Next(RecoveryCodeChars.Length)]);
            codes.Add(sb.ToString());
        }
        return codes;
    }

    public (bool Valid, string UpdatedJson) VerifyRecoveryCode(string json, string code)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(code))
            return (false, json);

        List<string>? codes;
        try
        {
            codes = JsonSerializer.Deserialize<List<string>>(json);
        }
        catch
        {
            return (false, json);
        }

        if (codes is null)
            return (false, json);

        var normalizedInput = code.Trim().ToUpperInvariant();
        var match = codes.FirstOrDefault(c => c.ToUpperInvariant() == normalizedInput);
        if (match is null)
            return (false, json);

        codes.Remove(match);
        return (true, JsonSerializer.Serialize(codes));
    }
}
