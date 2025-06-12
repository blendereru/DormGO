using DormGO.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Sdk;

namespace DormGO.Tests;

public class CustomWebApplicationFactoryFixture<TProgram> : WebApplicationFactory<TProgram>, IAsyncLifetime
    where TProgram : class
{
    public ApplicationUser TestUser { get; private set; }
    private static string TestUserPassword => "P@ssw0rd123!";
    
    private readonly MsSqlContainerFixture _msSqlFixture;
    private readonly IMessageSink _messageSink;
    public CustomWebApplicationFactoryFixture(IMessageSink messageSink)
    {
        _msSqlFixture = new MsSqlContainerFixture(messageSink);
        _messageSink = messageSink;
    }
    
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionString:TestConnection", _msSqlFixture.ConnectionString);
    }

    public async ValueTask InitializeAsync()
    {
        await ((IAsyncLifetime)_msSqlFixture).InitializeAsync();
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existingUser = await userManager.FindByNameAsync("testuser@example.com");
        if (existingUser == null)
        {
            TestUser = new ApplicationUser
            {
                UserName = "testuser@example.com",
                Email = "testuser@example.com",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(TestUser, TestUserPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create test user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            TestUser = existingUser;
        }
    }
    public override async ValueTask DisposeAsync()
    {
        await ((IAsyncLifetime)_msSqlFixture).DisposeAsync();
        await base.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}