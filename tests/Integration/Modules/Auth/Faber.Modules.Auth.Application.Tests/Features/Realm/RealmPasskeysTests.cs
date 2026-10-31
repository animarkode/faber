using Faber.Testing.Shared.Containers;
using Shouldly;

namespace Faber.Modules.Auth.Application.Tests.Features.Realm;

/// <summary>
/// keycloak-config-cli drops the passkey fields on import, so <c>infra/keycloak/passkeys.sh</c> sets them
/// afterwards. These tests prove it did, and that its kcadm update left the imported realm alone.
/// </summary>
[Collection<CollectionAuth>]
public class RealmPasskeysTests(WebApp app)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Realm_AfterStartup_ShouldHavePasskeysEnabled()
    {
        var realm = await app.Keycloak.Admin.GetRealmAsync(Ct);

        realm["webAuthnPolicyPasswordlessPasskeysEnabled"]!.GetValue<bool>().ShouldBeTrue();
        realm["webAuthnPolicyPasswordlessMediation"]!.GetValue<string>().ShouldBe("conditional");
    }

    [Fact]
    public async Task Realm_AfterEnablingPasskeys_ShouldKeepTheImportedSettings()
    {
        var realm = await app.Keycloak.Admin.GetRealmAsync(Ct);

        realm["webAuthnPolicyRpEntityName"]!.GetValue<string>().ShouldBe("Faber");
        realm["verifyEmail"]!.GetValue<bool>().ShouldBeTrue();
        realm["passwordPolicy"]!.GetValue<string>().ShouldContain("length(8)");
    }
}
