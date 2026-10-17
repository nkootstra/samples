var builder = DistributedApplication.CreateBuilder(args);

var apiKey = builder.AddParameter("anthropic-api-key", secret: true);

builder.AddProject<Projects.KitchenTools>("kitchen-tools")
    .WithEnvironment("ANTHROPIC_API_KEY", apiKey);

builder.Build().Run();
