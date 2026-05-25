using System.IO.Compression;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Nomelo.Server.Auth;
using Nomelo.Server.Data;
using Nomelo.Server.Endpoints;
using Nomelo.Server.Infrastructure;
using Nomelo.Server.Lists;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services
    .AddOptions<ListsOptions>()
    .Bind(builder.Configuration.GetSection(ListsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<ListFileLoader>();
builder.Services.AddSingleton<ListCache>();
builder.Services.AddScoped<ListDirectoryScanner>();
builder.Services.AddHostedService<ListRegistrarHostedService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<Nomelo.Server.Voting.VoteProcessor>();
builder.Services.AddScoped<Nomelo.Server.Voting.NextPairService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

// Honor X-Forwarded-* headers from the reverse proxy that terminates TLS in
// front of the container (Caddy/Traefik/nginx/Authentik forward auth). Without
// this, the OIDC middleware builds the redirect_uri using the in-container
// scheme (http) instead of the public one (https), and providers like Authentik
// reject the mismatch in Strict mode. KnownNetworks/Proxies are cleared so the
// container trusts whatever proxy is on the docker network — production exposes
// only the proxy to the outside, so this is safe in a single-host setup.
builder.Services.Configure<ForwardedHeadersOptions>(opt =>
{
    opt.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    opt.KnownNetworks.Clear();
    opt.KnownProxies.Clear();
});

// Brotli + gzip response compression for API payloads. Large /results
// responses (multi-thousand-item rankings) compress 5-8x and dominate the
// page load time on slow connections.
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
    o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "application/javascript",
        "text/css",
        "image/svg+xml",
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

builder.Services.AddNomeloAuth(builder.Configuration, builder.Environment);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var logger = sp.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database migration failed at startup");
        throw;
    }
}

// ForwardedHeaders MUST run before any middleware that reads the request scheme
// or host (auth, redirects, link generation).
app.UseForwardedHeaders();

// Compression sits before static files and endpoints so both bundled assets
// and JSON API responses get compressed in a single pass.
app.UseResponseCompression();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapListsEndpoints();
app.MapSessionsEndpoints();
app.MapVotingEndpoints();
app.MapShareEndpoints();

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
