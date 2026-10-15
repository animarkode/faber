using Faber.Modules.Communication.PublicApi;
using Faber.Modules.Documents.PublicApi;
using Faber.Modules.Notifications.Application.Email;
using Faber.Modules.Vault.PublicApi;
using FluentEmail.Core.Interfaces;
using FluentEmail.Smtp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Faber.Modules.Notifications.Application.UnitTests;

/// <summary>
/// Regression guard for issue #339: the module's real DI wiring must resolve <see cref="IEmailSender"/>
/// to <see cref="ThrottledEmailSender"/>, not the raw transport. This is the exact class of bug the issue
/// fixes — the rate limiter was registered in the container but never actually consulted, because nothing
/// forced the resolved <see cref="IEmailSender"/> through it. Unlike <c>ThrottledEmailSenderTests</c>,
/// which exercises the decorator in isolation, this drives
/// <see cref="DependencyInjection.AddNotificationsModule"/> itself against a real
/// <see cref="ServiceCollection"/>, the way <c>Faber.Api</c>'s <c>Program.cs</c> does.
/// </summary>
public class DependencyInjectionTests
{
    private static IConfiguration CreateConfiguration()
    {
        var settings = new Dictionary<string, string?>
        {
            ["FluentEmail:SmtpServer"] = "localhost",
            ["FluentEmail:SmtpPort"] = "1025",
            ["FluentEmail:EnableSsl"] = "false",
            ["FluentEmail:FromEmail"] = "no-reply@example.com",
            ["FluentEmail:FromName"] = "Faber",
            ["FluentEmail:Username"] = string.Empty,
            ["FluentEmail:Password"] = string.Empty
        };

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private static IWebHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName = environmentName;

        return environment;
    }

    private static IVaultModuleApi CreateVaultModuleApi()
    {
        var vaultModuleApi = Substitute.For<IVaultModuleApi>();

        vaultModuleApi
            .GetSecretValueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult("test-resend-api-key"));

        return vaultModuleApi;
    }

    private static ServiceCollection CreateBaseServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(CreateConfiguration());
        services.AddSingleton(Substitute.For<IDocumentsModuleApi>());

        return services;
    }

    [Fact]
    public void AddNotificationsModule_DevelopmentEnvironment_ShouldResolveIEmailSenderAsThrottledEmailSender()
    {
        var services = CreateBaseServices();

        services.AddNotificationsModule(CreateEnvironment("Development"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        emailSender.ShouldBeOfType<ThrottledEmailSender>();
    }

    [Fact]
    public void AddNotificationsModule_DevelopmentEnvironment_ShouldResolveSmtpTransport()
    {
        // ISender reads FluentEmailOptions when resolved rather than from a provider built during
        // registration; this pins that the configured options still produce the SMTP transport.
        var services = CreateBaseServices();

        services.AddNotificationsModule(CreateEnvironment("Development"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISender>().ShouldBeOfType<SmtpSender>();
    }

    [Fact]
    public void AddNotificationsModule_ProductionEnvironment_ShouldResolveIEmailSenderAsThrottledEmailSender()
    {
        var services = CreateBaseServices();
        services.AddSingleton(CreateVaultModuleApi());

        services.AddNotificationsModule(CreateEnvironment("Production"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        emailSender.ShouldBeOfType<ThrottledEmailSender>();
    }
}
