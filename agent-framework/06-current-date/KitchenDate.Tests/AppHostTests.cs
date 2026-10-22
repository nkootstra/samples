using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace KitchenDate.Tests;

public sealed class AppHostTests
{
    [Fact]
    public async Task The_app_gets_the_key_parameter_as_ANTHROPIC_API_KEY()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.KitchenDate_AppHost>(TestContext.Current.CancellationToken);
        var app = appHost.CreateResourceBuilder<ProjectResource>("kitchen-date");

        var configuration = await ExecutionConfigurationBuilder
            .Create(app.Resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(
                new(DistributedApplicationOperation.Publish),
                NullLogger.Instance,
                TestContext.Current.CancellationToken);

        Assert.Equal(
            "{anthropic-api-key.value}",
            configuration.EnvironmentVariables.ToDictionary()["ANTHROPIC_API_KEY"]);
    }

    [Fact]
    public async Task The_key_parameter_is_a_secret()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.KitchenDate_AppHost>(TestContext.Current.CancellationToken);

        var key = appHost.CreateResourceBuilder<ParameterResource>("anthropic-api-key");

        Assert.True(key.Resource.Secret);
    }
}
