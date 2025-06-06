using DormGO.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Tests.Helpers;

public static class TestDbContextFactory
{
    public static (ApplicationContext Db, SqliteConnection Connection) CreateSqliteDbContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlite(connection)
            .Options;

        var context = new ApplicationContext(options);
        context.Database.EnsureCreated();

        return (context, connection);
    }
}
