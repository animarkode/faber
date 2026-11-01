using ErrorOr;
using FastEndpoints;

namespace Faber.Modules.Auth.Application.Features.Account.DeleteCredential;

public record DeleteCredentialCommand(string Subject, string AccessToken, string Id) : ICommand<ErrorOr<Success>>;
