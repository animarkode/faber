using System.Net;
using System.Net.Mail;
using Faber.Modules.Auth.Application.Features.SignIn;
using Faber.Modules.Auth.Application.Features.SignUp;
using Faber.Testing.Shared.Data;
using Faber.Testing.Shared.Http;
using Faber.Modules.Auth.Application.Tests.Features.SignUp.Data;
using Faber.Modules.Identity.PublicApi;
using FastEndpoints;
using FastEndpoints.Testing;
using Microsoft.Extensions.DependencyInjection;
using FluentEmail.Core;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using static Faber.Modules.Auth.Application.Tests.Features.SignUp.SignUpConstants;

namespace Faber.Modules.Auth.Application.Tests.Features.SignUp;

[Collection<CollectionAuth>]
[Priority(3)]
public class SignUpTests(WebApp app) : TestBase
{
    [Theory]
    [Priority(1)]
    [InlineData("", "", "", "", "", "")]
    [InlineData(" ", " ", " ", " ", " ", " ")]
    public async Task EmptyAllFields_ShouldReturnBadRequest(
        string username,
        string password,
        string confirmPassword,
        string email,
        string firstName,
        string lastName)
    {
        var request = new SignUpRequest(username, password, confirmPassword, email, firstName, lastName);

        var (httpResponse, response) =
            await app.Client.POSTAsync<SignUpEndpoint, SignUpRequest, ErrorResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        response.Errors.Count.ShouldBeGreaterThanOrEqualTo(MinValidationErrorsForAllFields);
    }

