using Bogus;

namespace Faber.Testing.Shared.Data;

/// <summary>Generates <see cref="TestUser"/>s that satisfy the realm's password policy.</summary>
public class TestUserFaker(int seed)
{
    private readonly Faker _faker = new("en_US") { Random = new Randomizer(seed) };

    public TestUser Generate()
    {
        var person = new Person("en_US", _faker.Random.Int());

        return new TestUser(
            $"{_faker.Random.AlphaNumeric(8)}.{person.Email}".ToLowerInvariant(),
            _faker.Internet.Password(12, prefix: "Fb1!"),
            person.FirstName,
            person.LastName);
    }
}
