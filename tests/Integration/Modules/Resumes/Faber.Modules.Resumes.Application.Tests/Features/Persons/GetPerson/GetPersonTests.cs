using System.Net;
using Faber.Modules.Resumes.Application.Features.Persons.CreatePerson;
using Faber.Modules.Resumes.Application.Features.Persons.GetPerson;
using Faber.Modules.Resumes.Application.Features.Persons.GetPersonByResume;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Persons.GetPerson;

[Collection<CollectionResumes>]
[Priority(11)]
public class GetPersonTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task GetPerson_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var (httpResponse, _) = await app.Client
            .GETAsync<GetPersonEndpoint, GetPersonRequest, GetPersonResponse>(
                new GetPersonRequest(Guid.NewGuid(), Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task GetPerson_ShouldReturnCreatedPerson()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreatePersonRequestFaker(resume.Id).Generate();

        var (_, personResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreatePersonEndpoint, CreatePersonRequest, CreatePersonResponse>(createRequest);

        var (httpResponse, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetPersonEndpoint, GetPersonRequest, GetPersonResponse>(
                new GetPersonRequest(resume.Id, personResponse.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.Id.ShouldBe(personResponse.Id);
        getResponse.Firstname.ShouldBe(createRequest.Firstname);
    }

    [Fact]
    [Priority(2)]
    public async Task GetPersonByResume_ShouldReturnPerson()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreatePersonRequestFaker(resume.Id, seed: 11001).Generate();

        await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreatePersonEndpoint, CreatePersonRequest, CreatePersonResponse>(createRequest);

        var (httpResponse, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetPersonByResumeEndpoint, GetPersonByResumeRequest, GetPersonByResumeResponse>(
                new GetPersonByResumeRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.Firstname.ShouldBe(createRequest.Firstname);
        getResponse.ResumeId.ShouldBe(resume.Id);
    }

    [Fact]
    [Priority(3)]
    public async Task NonExistentPerson_ShouldReturnNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetPersonEndpoint, GetPersonRequest, GetPersonResponse>(
                new GetPersonRequest(resume.Id, Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(4)]
    public async Task GetPersonByNonExistentResume_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetPersonByResumeEndpoint, GetPersonByResumeRequest, GetPersonByResumeResponse>(
                new GetPersonByResumeRequest(Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(5)]
    public async Task CrossUserGetPerson_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var createRequest = new CreatePersonRequestFaker(resume.Id).Generate();

        var (_, personResponse) = await app.Client
            .WithAuthToken(userAToken)
            .POSTAsync<CreatePersonEndpoint, CreatePersonRequest, CreatePersonResponse>(createRequest);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(userBToken)
            .GETAsync<GetPersonEndpoint, GetPersonRequest, GetPersonResponse>(
                new GetPersonRequest(resume.Id, personResponse.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(6)]
    public async Task CrossUserGetPersonByResume_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var createRequest = new CreatePersonRequestFaker(resume.Id).Generate();

        await app.Client
            .WithAuthToken(userAToken)
            .POSTAsync<CreatePersonEndpoint, CreatePersonRequest, CreatePersonResponse>(createRequest);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(userBToken)
            .GETAsync<GetPersonByResumeEndpoint, GetPersonByResumeRequest, GetPersonByResumeResponse>(
                new GetPersonByResumeRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
