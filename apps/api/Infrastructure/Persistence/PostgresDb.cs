using Microsoft.EntityFrameworkCore;

namespace LearnForge.Api;

public sealed class PostgresDb(DbContextOptions<PostgresDb> options) : AppDb(options);
