package faber.keycloak;

import org.keycloak.authentication.AuthenticationFlowContext;
import org.keycloak.authentication.Authenticator;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.RealmModel;
import org.keycloak.models.UserModel;

/**
 * Ends the browser flow by sending the user back to the client with {@code error=access_denied}.
 *
 * <p>It sits last, as an alternative, in a flow whose other steps either sign the user in or redirect to
 * an identity provider. Reaching it means none of them could: typically the provider reported an error
 * (the user cancelled there), and Keycloak re-ran the flow, where the redirector steps aside. Without
 * this step Keycloak would render its own page; with it the client receives a standard OIDC error
 * response, together with its {@code state}, at its redirect URI.</p>
 */
public final class ReturnToClientAuthenticator implements Authenticator {

    static final ReturnToClientAuthenticator INSTANCE = new ReturnToClientAuthenticator();

    private ReturnToClientAuthenticator() {
    }

    @Override
    public void authenticate(AuthenticationFlowContext context) {
        context.cancelLogin();
    }

    @Override
    public void action(AuthenticationFlowContext context) {
    }

    @Override
    public boolean requiresUser() {
        return false;
    }

    @Override
    public boolean configuredFor(KeycloakSession session, RealmModel realm, UserModel user) {
        return true;
    }

    @Override
    public void setRequiredActions(KeycloakSession session, RealmModel realm, UserModel user) {
    }

    @Override
    public void close() {
    }
}
