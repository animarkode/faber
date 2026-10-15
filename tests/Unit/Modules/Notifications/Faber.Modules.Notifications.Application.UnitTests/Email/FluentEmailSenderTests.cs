using System.Net.Mail;
using ErrorOr;
using Faber.Modules.Documents.PublicApi;
using Faber.Modules.Notifications.Application.Email;
using Faber.Modules.Notifications.PublicApi.Contracts;
using FluentEmail.Core;
using FluentEmail.Core.Interfaces;
using FluentEmail.Core.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Faber.Modules.Notifications.Application.UnitTests.Email;

public class FluentEmailSenderTests
{
    private const string Recipient = "recipient@example.com";
    private const string DeliveryFailedCode = "Notifications.DeliveryFailed";

    /// <summary>Minimal Razor component stand-in; rendering is stubbed, only the type is forwarded.</summary>
    private sealed class TestTemplate : IComponent
    {
        public void Attach(RenderHandle renderHandle)
        {
        }

        public Task SetParametersAsync(ParameterView parameters) => Task.CompletedTask;
    }

    /// <summary>
    /// Builds the sender over a real FluentEmail <see cref="FluentEmail.Core.Email"/>, so only the SMTP
    /// transport (<see cref="ISender"/>) is substituted — the same seam SmtpSender plugs into at runtime.
    /// </summary>
    private static FluentEmailSender CreateSender(ISender transport)
    {
        var documentsModuleApi = Substitute.For<IDocumentsModuleApi>();

        documentsModuleApi
            .RenderToHtmlAsync<TestTemplate>(Arg.Any<Dictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns("<p>body</p>");

        var fluentEmail = new FluentEmail.Core.Email(null, transport, "no-reply@faber.test", "Faber");

        return new FluentEmailSender(fluentEmail, NullLogger<FluentEmailSender>.Instance, documentsModuleApi);
    }

    private static Task<ErrorOr<Success>> Send(FluentEmailSender sender)
    {
        var request = new EmailSenderRequest(Recipient, "Verify email", new Dictionary<string, object?>());

        return sender.SendAsync<TestTemplate>(request, CancellationToken.None);
    }

    [Fact]
    public async Task BrokenPipeSmtpFailure_ShouldMapToDeliveryFailed()
    {
        // The local sign-up failure: SmtpSender throws instead of returning an unsuccessful
        // SendResponse when the socket is gone, and before the fix that exception escaped the
        // awaited NewUserSignedUpEvent handler and turned a completed sign-up into a 500.
        var transport = Substitute.For<ISender>();

        transport
            .SendAsync(Arg.Any<IFluentEmail>(), Arg.Any<CancellationToken?>())
            .ThrowsAsync(new SmtpException("Failure sending mail.", new IOException("Broken pipe")));

        var result = await Send(CreateSender(transport));

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(DeliveryFailedCode);
    }

    [Fact]
    public async Task UnwrappedIOFailure_ShouldMapToDeliveryFailed()
    {
        var transport = Substitute.For<ISender>();

        transport
            .SendAsync(Arg.Any<IFluentEmail>(), Arg.Any<CancellationToken?>())
            .ThrowsAsync(new IOException("Connection reset by peer"));

        var result = await Send(CreateSender(transport));

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(DeliveryFailedCode);
    }

    [Fact]
    public async Task UnsuccessfulSendResponse_ShouldMapToDeliveryFailed()
    {
        var transport = Substitute.For<ISender>();

        transport
            .SendAsync(Arg.Any<IFluentEmail>(), Arg.Any<CancellationToken?>())
            .Returns(new SendResponse { ErrorMessages = ["Mailbox unavailable"] });

        var result = await Send(CreateSender(transport));

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(DeliveryFailedCode);
    }

    [Fact]
    public async Task AcceptedSend_ShouldReturnSuccess()
    {
        var transport = Substitute.For<ISender>();

        transport
            .SendAsync(Arg.Any<IFluentEmail>(), Arg.Any<CancellationToken?>())
            .Returns(new SendResponse());

        var result = await Send(CreateSender(transport));

        result.IsError.ShouldBeFalse();

        await transport.Received(1).SendAsync(
            Arg.Is<IFluentEmail>(email => email.Data.ToAddresses.Single().EmailAddress == Recipient),
            Arg.Any<CancellationToken?>());
    }

    [Fact]
    public async Task CancelledSend_ShouldPropagateCancellation()
    {
        // A caller walking away is not a delivery failure; widening the catch to Exception would
        // report a rejected message here and hide shutdown-time cancellation behind a warning.
        var transport = Substitute.For<ISender>();

        transport
            .SendAsync(Arg.Any<IFluentEmail>(), Arg.Any<CancellationToken?>())
            .ThrowsAsync(new TaskCanceledException());

        await Should.ThrowAsync<TaskCanceledException>(() => Send(CreateSender(transport)));
    }
}
