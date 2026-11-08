using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace KitchenBench.Tests;

/// <summary>
/// Starts the bench in memory with a stubbed chat client, so no test calls Claude.
/// The test server reports no remote address, which DevUI treats as a remote
/// caller, so the factory sets the address each request comes from.
/// The type argument only picks the app's assembly: the AppHost has a public
/// Program class too, so <c>Program</c> would be ambiguous here.
/// </summary>
sealed class BenchFactory(string environment, IPAddress? remoteAddress)
    : WebApplicationFactory<PantryTools>
{
    public IChatClient ChatClient { get; } = Substitute.For<IChatClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ANTHROPIC_API_KEY", "not-used");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(ChatClient);
            services.AddSingleton<IStartupFilter>(new RemoteAddress(remoteAddress));
        });
    }

    sealed class RemoteAddress(IPAddress? address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use((context, nextMiddleware) =>
                {
                    context.Connection.RemoteIpAddress = address;
                    return nextMiddleware(context);
                });
                next(app);
            };
    }
}
