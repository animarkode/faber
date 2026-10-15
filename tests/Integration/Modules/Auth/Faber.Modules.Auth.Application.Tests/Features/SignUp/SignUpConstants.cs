namespace Faber.Modules.Auth.Application.Tests.Features.SignUp;

public static class SignUpConstants
{
    public const string RequestUri = "api/v1/auth/sign-up";
    public const string ValidPassword = "ValidPass1!";
    public const string ValidUsername = "validtestuser";
    public const string ValidEmail = "validtest@email.com";
    public const string ValidFirstName = "John";
    public const string ValidLastName = "Doe";
    public const int MinValidationErrorsForAllFields = 4;
    public const string TransportFailureUsername = "smtpfailureuser";
    public const string TransportFailureEmail = "smtp-failure@email.com";
}