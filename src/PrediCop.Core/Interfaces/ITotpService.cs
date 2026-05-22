namespace PrediCop.Core.Interfaces;

public interface ITotpService
{
    string GenerateSecret();
    string GenerateQrCodeUri(string email, string tenantName, string secret);
    bool VerifyCode(string secret, string code);
    List<string> GenerateRecoveryCodes();
    (bool Valid, string UpdatedJson) VerifyRecoveryCode(string json, string code);
}
