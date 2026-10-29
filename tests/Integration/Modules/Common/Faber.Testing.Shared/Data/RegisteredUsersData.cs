using System.Collections;
using Xunit;

namespace Faber.Testing.Shared.Data;

/// <summary>The users every test fixture creates in Keycloak, generated from a fixed seed.</summary>
public class RegisteredUsersData : IEnumerable<TheoryDataRow<TestUser>>
{
    private const int Seed = 12345;
    private const int Count = 5;

    public IEnumerator<TheoryDataRow<TestUser>> GetEnumerator()
    {
        return Generate().Select(user => new TheoryDataRow<TestUser>(user)).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static List<TestUser> Generate()
    {
        var faker = new TestUserFaker(Seed);

        return Enumerable.Range(0, Count).Select(_ => faker.Generate()).ToList();
    }
}
