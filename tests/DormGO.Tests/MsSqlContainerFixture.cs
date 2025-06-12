using System.Data.Common;
using DormGO.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Testcontainers.Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace DormGO.Tests;

public class MsSqlContainerFixture : DbContainerFixture<MsSqlBuilder, MsSqlContainer>
{
    private readonly IMessageSink _messageSink;
    public MsSqlContainerFixture(IMessageSink messageSink) : base(messageSink)
    {
        _messageSink = messageSink;
    }
    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        var connectionString = Container.GetConnectionString();
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationContext>();
        optionsBuilder.UseSqlServer(connectionString);
        await using var dbContext = new ApplicationContext(optionsBuilder.Options);
        await dbContext.Database.MigrateAsync();
        var migrationsMessage = new DiagnosticMessage("Migrations applied to container: {0}", connectionString);
        _messageSink.OnMessage(migrationsMessage);
    }
    protected override MsSqlBuilder Configure(MsSqlBuilder builder)
    {
        return builder
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithName("TestDbContainer")
            .WithPassword("super_secret_password@123+");
    }
    public override DbProviderFactory DbProviderFactory => SqlClientFactory.Instance;
}