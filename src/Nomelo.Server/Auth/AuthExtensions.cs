using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Nomelo.Server.Auth;

public static class AuthExtensions
{
    public const string CookieScheme = "nm-cookie";
    public const string OidcScheme = "nm-oidc";

    public static IServiceCollection AddNomeloAuth(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddAuthentication(opt =>
            {
                opt.DefaultScheme = CookieScheme;
                opt.DefaultChallengeScheme = OidcScheme;
            })
            .AddCookie(CookieScheme, opt =>
            {
                opt.Cookie.Name = "nm_auth";
                opt.Cookie.HttpOnly = true;
                opt.Cookie.SameSite = SameSiteMode.Strict;
                opt.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                opt.ExpireTimeSpan = TimeSpan.FromDays(30);
                opt.SlidingExpiration = true;
                opt.Events.OnRedirectToLogin = ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/api"))
                    {
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }
                    ctx.Response.Redirect(ctx.RedirectUri);
                    return Task.CompletedTask;
                };
            })
            .AddOpenIdConnect(OidcScheme, opt =>
            {
                opt.Authority = config["OIDC:Authority"];
                opt.ClientId = config["OIDC:ClientId"];
                opt.ClientSecret = config["OIDC:ClientSecret"];
                opt.ResponseType = OpenIdConnectResponseType.Code;
                opt.UsePkce = true;
                opt.SaveTokens = false;
                opt.GetClaimsFromUserInfoEndpoint = true;
                opt.SignInScheme = CookieScheme;
                opt.Scope.Add("openid");
                opt.Scope.Add("profile");
                opt.Scope.Add("email");
                opt.CallbackPath = "/signin-oidc";
                opt.SignedOutCallbackPath = "/signout-callback-oidc";
            });

        services.AddAuthorization();
        return services;
    }

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/login", (HttpContext ctx, string? returnUrl) =>
        {
            var safeReturnUrl = !string.IsNullOrEmpty(returnUrl)
                               && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
                               && returnUrl.StartsWith("/")
                               && !returnUrl.StartsWith("//")
                ? returnUrl
                : "/";
            return Results.Challenge(
                new() { RedirectUri = safeReturnUrl },
                new[] { OidcScheme });
        });

        app.MapPost("/logout", () =>
            Results.SignOut(
                new() { RedirectUri = "/" },
                new[] { CookieScheme, OidcScheme }));

        app.MapGet("/api/me", (HttpContext ctx) =>
        {
            if (ctx.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();
            var sub = ctx.User.FindFirst("sub")?.Value
                      ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Results.Ok(new { userId = sub });
        });

        return app;
    }
}
