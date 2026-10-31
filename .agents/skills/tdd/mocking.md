# When to Mock

Mock at **system boundaries** only:

- External APIs (Keycloak admin API, Resend email)
- Third-party services that are slow/unreliable in tests
- Time/randomness (when determinism is needed)

Don't mock:

- DbContext — use real PostgreSQL via Testcontainers
- Redis — use real Redis via Testcontainers
- Your own command handlers, services, or modules
- Internal collaborators within a module

## Preferred: Real Infrastructure via Testcontainers

```csharp
// WebApp fixture configures real infrastructure
public class WebApp : AppFixture<Program>
{
    // PostgreSQL, Redis, Keycloak — all real containers
    // Only external services (email) are substituted
}
```

## When You Must Mock: Use NSubstitute

At system boundaries, design interfaces that are easy to substitute:

**1. Use DI registration in WebApp fixture**

```csharp
// In WebApp fixture — substitute only external services
protected override void ConfigureServices(IServiceCollection services)
{
    var emailSender = Substitute.For<IEmailSender>();
    emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
        .Returns(Task.CompletedTask);

    services.AddSingleton(emailSender);
}
```

**2. Module APIs as boundaries**

Cross-module calls go through `I{Module}ModuleApi` interfaces — these are natural mock points when testing a module in isolation:

```csharp
// If testing Auth module and need to stub Identity behavior
var identityApi = Substitute.For<IIdentityModuleApi>();
identityApi.GetUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(ErrorOrFactory.From(new UserResponse("id", "user@test.com")));
```

**3. Prefer specific interfaces over generic ones**

```csharp
// GOOD: Each method is independently mockable
public interface IEmailSender
{
    Task SendVerificationEmailAsync(string email, string token, CancellationToken ct);
    Task SendPasswordResetEmailAsync(string email, string token, CancellationToken ct);
}

// BAD: Generic method requires conditional setup
public interface IEmailSender
{
    Task SendAsync(string template, object data, CancellationToken ct);
}
```