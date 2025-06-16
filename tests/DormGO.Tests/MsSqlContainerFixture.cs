using System.Data.Common;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace DormGO.Tests;

public class MsSqlContainerFixture : DbContainerFixture<MsSqlBuilder, MsSqlContainer>
{
    public MsSqlContainerFixture(IMessageSink messageSink) : base(messageSink) { }
    protected override MsSqlBuilder Configure(MsSqlBuilder builder)
    {
        return builder
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest");
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        var logs = await Container.GetLogsAsync();
        await File.WriteAllTextAsync("TestContainerLogs/sqlcontainer.log", logs.Stdout);
    }

    public override DbProviderFactory DbProviderFactory => SqlClientFactory.Instance;
}