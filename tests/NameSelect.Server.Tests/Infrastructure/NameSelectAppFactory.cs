using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace NameSelect.Server.Tests.Infrastructure;

public class NameSelectAppFactory : WebApplicationFactory<Program>
{
    public string ConnectionString { get; init; } = "";
    public string ListsDirectory { get; init; } = "";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Lists:Directory", ListsDirectory);
        builder.UseSetting("OIDC:Authority", "https://example.invalid/");
        builder.UseSetting("OIDC:ClientId", "test");
        builder.UseSetting("OIDC:ClientSecret", "test");

        builder.ConfigureServices(services =>
        {
            services
                .AddAuthentication(opt =>
                {
                    opt.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    opt.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    opt.DefaultScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });
        });
    }
}
