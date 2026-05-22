namespace PrediCop.Core.DTOs;

public record TotpSetupResponse(string SecretKey, string QrCodeUri, List<string> RecoveryCodes);

public record TotpEnableRequest(string Code);

public record TotpVerifyRequest(string TempToken, string Code);
