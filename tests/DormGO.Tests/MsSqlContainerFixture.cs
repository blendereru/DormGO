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
    public MsSqlContainerFixture(IMessageSink messageSink) : base(messageSink) { }
    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        var connectionString = Container.GetConnectionString();
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationContext>();
        optionsBuilder.UseSqlServer(connectionString);
    }
    protected override MsSqlBuilder Configure(MsSqlBuilder builder)
    {
        return builder
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest");
    }
    public override DbProviderFactory DbProviderFactory => SqlClientFactory.Instance;
}