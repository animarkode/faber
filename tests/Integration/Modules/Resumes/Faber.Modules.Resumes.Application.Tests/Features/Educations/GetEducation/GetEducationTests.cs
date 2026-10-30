using System.Net;
using Faber.Modules.Resumes.Application.Features.Educations.CreateEducation;
using Faber.Modules.Resumes.Application.Features.Educations.GetAllEducations;
using Faber.Modules.Resumes.Application.Features.Educations.GetEducation;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Educations.GetEducation;

[Collection<CollectionResumes>]
[Priority(21)]
public class GetEducationTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task GetEducation_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var (httpResponse, _) = await app.Client
            .GETAsync<GetEducationEndpoint, GetEducationRequest, GetEducationResponse>(
                new GetEducationRequest(Guid.NewGuid(), Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task GetEducation_ShouldReturnCreatedEducation()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateEducationRequestFaker(resume.Id).Generate();

        var (_, educationResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateEducationEndpoint, CreateEducationRequest, CreateEducationResponse>(createRequest);

        var (httpResponse, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetEducationEndpoint, GetEducationRequest, GetEducationResponse>(
                new GetEducationRequest(resume.Id, educationResponse.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.Id.ShouldBe(educationResponse.Id);
        getResponse.School.ShouldBe(createRequest.School);
        getResponse.Degree.ShouldBe(createRequest.Degree);
    }

    [Fact]
    [Priority(2)]
    public async Task GetAllEducations_ShouldReturnEducationsForResume()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var items = new CreateEducationRequestFaker(resume.Id).Generate(2);

        await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateEducationEndpoint, CreateEducationRequest, CreateEducationResponse>(items[0]);

        await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateEducationEndpoint, CreateEducationRequest, CreateEducationResponse>(items[1]);

        var (httpResponse, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllEducationsEndpoint, GetAllEducationsRequest, GetAllEducationsResponse>(
                new GetAllEducationsRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.Items.Count.ShouldBe(2);
    }

    [Fact]
    [Priority(3)]
    public async Task NonExistentEducation_ShouldReturnNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetEducationEndpoint, GetEducationRequest, GetEducationResponse>(
                new GetEducationRequest(resume.Id, Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(4)]
    public async Task GetAllEducationsForNonExistentResume_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllEducationsEndpoint, GetAllEducationsRequest, GetAllEducationsResponse>(
                new GetAllEducationsRequest(Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(5)]
    public async Task GetAllEducationsEmptyResume_ShouldReturnEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var (httpResponse, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllEducationsEndpoint, GetAllEducationsRequest, GetAllEducationsResponse>(
                new GetAllEducationsRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.Items.ShouldBeEmpty();
    }

    [Fact]
    [Priority(6)]
    public async Task CrossUserGetEducation_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var createRequest = new CreateEducationRequestFaker(resume.Id).Generate();

        var (_, educationResponse) = await app.Client
            .WithAuthToken(userAToken)
            .POSTAsync<CreateEducationEndpoint, CreateEducationRequest, CreateEducationResponse>(createRequest);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(userBToken)
            .GETAsync<GetEducationEndpoint, GetEducationRequest, GetEducationResponse>(
                new GetEducationRequest(resume.Id, educationResponse.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(7)]
    public async Task CrossUserGetAllEducations_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(userBToken)
            .GETAsync<GetAllEducationsEndpoint, GetAllEducationsRequest, GetAllEducationsResponse>(
                new GetAllEducationsRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
