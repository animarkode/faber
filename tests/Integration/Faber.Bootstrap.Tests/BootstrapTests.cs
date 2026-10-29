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
    public async Task MissingKvMount_ShouldCreateKvVersion2Mount()
    {
        HttpRequestMessage? createRequest = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Json(HttpStatusCode.OK, "{}");
            }

            createRequest = await CloneAsync(request);
            return Json(HttpStatusCode.NoContent, string.Empty);
        });
        var client = CreateVaultClient(handler);

        await client.EnsureKvV2MountAsync(TestContext.Current.CancellationToken);

        createRequest.ShouldNotBeNull();
        createRequest.Method.ShouldBe(HttpMethod.Post);
        createRequest.RequestUri!.PathAndQuery.ShouldBe("/v1/sys/mounts/secrets");
        var body = await createRequest.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("type").GetString().ShouldBe("kv");
        document.RootElement.GetProperty("options").GetProperty("version").GetString().ShouldBe("2");
    }

    [Fact]
    public async Task ExistingKvV2Mount_ShouldNotCreateMount()
    {
        var postCount = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                postCount++;
            }

            return Task.FromResult(Json(HttpStatusCode.OK, """{"secrets/":{"type":"kv","options":{"version":"2"}}}"""));
        });

        await CreateVaultClient(handler).EnsureKvV2MountAsync(TestContext.Current.CancellationToken);

        postCount.ShouldBe(0);
    }

    [Fact]
    public async Task KeycloakClientLookup_ShouldRegenerateAndReturnNewSecret()
    {
        var requests = new List<(HttpMethod Method, string Path)>();
        var handler = new StubHttpMessageHandler(async request =>
        {
            requests.Add((request.Method, request.RequestUri!.PathAndQuery));
            if (request.RequestUri.AbsolutePath.EndsWith("/token", StringComparison.Ordinal))
            {
                var form = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
                form.ShouldContain("username=admin");
                form.ShouldContain("password=admin-password");
                return Json(HttpStatusCode.OK, """{"access_token":"admin-token"}""");
            }

            request.Headers.Authorization!.ToString().ShouldBe("Bearer admin-token");
            if (request.RequestUri.Query.Length > 0)
            {
                return Json(HttpStatusCode.OK, """[{"id":"generated-id","clientId":"faber-api"}]""");
            }

            request.Method.ShouldBe(HttpMethod.Post);
            return Json(HttpStatusCode.OK, """{"value":"generated-secret"}""");
        });

        var result = await CreateKeycloakClient(handler).GetClientSecretAsync(TestContext.Current.CancellationToken);

        result.ShouldBe("generated-secret");
        requests.ShouldContain((HttpMethod.Get, "/admin/realms/faber/clients?clientId=faber-api"));
        requests.ShouldContain((HttpMethod.Post, "/admin/realms/faber/clients/generated-id/client-secret"));
    }

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
    public async Task RepeatedBootstrap_ShouldWriteOnlyTheKeycloakSecretIdempotently()
    {
        var storedSecrets = new Dictionary<string, string>();
        var vaultHandler = new StubHttpMessageHandler(async request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Json(HttpStatusCode.OK, """{"secrets/":{"type":"kv","options":{"version":"2"}}}""");
            }

            var path = request.RequestUri!.AbsolutePath.Split('/').Last();
            storedSecrets[path] = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
            return Json(HttpStatusCode.OK, "{}");
        });
        var keycloakHandler = SuccessfulKeycloakHandler();
        var options = Options.Create(CreateOptions());
        var workflow = new BootstrapWorkflow(
            new KeycloakAdminClient(new HttpClient(keycloakHandler) { BaseAddress = new Uri("http://keycloak/") }, options),
            new VaultClient(new HttpClient(vaultHandler) { BaseAddress = new Uri("http://vault/") }, options),
            options);

        await workflow.RunAsync(TestContext.Current.CancellationToken);
        var firstWrite = storedSecrets.ToDictionary();
        await workflow.RunAsync(TestContext.Current.CancellationToken);

        storedSecrets.ShouldBe(firstWrite);
        storedSecrets["keycloak"].ShouldContain("generated-secret");
        storedSecrets.Keys.ShouldBe(["keycloak"], ignoreOrder: true, customMessage: "Development never reads secrets/mail, so it is not provisioned");
    }

    [Fact]
    public async Task VaultAuthorizationFailure_ShouldReportStatusWithoutResponseBody()
    {
        const string sensitiveResponse = "client_secret=must-not-be-logged";
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(
            Json(HttpStatusCode.Forbidden, sensitiveResponse)));

        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            CreateVaultClient(handler).EnsureKvV2MountAsync(TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("list Vault secret mounts");
        exception.Message.ShouldContain("403");
        exception.Message.ShouldNotContain(sensitiveResponse);
        exception.Message.ShouldNotContain("must-not-be-logged");
    }

    private static KeycloakAdminClient CreateKeycloakClient(HttpMessageHandler handler)
    {
        var options = Options.Create(CreateOptions());
        return new KeycloakAdminClient(
            new HttpClient(handler) { BaseAddress = new Uri(options.Value.KeycloakAddress) },
            options);
    }

    private static VaultClient CreateVaultClient(HttpMessageHandler handler)
    {
        var options = Options.Create(CreateOptions());
        return new VaultClient(
            new HttpClient(handler) { BaseAddress = new Uri(options.Value.VaultAddress) },
            options);
    }

    private static BootstrapOptions CreateOptions() => new()
    {
        KeycloakAddress = "http://keycloak/",
        KeycloakAdminUsername = "admin",
        KeycloakAdminPassword = "admin-password",
        KeycloakRealm = "faber",
        KeycloakClientId = "faber-api",
        VaultAddress = "http://vault/",
        VaultToken = "development-token",
        VaultMountPoint = "secrets"
    };

    private static StubHttpMessageHandler SuccessfulKeycloakHandler() => new(request =>
    {
        if (request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal))
        {
            return Task.FromResult(Json(HttpStatusCode.OK, """{"access_token":"admin-token"}"""));
        }

        if (request.RequestUri.Query.Length > 0)
        {
            return Task.FromResult(Json(HttpStatusCode.OK, """[{"id":"generated-id","clientId":"faber-api"}]"""));
        }

        if (request.RequestUri.AbsolutePath == "/admin/realms/faber")
        {
            return Task.FromResult(request.Method == HttpMethod.Get
                ? Json(HttpStatusCode.OK, """{"realm":"faber"}""")
                : Json(HttpStatusCode.NoContent, string.Empty));
        }

        // Regenerated on every call; this stub returns the same value both times a real
        // Keycloak would return a different secret per call, but Vault simply reflects
        // whatever was most recently regenerated either way.
        request.Method.ShouldBe(HttpMethod.Post);
        return Task.FromResult(Json(HttpStatusCode.OK, """{"value":"generated-secret"}"""));
    });

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string content) => new(statusCode)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        if (request.Content is not null)
        {
            clone.Content = new StringContent(
                await request.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
                Encoding.UTF8,
                "application/json");
        }

        return clone;
    }

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
