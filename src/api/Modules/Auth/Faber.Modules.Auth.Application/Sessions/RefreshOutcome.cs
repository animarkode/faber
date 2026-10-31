namespace Faber.Modules.Auth.Application.Sessions;

public enum RefreshOutcome
{
    Refreshed,

    /// <summary>Keycloak refused the refresh token (<c>invalid_grant</c>): the session is over.</summary>
    Rejected,

    /// <summary>Keycloak could not answer usefully; the session is kept and retried on a later request.</summary>
    Unavailable
}
