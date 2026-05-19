using Microsoft.EntityFrameworkCore;
using Nomelo.Server.Auth;
using Nomelo.Server.Data;
using Nomelo.Server.Endpoints;
using Nomelo.Server.Lists;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<ListFileLoader>();
builder.Services.AddScoped<ListDirectoryScanner>();
builder.Services.AddHostedService<ListRegistrarHostedService>();

builder.Services.AddNomeloAuth(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();
app.MapListsEndpoints();
app.MapSessionsEndpoints();

app.Run();

public partial class Program;
