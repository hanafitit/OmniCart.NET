using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OmniCart.Infrastructure;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        
        // Neon Cloud PostgreSQL
        var connectionString = "Host=ep-round-haze-aqts00ty.c-8.us-east-1.aws.neon.tech;" +
                               "Database=neondb;" +
                               "Username=neondb_owner;" +
                               "Password=npg_fv9BLY8IVWrt;" +
                               "SSL Mode=Require;" +
                               "Trust Server Certificate=true;";
        
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
