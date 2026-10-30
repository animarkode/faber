using Faber.Testing.Shared.Data;

namespace Faber.Api.Tests.Features.RateLimiting.Data;

/// Accounts reserved for the per-user named-policy tests (user-lookup, authenticated-default,
/// expensive-resource). Every user in the shared RegisteredUsersData set is already spent by an
/// existing rate limiting suite, and an exhausted per-user budget stays exhausted for the whole
/// 60-second window — so tests that need to *exhaust* a per-user policy need accounts nobody else
/// touches. Indices 0-4 are unused (their suites went with the Users and Identity modules);
/// 5-8 to ResumesCrudRateLimitingTests; 9-11 to DocumentGenerationRateLimitingTests.
///
/// A different Bogus seed keeps these usernames and emails clear of RegisteredUsersData. The faker
/// is sequential under a fixed seed, so raising Count only appends — indices 0-4 keep the exact
/// identities the existing suites already depend on. Unlike RegisteredUsersData this type is never
/// used as [ClassData], so it is a plain static provider rather than an IEnumerable<TheoryDataRow<>>.
public static class RateLimitingUsersData
{
    private const int Seed = 54321;
    private const int Count = 12;

    public static List<TestUser> Generate()
    {
        var faker = new TestUserFaker(Seed);

        return Enumerable.Range(0, Count).Select(_ => faker.Generate()).ToList();
    }
}
