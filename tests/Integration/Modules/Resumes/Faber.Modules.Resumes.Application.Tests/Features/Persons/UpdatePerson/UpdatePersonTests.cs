using System.Net;
using Faber.Modules.Resumes.Application.Features.Persons.CreatePerson;
using Faber.Modules.Resumes.Application.Features.Persons.GetPerson;
using Faber.Modules.Resumes.Application.Features.Persons.UpdatePerson;
using Faber.Modules.Resumes.Application.Features.Resumes.GetResume;
using Faber.Modules.Resumes.Application.Features.Resumes.Shared;
using Faber.Modules.Resumes.Application.Features.Resumes.UpdateTitle;
using Faber.Modules.Resumes.Application.Tests.Features.Persons.UpdatePerson.Data;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;

using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Persons.UpdatePerson;

[Collection<CollectionResumes>]
[Priority(12)]
public class UpdatePersonTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task UpdatePerson_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .PUTAsync<UpdatePersonEndpoint, UpdatePersonRequest>(
                new UpdatePersonRequest(Guid.NewGuid(), Guid.NewGuid(), null, null, null, null, null, null, null, null, null, null, null, null));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task UpdatePerson_ShouldModifyFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreatePersonRequestFaker(resume.Id).Generate();

        var (_, personResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreatePersonEndpoint, CreatePersonRequest, CreatePersonResponse>(createRequest);

        var updatedPerson = new CreatePersonRequestFaker(resume.Id, seed: 12001).Generate();
        var updateRequest = new UpdatePersonRequest(
            personResponse.Id,
            resume.Id,
            updatedPerson.JobTitle,
            updatedPerson.Firstname,
            updatedPerson.Lastname,
            updatedPerson.Email,
            updatedPerson.Phone,
            updatedPerson.Country,
            updatedPerson.City,
            updatedPerson.Street,
            updatedPerson.PostCode,
            updatedPerson.Nationality,
            updatedPerson.DateOfBirth,
            updatedPerson.DrivingLicense);

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdatePersonEndpoint, UpdatePersonRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetPersonEndpoint, GetPersonRequest, GetPersonResponse>(
                new GetPersonRequest(resume.Id, personResponse.Id));

        getResponse.Lastname.ShouldBe(updatedPerson.Lastname);
        getResponse.Email.ShouldBe(updatedPerson.Email);
        getResponse.JobTitle.ShouldBe(updatedPerson.JobTitle);
    }

    [Fact]
    [Priority(2)]
    public async Task NonExistentPerson_ShouldReturnNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var person = new CreatePersonRequestFaker(resume.Id).Generate();
        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdatePersonEndpoint, UpdatePersonRequest>(
                new UpdatePersonRequest(
                    Guid.NewGuid(), resume.Id, person.JobTitle, person.Firstname, person.Lastname,
                    person.Email, person.Phone, person.Country, person.City, person.Street,
                    person.PostCode, person.Nationality, person.DateOfBirth, person.DrivingLicense));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(3)]
    public async Task CrossUserUpdatePerson_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var createRequest = new CreatePersonRequestFaker(resume.Id).Generate();

        var (_, personResponse) = await app.Client
            .WithAuthToken(userAToken)
            .POSTAsync<CreatePersonEndpoint, CreatePersonRequest, CreatePersonResponse>(createRequest);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var updated = new CreatePersonRequestFaker(resume.Id, seed: 12002).Generate();
        var httpResponse = await app.Client
            .WithAuthToken(userBToken)
            .PUTAsync<UpdatePersonEndpoint, UpdatePersonRequest>(
                new UpdatePersonRequest(
                    personResponse.Id, resume.Id, updated.JobTitle, updated.Firstname, updated.Lastname,
                    updated.Email, updated.Phone, updated.Country, updated.City, updated.Street,
                    updated.PostCode, updated.Nationality, updated.DateOfBirth, updated.DrivingLicense));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [ClassData(typeof(InvalidUpdatePersonData))]
    [Priority(4)]
    public async Task UpdatePerson_WithInvalidData_ShouldReturnBadRequest(UpdatePersonRequest invalidRequest)
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreatePersonRequestFaker(resume.Id).Generate();
        var (_, createResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreatePersonEndpoint, CreatePersonRequest, CreatePersonResponse>(createRequest);

        var updateRequest = invalidRequest with { Id = createResponse.Id, ResumeId = resume.Id };

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdatePersonEndpoint, UpdatePersonRequest>(updateRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Priority(5)]
    public async Task FirstNameUpdate_ShouldSnapshotFullNameIntoResumeTitle()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreatePersonRequestFaker(resume.Id).Generate() with
        {
            Firstname = null,
            Lastname = null
        };
        var (_, personResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreatePersonEndpoint, CreatePersonRequest, CreatePersonResponse>(createRequest);

        var updateRequest = new UpdatePersonRequest(
            personResponse.Id,
            resume.Id,
            createRequest.JobTitle,
            "Ada",
            "Lovelace",
            createRequest.Email,
            createRequest.Phone,
            createRequest.Country,
            createRequest.City,
            createRequest.Street,
            createRequest.PostCode,
            createRequest.Nationality,
            createRequest.DateOfBirth,
            createRequest.DrivingLicense);

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdatePersonEndpoint, UpdatePersonRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResumeResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(new GetResumeRequest(resume.Id));

        getResumeResponse.Title.ShouldBe("Ada Lovelace");
    }

    [Fact]
    [Priority(6)]
    public async Task SecondNameUpdate_ShouldLeaveResumeTitleUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreatePersonRequestFaker(resume.Id).Generate() with
        {
            Firstname = null,
            Lastname = null
        };
        var (_, personResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreatePersonEndpoint, CreatePersonRequest, CreatePersonResponse>(createRequest);

        var firstUpdateRequest = new UpdatePersonRequest(
            personResponse.Id,
            resume.Id,
            createRequest.JobTitle,
            "Ada",
            "Lovelace",
            createRequest.Email,
            createRequest.Phone,
            createRequest.Country,
            createRequest.City,
            createRequest.Street,
            createRequest.PostCode,
            createRequest.Nationality,
            createRequest.DateOfBirth,
            createRequest.DrivingLicense);

        await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdatePersonEndpoint, UpdatePersonRequest>(firstUpdateRequest);

        var secondUpdateRequest = firstUpdateRequest with { Firstname = "Grace", Lastname = "Hopper" };

        var secondUpdateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdatePersonEndpoint, UpdatePersonRequest>(secondUpdateRequest);

        secondUpdateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResumeResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(new GetResumeRequest(resume.Id));

        getResumeResponse.Title.ShouldBe("Ada Lovelace");
    }

    [Fact]
    [Priority(7)]
    public async Task ManualTitleUpdateThenNameUpdate_ShouldLeaveManualTitleUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreatePersonRequestFaker(resume.Id).Generate();
        var (_, personResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreatePersonEndpoint, CreatePersonRequest, CreatePersonResponse>(createRequest);

        await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateTitleEndpoint, UpdateTitleRequest>(new UpdateTitleRequest(resume.Id, "Manual Title"));

        var updateRequest = new UpdatePersonRequest(
            personResponse.Id,
            resume.Id,
            createRequest.JobTitle,
            "Ada",
            "Lovelace",
            createRequest.Email,
            createRequest.Phone,
            createRequest.Country,
            createRequest.City,
            createRequest.Street,
            createRequest.PostCode,
            createRequest.Nationality,
            createRequest.DateOfBirth,
            createRequest.DrivingLicense);

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdatePersonEndpoint, UpdatePersonRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResumeResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(new GetResumeRequest(resume.Id));

        getResumeResponse.Title.ShouldBe("Manual Title");
    }

    [Fact]
    [Priority(8)]
    public async Task NameUpdateWithBlankNames_ShouldLeaveResumeTitleNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreatePersonRequestFaker(resume.Id).Generate() with
        {
            Firstname = null,
            Lastname = null
        };
        var (_, personResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreatePersonEndpoint, CreatePersonRequest, CreatePersonResponse>(createRequest);

        var updateRequest = new UpdatePersonRequest(
            personResponse.Id,
            resume.Id,
            createRequest.JobTitle,
            null,
            null,
            createRequest.Email,
            createRequest.Phone,
            createRequest.Country,
            createRequest.City,
            createRequest.Street,
            createRequest.PostCode,
            createRequest.Nationality,
            createRequest.DateOfBirth,
            createRequest.DrivingLicense);

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdatePersonEndpoint, UpdatePersonRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResumeResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(new GetResumeRequest(resume.Id));

        getResumeResponse.Title.ShouldBeNull();
    }
}
