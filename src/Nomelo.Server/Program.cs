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

builder.Services.AddNomeloAuth(builder.Configuration);

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

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapListsEndpoints();
app.MapSessionsEndpoints();
app.MapVotingEndpoints();
app.MapShareEndpoints();

app.Run();

public partial class Program;
