# Production health probes

The API container listens for internal HTTP probes on port 7107. The container healthcheck and deployment smoke check should request `http://<api-container>:7107/ready` over the container network and require **HTTP 200**. A 503 means at least one required readiness check failed: the Auth or Resumes PostgreSQL context, Vault health and token access, or the configured Keycloak realm. The response does not disclose dependency details.

`http://<api-container>:7107/alive` is the liveness probe. It requires HTTP 200 and remains independent of those dependencies. Use readiness to decide when traffic may be routed; use liveness to detect a stalled API process. Allow for dependency startup and brief outages in the healthcheck's retry and start period settings.

Port 7107 must stay on the private container network. Do not publish it on the host or route it through the reverse proxy. The public API continues on HTTPS port 7106. In Production, `/ready` and `/alive` return 404 on that public listener, and non-probe paths return 404 on the internal listener. Access is decided from the connection's local port, so a client-supplied `Host` or forwarded header cannot turn a public request into an internal probe.

## Deployment validation

- [ ] Confirm `/ready` returns 200 from the container network when PostgreSQL, Vault, and Keycloak are available.
- [ ] Make one required dependency unavailable and confirm `/ready` returns 503 while `/alive` returns 200.
- [ ] Confirm public HTTPS requests to `/ready` and `/alive` return 404 and that neither a host port mapping nor reverse-proxy route exposes port 7107.
- [ ] Update the deployment healthcheck and smoke check to request the internal readiness URL and require HTTP 200.
