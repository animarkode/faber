using System.Net;
using Faber.Modules.Resumes.Application.Features.Resumes.CreateResume;
using Faber.Modules.Resumes.Application.Features.Resumes.GetResume;
using Faber.Modules.Resumes.Application.Features.Resumes.Shared;
using Faber.Modules.Resumes.Application.Features.Resumes.UpdateHobbies;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Resumes.UpdateHobbies;

[Collection<CollectionResumes>]
[Priority(6)]
public class UpdateHobbiesTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task UpdateHobbies_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .PUTAsync<UpdateHobbiesEndpoint, UpdateHobbiesRequest>(
                new UpdateHobbiesRequest(Guid.NewGuid(), "Hobbies"));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task UpdateHobbies_ShouldModifyHobbies()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var createRequest = new CreateResumeRequest("en-us");

        var (_, createResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateResumeEndpoint, CreateResumeRequest, CreateResumeResponse>(createRequest);

        var updateRequest = new UpdateHobbiesRequest(createResponse.Id, "Reading, Swimming");

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateHobbiesEndpoint, UpdateHobbiesRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getRequest = new GetResumeRequest(createResponse.Id);

        var (_, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(getRequest);

        getResponse.Hobbies.ShouldBe("Reading, Swimming");
    }

    [Fact]
    [Priority(2)]
    public async Task NonExistentResume_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateHobbiesEndpoint, UpdateHobbiesRequest>(
                new UpdateHobbiesRequest(Guid.NewGuid(), "Hobbies"));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(3)]
    public async Task HobbiesWithExactly200VisibleCharacters_ShouldReturnNoContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var createRequest = new CreateResumeRequest("en-us");

        var (_, createResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateResumeEndpoint, CreateResumeRequest, CreateResumeResponse>(createRequest);

        var hobbies = $"<p><strong>{new string('a', 200)}</strong></p>";
        var updateRequest = new UpdateHobbiesRequest(createResponse.Id, hobbies);

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateHobbiesEndpoint, UpdateHobbiesRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    [Priority(4)]
    public async Task HobbiesWithDisallowedAttributeContainingAngleBracketAnd200VisibleCharacters_ShouldReturnNoContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var (_, createResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateResumeEndpoint, CreateResumeRequest, CreateResumeResponse>(new CreateResumeRequest("en-us"));

        var hobbies = $"<p data-value=\">\">{new string('a', 200)}</p>";
        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateHobbiesEndpoint, UpdateHobbiesRequest>(new UpdateHobbiesRequest(createResponse.Id, hobbies));

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    [Priority(5)]
    public async Task HobbiesWith201VisibleCharacters_ShouldReturnBadRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var createRequest = new CreateResumeRequest("en-us");

        var (_, createResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateResumeEndpoint, CreateResumeRequest, CreateResumeResponse>(createRequest);

        var hobbies = $"<p><strong>{new string('a', 201)}</strong></p>";
        var updateRequest = new UpdateHobbiesRequest(createResponse.Id, hobbies);

        var (httpResponse, response) = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateHobbiesEndpoint, UpdateHobbiesRequest, ErrorResponse>(updateRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Errors.ShouldContainKey("hobbies");
    }
}
