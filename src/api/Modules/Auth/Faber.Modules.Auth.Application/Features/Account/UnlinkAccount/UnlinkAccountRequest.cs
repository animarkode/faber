namespace Faber.Modules.Auth.Application.Features.Account.UnlinkAccount;

/// <param name="Alias">The provider alias, such as google or github.</param>
public record UnlinkAccountRequest(string Alias);
