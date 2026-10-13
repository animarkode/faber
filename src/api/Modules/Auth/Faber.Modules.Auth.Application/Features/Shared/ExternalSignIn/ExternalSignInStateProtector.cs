using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;

/// <summary>
/// Protects the <c>{state, code_verifier}</c> pair stored in the state cookie with a time-limited
/// Data Protection protector, so the cookie is confidential, tamper-proof and self-expiring.
/// </summary>
public class ExternalSignInStateProtector(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<ExternalSignInOptions> options)
{
    /// <summary>Fixed Data Protection purpose string for the state cookie.</summary>
    public const string Purpose = "Faber.Auth.ExternalSignIn.State";

    private const char Separator = '\n';

    private readonly ITimeLimitedDataProtector _protector =
        dataProtectionProvider.CreateProtector(Purpose).ToTimeLimitedDataProtector();

    /// <summary>The lifetime of a freshly protected value.</summary>
    public TimeSpan Lifetime => TimeSpan.FromSeconds(options.Value.StateLifetimeSeconds);

    /// <summary>Protects the pair so that it expires after <see cref="Lifetime"/>.</summary>
    public string Protect(string state, string codeVerifier)
    {
        return Protect(state, codeVerifier, DateTimeOffset.UtcNow.Add(Lifetime));
    }

    /// <summary>Protects the pair with an explicit expiry (used by tests to mint expired cookies).</summary>
    public string Protect(string state, string codeVerifier, DateTimeOffset expiresAt)
    {
        return _protector.Protect($"{state}{Separator}{codeVerifier}", expiresAt);
    }

    /// <summary>Returns the pair, or <see langword="null"/> when the value is malformed, tampered with or expired.</summary>
    public (string State, string CodeVerifier)? Unprotect(string? protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue))
        {
            return null;
        }

        try
        {
            var parts = _protector.Unprotect(protectedValue).Split(Separator);

            return parts.Length == 2 ? (parts[0], parts[1]) : null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}
