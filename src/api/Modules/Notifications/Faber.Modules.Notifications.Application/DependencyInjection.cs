using System.Net;
using System.Net.Mail;
using Faber.Modules.Communication.PublicApi;
using Faber.Modules.Notifications.Application.Email;
using Faber.Modules.Notifications.Application.Email.RateLimiting;
using Faber.Modules.Notifications.PublicApi.Options;
using Faber.Modules.Notifications.PublicApi.OptionsSetup;
using FluentEmail.Core.Interfaces;
using FluentEmail.Smtp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Resend;

namespace Faber.Modules.Notifications.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IWebHostEnvironment environment)
    {
        services.ConfigureOptions<FluentEmailOptionsSetup>();

        // Registered unconditionally: the limiter is environment-agnostic and its Enabled flag already
        // follows the shared RateLimiting:Enabled switch, so test hosts resolve it without special-casing.
        // Consumed by ThrottledEmailSender, which decorates whichever transport the environment selected.
        services.ConfigureOptions<RecipientEmailRateLimitingOptionsSetup>();
        services.AddSingleton<IRecipientEmailRateLimiter, RecipientEmailRateLimiter>();

        if (environment.IsDevelopment())
        {
            // ISender reads FluentEmailOptions when resolved instead of from a throwaway provider built
            // here, and takes a factory rather than one SmtpClient instance: a shared client keeps
            // its pooled connection for the app lifetime, so once the SMTP server drops it (Mailpit idling
            // out or restarting) the next send writes to a dead socket, and SmtpClient is not safe for
            // concurrent sends anyway. SmtpSender disposes each factory-built client after its send.
            services.AddScoped<ISender>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<FluentEmailOptions>>().Value;

                return new SmtpSender(() => CreateSmtpClient(options));
            });

            services.AddScoped<FluentEmailSender>();
            services.AddScoped<IEmailSender>(sp => new ThrottledEmailSender(
                sp.GetRequiredService<FluentEmailSender>(),
                sp.GetRequiredService<IRecipientEmailRateLimiter>(),
                sp.GetRequiredService<ILogger<ThrottledEmailSender>>()));
        }

        if (environment.IsProduction())
        {
            services.AddHttpClient<ResendClient>();
            services.ConfigureOptions<ResendClientOptionsSetup>();
            services.AddTransient<IResend, ResendClient>();
            services.AddScoped<ResendEmailSender>();
            services.AddScoped<IEmailSender>(sp => new ThrottledEmailSender(
                sp.GetRequiredService<ResendEmailSender>(),
                sp.GetRequiredService<IRecipientEmailRateLimiter>(),
                sp.GetRequiredService<ILogger<ThrottledEmailSender>>()));
        }

        return services;
    }

    private static SmtpClient CreateSmtpClient(FluentEmailOptions options) =>
        new(options.SmtpServer, int.Parse(options.SmtpPort))
        {
            EnableSsl = options.EnableSsl,
            UseDefaultCredentials = string.IsNullOrEmpty(options.Username),
            Credentials = new NetworkCredential(options.Username, options.Password)
        };
}