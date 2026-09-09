using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;

namespace Schuly.API.Services.Notifications
{
    public sealed class FirebasePushSender(FirebaseApp app, ILogger<FirebasePushSender> logger) : IPushSender
    {
        // FCM rejects a multicast with more than 500 tokens.
        private const int MaxTokensPerBatch = 500;

        public bool IsConfigured => true;

        public async Task<PushResult> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken cancellationToken = default)
        {
            var messaging = FirebaseMessaging.GetMessaging(app);
            var successCount = 0;
            var failureCount = 0;
            var unregisteredTokens = new List<string>();

            for (var offset = 0; offset < tokens.Count; offset += MaxTokensPerBatch)
            {
                var batch = tokens.Skip(offset).Take(MaxTokensPerBatch).ToList();

                // Tokens is deprecated in favour of Fids, but the two are different wire
                // fields ("token" vs "fid"): Fids expects Firebase installation IDs, while
                // the app registers FCM registration tokens. Sending those as Fids would
                // address nothing, so keep Tokens and silence the deprecation.
#pragma warning disable CS0618
                var multicastMessage = new MulticastMessage
                {
                    Tokens = batch,
                    Notification = new Notification { Title = message.Title, Body = message.Body },
                    Data = message.Data,
                };
#pragma warning restore CS0618

                var response = await messaging.SendEachForMulticastAsync(multicastMessage, cancellationToken);

                for (var i = 0; i < response.Responses.Count; i++)
                {
                    var sendResponse = response.Responses[i];
                    if (sendResponse.IsSuccess)
                    {
                        successCount++;
                        continue;
                    }

                    failureCount++;

                    if (IsDeadToken(sendResponse.Exception?.MessagingErrorCode))
                        unregisteredTokens.Add(batch[i]);
                    else
                        logger.LogWarning(sendResponse.Exception, "Push delivery failed for a device token");
                }
            }

            return new PushResult(successCount, failureCount, unregisteredTokens);
        }

        private static bool IsDeadToken(MessagingErrorCode? errorCode) =>
            errorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.SenderIdMismatch or MessagingErrorCode.InvalidArgument;
    }
}