    [Theory]
    [Priority(2)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task EmptyUsername_ShouldReturnBadRequest(string username)
    {
        var request = new SignUpRequest(
            username,
            ValidPassword,
            ValidPassword,
            ValidEmail,
            ValidFirstName,
            ValidLastName);

        var (httpResponse, response) =
            await app.Client.POSTAsync<SignUpEndpoint, SignUpRequest, ErrorResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        response.Errors.ShouldContainKey("username");
    }

    [Theory]
    [Priority(3)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task EmptyEmail_ShouldReturnBadRequest(string email)
    {
        var request = new SignUpRequest(
            ValidUsername,
            ValidPassword,
            ValidPassword,
            email,
            ValidFirstName,
            ValidLastName);

        var (httpResponse, response) =
            await app.Client.POSTAsync<SignUpEndpoint, SignUpRequest, ErrorResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        response.Errors.ShouldContainKey("email");
    }

    [Theory]
    [Priority(4)]
    [InlineData("notanemail")]
    [InlineData("missing@")]
    public async Task InvalidEmailFormat_ShouldReturnBadRequest(string email)
    {
        var request = new SignUpRequest(
            ValidUsername,
            ValidPassword,
            ValidPassword,
            email,
            ValidFirstName,
            ValidLastName);

        var (httpResponse, response) =
            await app.Client.POSTAsync<SignUpEndpoint, SignUpRequest, ErrorResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        response.Errors.ShouldContainKey("email");
    }

    [Theory]
    [Priority(5)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task EmptyPassword_ShouldReturnBadRequest(string password)
    {
        var request = new SignUpRequest(
            ValidUsername,
            password,
            ValidPassword,
            ValidEmail,
            ValidFirstName,
            ValidLastName);

        var (httpResponse, response) =
            await app.Client.POSTAsync<SignUpEndpoint, SignUpRequest, ErrorResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        response.Errors.ShouldContainKey("password");
    }

    [Theory]
    [Priority(6)]
    [InlineData("short")]
    [InlineData("1234567")]
    public async Task PasswordTooShort_ShouldReturnBadRequest(string password)
    {
        var request = new SignUpRequest(
            ValidUsername,
            password,
            password,
            ValidEmail,
            ValidFirstName,
            ValidLastName);

        var (httpResponse, response) =
            await app.Client.POSTAsync<SignUpEndpoint, SignUpRequest, ErrorResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        response.Errors.ShouldContainKey("password");
    }

    [Theory]
    [Priority(7)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task EmptyConfirmPassword_ShouldReturnBadRequest(string confirmPassword)
    {
        var request = new SignUpRequest(
            ValidUsername,
            ValidPassword,
            confirmPassword,
            ValidEmail,
            ValidFirstName,
            ValidLastName);

        var (httpResponse, response) =
            await app.Client.POSTAsync<SignUpEndpoint, SignUpRequest, ErrorResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        response.Errors.ShouldContainKey("confirmPassword");
    }

    [Theory]
    [Priority(8)]
    [InlineData("ValidPass1!", "DifferentPass1!")]
    [InlineData("Password123", "Password456")]
    public async Task PasswordsDoNotMatch_ShouldReturnBadRequest(string password, string confirmPassword)
    {
        var request = new SignUpRequest(
            ValidUsername,
            password,
            confirmPassword,
            ValidEmail,
            ValidFirstName,
            ValidLastName);

        var (httpResponse, response) =
            await app.Client.POSTAsync<SignUpEndpoint, SignUpRequest, ErrorResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        response.Errors.ShouldContainKey("confirmPassword");
    }

    [Fact]
    [Priority(9)]
    public async Task DuplicateUsername_ShouldReturnBadRequest()
    {
        var (existingUser, _) = RegisteredUsersData.Generate().First();

        var request = new SignUpRequest(
            existingUser.Username,
            ValidPassword,
            ValidPassword,
            ValidEmail,
            ValidFirstName,
            ValidLastName);

        var (httpResponse, response) =
            await app.Client.POSTAsync<SignUpEndpoint, SignUpRequest, ErrorResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        response.Errors.ShouldContainKey("username");
    }

    [Fact]
    [Priority(10)]
    public async Task DuplicateEmail_ShouldReturnBadRequest()
    {
        var (existingUser, _) = RegisteredUsersData.Generate().First();

        var request = new SignUpRequest(
            ValidUsername,
            ValidPassword,
            ValidPassword,
            existingUser.Email,
            ValidFirstName,
            ValidLastName);

        var (httpResponse, response) =
            await app.Client.POSTAsync<SignUpEndpoint, SignUpRequest, ErrorResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        response.Errors.ShouldContainKey("email");
    }

    [Theory]
    [Priority(11)]
    [ClassData(typeof(FreshSignUpUsersData))]
    public async Task ValidSignUp_ShouldReturnNoContent(SignUpRequest request)
    {
        var httpResponse = await app.Client.PostWithHeadersAsync(
            RequestUri,
            request,
            ct: TestContext.Current.CancellationToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Theory]
    [Priority(12)]
    [ClassData(typeof(FreshSignUpUsersData))]
    public async Task ValidSignUp_UserCanSignInAfterSignUp(SignUpRequest request)
    {
        // A password account cannot sign in until its e-mail is verified in Faber.
        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IIdentityModuleApi>()
                .VerifyEmailAsync(request.Email, TestContext.Current.CancellationToken);
        }

        var signInRequest = new SignInRequest(request.Username, request.Password);

        var (signInHttp, signInResponse) =
            await app.Client.POSTAsync<SignInEndpoint, SignInRequest, SignInResponse>(signInRequest);

        signInHttp.StatusCode.ShouldBe(HttpStatusCode.OK);
        signInResponse.AccessToken.ShouldNotBeNullOrEmpty();
        signInResponse.RefreshToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    [Priority(13)]
    public async Task SmtpTransportFailure_ShouldReturnNoContent()
    {
        // The verification email is sent by an awaited event handler after the user already exists.
        // A dropped SMTP connection must surface as a logged delivery failure, not turn the completed
        // sign-up into a 500 that invites the user to retry into "username taken".
        app.EmailTransport
            .SendAsync(
                Arg.Is<IFluentEmail>(email => email.Data.ToAddresses.Any(
                    address => address.EmailAddress == TransportFailureEmail)),
                Arg.Any<CancellationToken?>())
            .ThrowsAsync(new SmtpException("Failure sending mail.", new IOException("Broken pipe")));

        var request = new SignUpRequest(
            TransportFailureUsername,
            ValidPassword,
            ValidPassword,
            TransportFailureEmail,
            ValidFirstName,
            ValidLastName);

        var httpResponse = await app.Client.PostWithHeadersAsync(
            RequestUri,
            request,
            ct: TestContext.Current.CancellationToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await app.EmailTransport.Received(1).SendAsync(
            Arg.Is<IFluentEmail>(email => email.Data.ToAddresses.Any(
                address => address.EmailAddress == TransportFailureEmail)),
            Arg.Any<CancellationToken?>());
    }
}
