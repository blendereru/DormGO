using DormGO.Data;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Tests.Helpers;

public static class TestDbContextFactory
{
    public static ApplicationContext CreateDbContext()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationContext(dbOptions);
        return context;
    }
}