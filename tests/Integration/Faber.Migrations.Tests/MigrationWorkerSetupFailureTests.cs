using Faber.Migrations;
using Faber.Modules.Auth.Infrastructure.Database;
using Faber.Modules.Resumes.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;
using AuthDbConst = Faber.Modules.Auth.Infrastructure.Database.DbConstants;
using ResumesDbConst = Faber.Modules.Resumes.Infrastructure.Database.DbConstants;

namespace Faber.Migrations.Tests;

[Collection(MigrationExitCodeGroup.Name)]
public class MigrationWorkerSetupFailureTests
{
    [Fact]
    public async Task MissingDbContextRegistration_ShouldSetExitCode1AndStopApplication()
    {
        Environment.ExitCode = 0;
        using var serviceProvider = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        using var lifetime = new TestApplicationLifetime();
        using var worker = new TestableMigrationWorker(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new MigrationOptions { ExternalSchemas = ["keycloak"] }),
            lifetime,
            serviceProvider.GetRequiredService<ILogger<MigrationWorker>>());

        try
        {
            await worker.RunAsync(TestContext.Current.CancellationToken);

            Environment.ExitCode.ShouldBe(1);
            lifetime.ApplicationStopping.IsCancellationRequested.ShouldBeTrue();
        }
        finally
        {
            Environment.ExitCode = 0;
        }
    }

    private sealed class TestableMigrationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<MigrationOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<MigrationWorker> logger) : MigrationWorker(scopeFactory, options, lifetime, logger)
    {
        public Task RunAsync(CancellationToken cancellationToken) => ExecuteAsync(cancellationToken);
    }

    private sealed class TestApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource _stopping = new();

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => _stopping.Cancel();

        public void Dispose() => _stopping.Dispose();
    }
}
