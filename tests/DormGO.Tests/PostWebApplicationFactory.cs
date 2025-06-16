using System.Net;
using DormGO.Models;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Sdk;

namespace DormGO.Tests;

public class PostWebApplicationFactory : WebApplicationFactory<Program>
{
    public ApplicationUser? TestUser { get; private set; }
    public ApplicationUser? TestHubUser { get; private set; }
    private static string TestPassword => "P@ssw0rd123!";
    
    private readonly MsSqlContainerFixture _fixture;
    public PostWebApplicationFactory(IMessageSink sink)
    {   
        _fixture = new MsSqlContainerFixture(sink);
        _fixture.Container.StartAsync().GetAwaiter().GetResult();
    }

    public async Task InitializeUsersAsync()
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existingTestUser = await userManager.FindByNameAsync("testuser@example.com");
        if (existingTestUser == null)
        {
            TestUser = new ApplicationUser { UserName = "testuser@example.com", Email = "testuser@example.com", EmailConfirmed = true };
            var result = await userManager.CreateAsync(TestUser, TestPassword);
            if (!result.Succeeded) throw new InvalidOperationException($"Failed to create test user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
        else { TestUser = existingTestUser; }
        
        var existingHubUser = await userManager.FindByNameAsync("testhub@example.com");
        if (existingHubUser == null)
        {
            TestHubUser = new ApplicationUser { UserName = "testhub@example.com", Email = "testhub@example.com", EmailConfirmed = true };
            var result = await userManager.CreateAsync(TestHubUser, TestPassword);
            if (!result.Succeeded) throw new InvalidOperationException($"Failed to create hub user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
        else { TestHubUser = existingHubUser; }
    }

    public async Task<HubConnection> InitializeTestHubConnectionAsync(Uri baseUri)
    {
        var jwtToken = TokenHelper.GenerateJwt(TestHubUser!.Id, TestHubUser.Email,
            TestHubUser.EmailConfirmed.ToString(), DateTime.UtcNow.AddMinutes(30));
        const string simulatedIpAddress = "192.168.1.100";
        var hubUri = new Uri(baseUri, "api/posthub");
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUri, conf =>
            {
                conf.Headers.Add("Authorization", $"Bearer {jwtToken}");
                conf.HttpMessageHandlerFactory = _ => new SimulatedForwardedForHandler(
                    Server.CreateHandler(),
                    simulatedIpAddress);
            })
            .Build();
        await connection.StartAsync();
        return connection;
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:IdentityConnection", _fixture.ConnectionString);

        builder.ConfigureServices(services =>
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownProxies.Add(IPAddress.Loopback);
            });
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await _fixture.Container.DisposeAsync();
        await base.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}