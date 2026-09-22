using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using QuireClone.Services;

namespace QuireClone.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? LocalPostgres.TryGetExistingConnectionString(Directory.GetCurrentDirectory())
            ?? "Host=localhost;Port=5433;Database=quireclone;Username=quireclone;Password=quireclone";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
        return new AppDbContext(options);
    }
}
