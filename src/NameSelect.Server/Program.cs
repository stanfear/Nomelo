using Microsoft.EntityFrameworkCore;
using NameSelect.Server.Data;
using NameSelect.Server.Lists;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<ListFileLoader>();
builder.Services.AddScoped<ListDirectoryScanner>();
builder.Services.AddHostedService<ListRegistrarHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
