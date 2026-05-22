using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PrediCop.Core.Interfaces;

namespace PrediCop.Infrastructure.Services;

public class FcmPushNotificationService(IConfiguration configuration, ILogger<FcmPushNotificationService> logger)
    : IPushNotificationService
{
    private static readonly object _initLock = new();
    private static bool _initialized;
    private bool _configured;

    private FirebaseMessaging? GetMessaging()
    {
        if (_configured)
            return FirebaseMessaging.DefaultInstance;

        var settings = configuration.GetSection("FirebaseSettings");
        var serviceAccountPath = settings["ServiceAccountJsonPath"];
        var projectId = settings["ProjectId"];

        if (string.IsNullOrWhiteSpace(serviceAccountPath) || string.IsNullOrWhiteSpace(projectId))
        {
            logger.LogWarning("FirebaseSettings:ServiceAccountJsonPath ou ProjectId non configuré — push notifications désactivées.");
            return null;
        }

        lock (_initLock)
        {
            if (!_initialized)
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromFile(serviceAccountPath),
                    ProjectId = projectId
                });
                _initialized = true;
            }
        }

        _configured = true;
        return FirebaseMessaging.DefaultInstance;
    }

    public async Task SendToDeviceAsync(string deviceToken, string title, string body,
        Dictionary<string, string>? data = null, CancellationToken ct = default)
    {
        var messaging = GetMessaging();
        if (messaging is null) return;

        var message = new Message
        {
            Token = deviceToken,
            Notification = new Notification { Title = title, Body = body },
            Data = data ?? new Dictionary<string, string>()
        };

        try
        {
            var messageId = await messaging.SendAsync(message, ct);
            logger.LogDebug("Push envoyé → {MessageId}", messageId);
        }
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
        {
            logger.LogWarning("Token FCM non enregistré (expiré/révoqué): {Token}", deviceToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de l'envoi du push au token {Token}", deviceToken);
        }
    }

    public async Task SendToDevicesAsync(IEnumerable<string> deviceTokens, string title, string body,
        Dictionary<string, string>? data = null, CancellationToken ct = default)
    {
        var messaging = GetMessaging();
        if (messaging is null) return;

        var tokens = deviceTokens.ToList();
        if (tokens.Count == 0) return;

        if (tokens.Count == 1)
        {
            await SendToDeviceAsync(tokens[0], title, body, data, ct);
            return;
        }

        // MulticastMessage supporte jusqu'à 500 tokens
        const int batchSize = 500;
        for (var i = 0; i < tokens.Count; i += batchSize)
        {
            var batch = tokens.Skip(i).Take(batchSize).ToList();

            var multicast = new MulticastMessage
            {
                Tokens = batch,
                Notification = new Notification { Title = title, Body = body },
                Data = data ?? new Dictionary<string, string>()
            };

            try
            {
                var response = await messaging.SendEachForMulticastAsync(multicast, ct);
                logger.LogDebug("Push multicast: {Success}/{Total} succès", response.SuccessCount, batch.Count);

                for (var j = 0; j < response.Responses.Count; j++)
                {
                    var r = response.Responses[j];
                    if (!r.IsSuccess &&
                        r.Exception?.MessagingErrorCode == MessagingErrorCode.Unregistered)
                    {
                        logger.LogWarning("Token FCM non enregistré (expiré/révoqué): {Token}", batch[j]);
                    }
                    else if (!r.IsSuccess)
                    {
                        logger.LogError(r.Exception, "Échec push pour le token {Token}", batch[j]);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors de l'envoi multicast (batch {BatchIndex})", i / batchSize);
            }
        }
    }
}
