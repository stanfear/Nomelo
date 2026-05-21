# Authentik configuration for Nomelo

These steps assume a running Authentik instance reachable at `https://auth.example.com`.

## 1. Create the OIDC provider

In the Authentik admin UI:

1. Providers → Create → OAuth2/OpenID Provider.
2. Name: `Nomelo`.
3. Authorization flow: `default-provider-authorization-implicit-consent` (or your hardened flow).
4. Client type: Confidential.
5. Client ID: auto-generated, copy it.
6. Client Secret: auto-generated, copy it.
7. Redirect URIs (one per line):
   ```
   https://nomelo.example.com/signin-oidc
   http://localhost:8080/signin-oidc
   ```
8. Signing Key: any active key from your instance.
9. Subject mode: `Based on the User's hashed ID` (stable per-user `sub`).
10. Scopes: keep defaults `openid`, `profile`, `email`.
11. Save.

## 2. Create the application

1. Applications → Create.
2. Name: `Nomelo`.
3. Slug: `nomelo`.
4. Provider: the OIDC provider created above.
5. Launch URL: `https://nomelo.example.com/`.
6. Save.

## 3. Note the Authority URL

The OIDC discovery document is at:

```
https://auth.example.com/application/o/nomelo/.well-known/openid-configuration
```

The `Authority` value for `appsettings`/`.env` is the issuer (the path **without** `.well-known/...`):

```
https://auth.example.com/application/o/nomelo/
```

## 4. Populate .env

```
OIDC_AUTHORITY=https://auth.example.com/application/o/nomelo/
OIDC_CLIENT_ID=<copied from step 1.5>
OIDC_CLIENT_SECRET=<copied from step 1.6>
```

## 5. Restart the app

```bash
docker compose up -d app
```

Open `https://nomelo.example.com/` — it should redirect to Authentik, then back to Nomelo after consent. `/api/me` returns the OIDC `sub` claim.

## Troubleshooting

- **`OpenIdConnectProtocolException: IDX21323`** — usually a redirect URI mismatch. Verify the URI list in step 1.7 matches the actual host header.
- **`unauthorized_client`** — the Authentik application binding to the provider is missing or the user isn't in a permitted group.
- **Cookie not set after callback** — check that the app is behind HTTPS in production. The cookie is `SameSite=Strict` and `Secure` policies behave differently over plain HTTP.
