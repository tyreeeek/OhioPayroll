using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Data.Sqlite;

namespace OhioPayroll.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PayrollDbContext>
{
    public PayrollDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PayrollDbContext>();
        optionsBuilder.UseSqlite("Data Source=design_time.db");
        return new PayrollDbContext(optionsBuilder.Options);
    }
}
