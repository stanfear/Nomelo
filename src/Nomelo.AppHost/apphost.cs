#:sdk Aspire.AppHost.Sdk@13.3.3
#:package Aspire.Hosting.AppHost@13.3.3
#:package Aspire.Hosting.PostgreSQL@9.5.0
#:package CommunityToolkit.Aspire.Hosting.NodeJS.Extensions@9.9.0

var builder = DistributedApplication.CreateBuilder(args);

// Postgres. Aspire publishes ConnectionStrings__<dbName> to consumers, so
// naming the database resource "default" surfaces as ConnectionStrings:Default
// in the Nomelo.Server configuration (the existing Program.cs reads "Default").
var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume("nomelo-pgdata")
    .WithPgAdmin();

var db = postgres.AddDatabase("default", databaseName: "nomelo");

// Nomelo backend (existing ASP.NET project, referenced by csproj path).
// Single-file AppHost uses the string-path overload of AddProject rather than
// the Projects.<Type> overload used by csproj-based AppHosts.
var server = builder
    .AddProject("server", "../Nomelo.Server/Nomelo.Server.csproj")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
