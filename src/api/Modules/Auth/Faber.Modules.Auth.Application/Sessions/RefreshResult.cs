namespace Faber.Modules.Auth.Application.Sessions;

public sealed record RefreshResult(RefreshOutcome Outcome, TokenResponse? Tokens)
{
    public static readonly RefreshResult Rejected = new(RefreshOutcome.Rejected, null);
    public static readonly RefreshResult Unavailable = new(RefreshOutcome.Unavailable, null);

    public static RefreshResult Refreshed(TokenResponse tokens) => new(RefreshOutcome.Refreshed, tokens);
}
