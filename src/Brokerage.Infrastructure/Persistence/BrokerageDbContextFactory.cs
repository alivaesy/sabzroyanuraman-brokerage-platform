using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Brokerage.Infrastructure.Persistence;

public sealed class BrokerageDbContextFactory : IDesignTimeDbContextFactory<BrokerageDbContext>
{
    public BrokerageDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BrokerageDbContext>();
        optionsBuilder.UseSqlite("Data Source=brokerage.design-time.db");

        return new BrokerageDbContext(optionsBuilder.Options);
    }
}
