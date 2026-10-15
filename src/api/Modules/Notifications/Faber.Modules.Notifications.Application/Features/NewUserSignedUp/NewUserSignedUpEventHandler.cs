using Faber.Modules.Auth.PublicApi.Events;
using Faber.Modules.Communication.PublicApi;
using Faber.Modules.Notifications.PublicApi.Contracts;
using FastEndpoints;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Notifications.Application.Features.NewUserSignedUp;

public class NewUserSignedUpEventHandler(
    IEmailSender emailSender,
    IConfiguration configuration,
    ILogger<NewUserSignedUpEventHandler> logger)
    : IEventHandler<NewUserSignedUpEvent>
{
    public async Task HandleAsync(NewUserSignedUpEvent eventModel, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>
        {
            { "Host", configuration.GetConnectionString("FaberHost") },
            { "Key", eventModel.CombinedKey.Value }
        };

        var headers = new EmailSenderRequest(eventModel.Email, "Verify email", parameters);
        var result = await emailSender.SendAsync<NewUserSignedUpTemplate>(headers, cancellationToken);

        if (result.IsError)
        {
            // The publisher awaits this handler, but the event carries no result back: the user or reset
            // token already exists, so failing the request would only hide that it succeeded. The log is
            // the signal here — which is also why IEmailSender reports delivery failures instead of
            // throwing them.
            logger.LogWarning(
                "Verification email not sent: {ErrorCode}",
                result.FirstError.Code);
        }
    }
}
