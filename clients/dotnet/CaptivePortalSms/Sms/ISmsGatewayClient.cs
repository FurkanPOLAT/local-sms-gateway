namespace CaptivePortalSms.Sms;

/// <summary>
/// Android SMS Gateway'e SMS gonderen istemci. Gateway "aptal boru"dur;
/// mesaj metni (marka etiketi dahil) bu katmanda hazirlanir.
/// </summary>
public interface ISmsGatewayClient
{
    Task<SmsSendResult> SendAsync(string phone, string message, CancellationToken ct = default);
}
