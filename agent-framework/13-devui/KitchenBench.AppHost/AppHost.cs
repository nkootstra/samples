var builder = DistributedApplication.CreateBuilder(args);

var apiKey = builder.AddParameter("anthropic-api-key", secret: true);

builder.AddProject<Projects.KitchenBench>("kitchen-bench")
    .WithEnvironment("ANTHROPIC_API_KEY", apiKey)
    .WithUrlForEndpoint("http", url =>
    {
        url.Url = "/devui/";
        url.DisplayText = "DevUI";
    });

builder.Build().Run();
