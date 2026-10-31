using System.Net;
using System.Text;
using System.Text.Json;
using Faber.Bootstrap;
using Faber.Bootstrap.Clients;
using Faber.Bootstrap.Workflow;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Faber.Bootstrap.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BootstrapExitCodeCollection
{
    public const string Name = "Bootstrap exit code";
}

public class BootstrapTests
{
    [Fact]
    public async Task EnablePasskeys_ShouldTurnPasskeysOnAndKeepTheRestOfTheRealm()
    {
        string? putBody = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """{"access_token":"admin-token"}""");
            }

            request.Headers.Authorization!.ToString().ShouldBe("Bearer admin-token");
            request.RequestUri.AbsolutePath.ShouldBe("/admin/realms/faber");
            if (request.Method == HttpMethod.Get)
            {
                return Json(HttpStatusCode.OK, """{"realm":"faber","verifyEmail":true,"webAuthnPolicyPasswordlessPasskeysEnabled":false}""");
            }

            request.Method.ShouldBe(HttpMethod.Put);
            putBody = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
            return Json(HttpStatusCode.NoContent, string.Empty);
        });

        await CreateKeycloakClient(handler).EnablePasskeysAsync(TestContext.Current.CancellationToken);

        putBody.ShouldNotBeNull();
        using var document = JsonDocument.Parse(putBody);
        document.RootElement.GetProperty("webAuthnPolicyPasswordlessPasskeysEnabled").GetBoolean().ShouldBeTrue();
        document.RootElement.GetProperty("webAuthnPolicyPasswordlessMediation").GetString().ShouldBe("conditional");
        document.RootElement.GetProperty("verifyEmail").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Workflow_ShouldEnablePasskeysEveryRun()
    {
        var putCount = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal))
            {
                return Task.FromResult(Json(HttpStatusCode.OK, """{"access_token":"admin-token"}"""));
            }

            if (request.Method == HttpMethod.Put)
            {
                putCount++;
                return Task.FromResult(Json(HttpStatusCode.NoContent, string.Empty));
            }

            return Task.FromResult(Json(HttpStatusCode.OK, """{"realm":"faber"}"""));
        });
        var workflow = new BootstrapWorkflow(CreateKeycloakClient(handler));

        await workflow.RunAsync(TestContext.Current.CancellationToken);
        await workflow.RunAsync(TestContext.Current.CancellationToken);

        putCount.ShouldBe(2);
    }

    [Fact]
    public async Task KeycloakFailure_ShouldReportStatusWithoutResponseBody()
    {
        const string sensitiveResponse = "client_secret=must-not-be-logged";
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(
            Json(HttpStatusCode.Forbidden, sensitiveResponse)));

        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            CreateKeycloakClient(handler).EnablePasskeysAsync(TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("authenticate with Keycloak");
        exception.Message.ShouldContain("403");
        exception.Message.ShouldNotContain("must-not-be-logged");
    }

    private static KeycloakAdminClient CreateKeycloakClient(HttpMessageHandler handler)
    {
        var options = Options.Create(CreateOptions());
        return new KeycloakAdminClient(
            new HttpClient(handler) { BaseAddress = new Uri(options.Value.KeycloakAddress) },
            options);
    }

    private static BootstrapOptions CreateOptions() => new()
    {
        KeycloakAddress = "http://keycloak/",
        KeycloakAdminUsername = "admin",
        KeycloakAdminPassword = "admin-password",
        KeycloakRealm = "faber"
    };

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string content) => new(statusCode)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responseFactory(request);
    }
}

[Collection(BootstrapExitCodeCollection.Name)]
public sealed class BootstrapWorkerTests
{
    [Fact]
    public async Task BootstrapFailure_ShouldSetExitCode1AndStopApplication()
    {
        Environment.ExitCode = 0;
        using var lifetime = new TestApplicationLifetime();
        var worker = new TestableBootstrapWorker(
            new FailingBootstrapWorkflow(),
            lifetime,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BootstrapWorker>.Instance);

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

    private sealed class FailingBootstrapWorkflow : IBootstrapWorkflow
    {
        public Task RunAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("bootstrap failed");
    }

    private sealed class TestableBootstrapWorker(
        IBootstrapWorkflow workflow,
        IHostApplicationLifetime lifetime,
        ILogger<BootstrapWorker> logger) : BootstrapWorker(workflow, lifetime, logger)
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
