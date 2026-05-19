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

// Rauthy. Self-hosted OIDC provider, default container port is 8080. We
// expose it on host port 8081 and advertise that as PUB_URL so the issued
// tokens carry the correct issuer URL for the server to validate against.
// HQL_INSECURE_COOKIE allows http cookies in dev (production must use TLS).
var rauthyAdminPassword = builder.AddParameter("rauthy-admin-password", secret: true);
var rauthyClientSecret = builder.AddParameter("rauthy-nomelo-client-secret", secret: true);

var rauthy = builder
    .AddContainer("rauthy", "ghcr.io/sebadob/rauthy", "latest")
    .WithHttpEndpoint(port: 8081, targetPort: 8080, name: "http")
    .WithEnvironment("PUB_URL", "localhost:8081")
    .WithEnvironment("LISTEN_SCHEME", "http")
    .WithEnvironment("LISTEN_PORT_HTTP", "8080")
    .WithEnvironment("BOOTSTRAP_ADMIN_EMAIL", "admin@nomelo.local")
    .WithEnvironment("BOOTSTRAP_ADMIN_PASSWORD_PLAIN", rauthyAdminPassword)
    .WithEnvironment("HQL_INSECURE_COOKIE", "true")
    .WithVolume("nomelo-rauthy-data", "/app/data");

var rauthyEndpoint = rauthy.GetEndpoint("http");

// Nomelo backend (existing ASP.NET project, referenced by csproj path).
// Single-file AppHost uses the string-path overload of AddProject rather than
// the Projects.<Type> overload used by csproj-based AppHosts.
var server = builder
    .AddProject("server", "../Nomelo.Server/Nomelo.Server.csproj")
    .WithReference(db)
    .WaitFor(db)
    .WaitFor(rauthy)
    .WithEnvironment("OIDC__Authority", $"{rauthyEndpoint.Url}/auth/v1")
    .WithEnvironment("OIDC__ClientId", "nomelo")
    .WithEnvironment("OIDC__ClientSecret", rauthyClientSecret);

builder.Build().Run();
