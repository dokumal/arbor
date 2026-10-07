using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using QuireClone.Services;

namespace QuireClone.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var settings = new StorageSettings(Directory.GetCurrentDirectory());
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(settings.ConnectionString).Options;
        return new AppDbContext(options);
    }
}
