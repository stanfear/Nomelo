#:sdk Aspire.AppHost.Sdk@13.3.3
#:package Aspire.Hosting.AppHost@13.3.3
#:package Aspire.Hosting.PostgreSQL@9.5.0
#:package CommunityToolkit.Aspire.Hosting.NodeJS.Extensions@9.9.0

var builder = DistributedApplication.CreateBuilder(args);

builder.Build().Run();
