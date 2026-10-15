using System.Net.Mail;
using ErrorOr;
using Faber.Modules.Communication.PublicApi;
using Faber.Modules.Documents.PublicApi;
using Faber.Modules.Notifications.PublicApi;
using Faber.Modules.Notifications.PublicApi.Contracts;
using FluentEmail.Core;
using FluentEmail.Core.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Notifications.Application.Email;

/// <summary>
/// Sends email via SMTP through FluentEmail, used for the Development environment. Internal because it
/// must only ever be reached through the <see cref="IEmailSender"/> decorator chain built by
/// <see cref="DependencyInjection.AddNotificationsModule"/> — anything injecting this type directly
/// instead of <see cref="IEmailSender"/> would bypass the per-recipient throttle in
/// <see cref="ThrottledEmailSender"/>.
/// </summary>
internal sealed class FluentEmailSender : IEmailSender
{
    private readonly IFluentEmail _fluentEmail;
    private readonly ILogger<FluentEmailSender> _logger;
    private readonly IDocumentsModuleApi _documentsModuleApi;

    public FluentEmailSender(
        IFluentEmail fluentEmail,
        ILogger<FluentEmailSender> logger,
        IDocumentsModuleApi documentsModuleApi)
    {
        _fluentEmail = fluentEmail;
        _logger = logger;
        _documentsModuleApi = documentsModuleApi;
    }

    public async Task<ErrorOr<Success>> SendAsync<TComponent>(
        EmailSenderRequest request,
        CancellationToken cancellationToken = default)
        where TComponent : IComponent
    {
        var messageBody = await _documentsModuleApi.RenderToHtmlAsync<TComponent>(request.Parameters, cancellationToken);

        var email = _fluentEmail
            .To(request.To)
            .Subject(request.Subject)
            .Body(messageBody, request.IsHtml);

        SendResponse result;

        try
        {
            result = await email.SendAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is SmtpException or IOException)
        {
            // SmtpSender reports a transport failure (refused connection, dropped socket, rejected
            // recipient) by throwing rather than through SendResponse. Left uncaught it escapes the
            // event handler and fails the request that published the event, even though that request's
            // own work is already done. Cancellation is not a delivery failure and is left to propagate.
            _logger.LogWarning(exception, "Failed to send email via SMTP");

            return EmailSendErrors.DeliveryFailed(exception.Message);
        }

        if (!result.Successful)
        {
            var errors = string.Join(", ", result.ErrorMessages);
            _logger.LogWarning("Failed to send email: {Errors}", errors);

            return EmailSendErrors.DeliveryFailed(errors);
        }

        return Result.Success;
    }
}