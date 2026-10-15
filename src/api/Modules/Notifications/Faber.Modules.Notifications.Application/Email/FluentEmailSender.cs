using System.Net.Mail;
using ErrorOr;
using Faber.Modules.Communication.PublicApi;
using Faber.Modules.Documents.PublicApi;
using Faber.Modules.Notifications.PublicApi;
using Faber.Modules.Notifications.PublicApi.Contracts;
using Faber.Modules.Notifications.PublicApi.Options;
using FluentEmail.Core.Interfaces;
using FluentEmail.Core.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly ISender _sender;
    private readonly ILogger<FluentEmailSender> _logger;
    private readonly IDocumentsModuleApi _documentsModuleApi;

    public FluentEmailSender(
        ISender sender,
        IOptions<FluentEmailOptions> options,
        ILogger<FluentEmailSender> logger,
        IDocumentsModuleApi documentsModuleApi)
    {
        _sender = sender;
        _logger = logger;
        _documentsModuleApi = documentsModuleApi;
        _fromEmail = options.Value.FromEmail;
        _fromName = options.Value.FromName;
    }

    public async Task<ErrorOr<Success>> SendAsync<TComponent>(
        EmailSenderRequest request,
        CancellationToken cancellationToken = default)
        where TComponent : IComponent
    {
        var messageBody = await _documentsModuleApi.RenderToHtmlAsync<TComponent>(request.Parameters, cancellationToken);

        // A new message per send: FluentEmail's Email is a mutable builder, and FastEndpoints keeps event
        // handlers — and with them this sender — alive across requests. Reusing one instance appended
        // every recipient to the same To list, so each verification link also went to all earlier ones.
        var email = new FluentEmail.Core.Email(renderer: null, _sender, _fromEmail, _fromName)
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