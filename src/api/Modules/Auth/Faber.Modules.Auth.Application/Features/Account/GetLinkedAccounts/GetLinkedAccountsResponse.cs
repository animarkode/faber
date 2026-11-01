namespace Faber.Modules.Auth.Application.Features.Account.GetLinkedAccounts;

/// <param name="LinkAction">App action name that links the provider (<c>link-google</c>, <c>link-github</c>); null otherwise.</param>
public record GetLinkedAccountsResponse(
    string ProviderAlias,
    string? DisplayName,
    bool Connected,
    string? LinkedUsername,
    string? LinkAction);
