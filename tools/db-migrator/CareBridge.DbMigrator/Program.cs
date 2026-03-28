using CareBridge.CaseService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var migrationTargets = new (string Name, string ConnectionStringKey, Action<DbContextOptionsBuilder> Configure)[]
{
    ("Case Service", "CaseDb", opts => opts.UseSqlServer(configuration.GetConnectionString("CaseDb")))
};

Console.WriteLine("CareBridge Database Migration Runner");
Console.WriteLine(new string('=', 40));

var hasErrors = false;

foreach (var (name, connKey, configure) in migrationTargets)
{
    Console.WriteLine();
    Console.WriteLine($"[{name}]");

    var connectionString = configuration.GetConnectionString(connKey);
    if (string.IsNullOrEmpty(connectionString))
    {
        Console.WriteLine($"  WARNING: No connection string found for '{connKey}'. Skipping.");
        continue;
    }

    try
    {
        var optionsBuilder = new DbContextOptionsBuilder<CaseDbContext>();
        configure(optionsBuilder);

        using var context = new CaseDbContext(optionsBuilder.Options);

        Console.WriteLine($"  Database: {context.Database.GetConnectionString()?.Split(';').FirstOrDefault(s => s.TrimStart().StartsWith("Database="))?.Trim() ?? "unknown"}");

        await context.Database.EnsureCreatedAsync();

        var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            Console.WriteLine($"  Applying {pending.Count} migration(s):");
            foreach (var migration in pending)
            {
                Console.WriteLine($"    - {migration}");
            }
            await context.Database.MigrateAsync();
            Console.WriteLine("  Done.");
        }
        else
        {
            Console.WriteLine("  Up to date.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ERROR: {ex.Message}");
        hasErrors = true;
    }
}

Console.WriteLine();
Console.WriteLine(hasErrors ? "Completed with errors." : "All migrations applied successfully.");
return hasErrors ? 1 : 0;
