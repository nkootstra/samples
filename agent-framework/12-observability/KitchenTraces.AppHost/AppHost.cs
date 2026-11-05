var builder = DistributedApplication.CreateBuilder(args);

var apiKey = builder.AddParameter("anthropic-api-key", secret: true);

builder.AddProject<Projects.KitchenTraces>("kitchen-traces")
    .WithEnvironment("ANTHROPIC_API_KEY", apiKey)
    .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName);

builder.Build().Run();
