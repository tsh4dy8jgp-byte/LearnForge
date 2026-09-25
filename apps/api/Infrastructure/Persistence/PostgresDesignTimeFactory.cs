using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LearnForge.Api;

public sealed class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresDb>
{
    public PostgresDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PostgresDb>()
        .UseNpgsql("Host=localhost;Database=learnforge;Username=learnforge;Password=design-only").Options);
}
