using System.Diagnostics.CodeAnalysis;
using Bogus;
using Faber.Modules.Resumes.Application.Features.Persons.CreatePerson;

namespace Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;

public sealed class CreatePersonRequestFaker : Faker<CreatePersonRequest>
{
    [SuppressMessage("Usage", "MA0056:Do not call overridable members in constructor",
        Justification = "Bogus configures a Faker through its virtual fluent methods; the faker is not subclassed further.")]
    public CreatePersonRequestFaker(Guid resumeId, int seed = ResumesTestConstants.PersonSeed)
    {
        UseSeed(seed);
        CustomInstantiator(f => new CreatePersonRequest(
            resumeId,
            f.Name.JobTitle(),
            f.Name.FirstName(),
            f.Name.LastName(),
            f.Internet.Email(),
            f.Phone.PhoneNumber(),
            f.Address.Country(),
            f.Address.City(),
            f.Address.StreetAddress(),
            f.Address.ZipCode(),
            f.Address.Country(),
            f.Date.PastDateOnly(30, new DateOnly(2000, 1, 1)),
            f.PickRandom("A", "B", "C", "D")));
    }
}
