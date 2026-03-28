using CareBridge.CaseService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var host = configuration["SqlServer:Host"] ?? "localhost,1433";
var userId = configuration["SqlServer:UserId"] ?? "sa";
var password = configuration["SQL_SA_PASSWORD"]
    ?? throw new InvalidOperationException("SQL_SA_PASSWORD environment variable is required. Copy .env.example to .env and source it, or set the variable directly.");

string BuildConnectionString(string database) =>
    $"Server={host};Database={database};User Id={userId};Password={password};TrustServerCertificate=True";

var databases = configuration.GetSection("Databases").GetChildren()
    .ToDictionary(c => c.Key, c => c.Value!);

var migrationTargets = new (string Name, string DatabaseKey, Action<DbContextOptionsBuilder> Configure)[]
{
    ("Case Service", "CaseDb", opts => opts.UseSqlServer(BuildConnectionString(databases["CaseDb"])))
};

Console.WriteLine("CareBridge Database Migration Runner");
Console.WriteLine(new string('=', 40));

var hasErrors = false;

foreach (var (name, dbKey, configure) in migrationTargets)
{
    Console.WriteLine();
    Console.WriteLine($"[{name}]");
    Console.WriteLine($"  Database: {databases[dbKey]}");

    try
    {
        var optionsBuilder = new DbContextOptionsBuilder<CaseDbContext>();
        configure(optionsBuilder);

        using var context = new CaseDbContext(optionsBuilder.Options);

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
