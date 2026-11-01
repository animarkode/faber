namespace Faber.Modules.Auth.Application.Features.Account.GetCredentials;

public record StoredCredential(string Id, string? UserLabel, DateTimeOffset? CreatedDate);
