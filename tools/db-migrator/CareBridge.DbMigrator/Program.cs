using CareBridge.CaseService.Data;
using CareBridge.CarePlanService.Data;
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

var migrationTargets = new (string Name, string DatabaseKey, Func<DbContext> CreateContext)[]
{
    ("Case Service", "CaseDb", () =>
    {
        var opts = new DbContextOptionsBuilder<CaseDbContext>()
            .UseSqlServer(BuildConnectionString(databases["CaseDb"]))
            .Options;
        return new CaseDbContext(opts);
    }),
    ("Care Plan Service", "CarePlanDb", () =>
    {
        var opts = new DbContextOptionsBuilder<CarePlanDbContext>()
            .UseSqlServer(BuildConnectionString(databases["CarePlanDb"]))
            .Options;
        return new CarePlanDbContext(opts);
    })
};

Console.WriteLine("CareBridge Database Migration Runner");
Console.WriteLine(new string('=', 40));

var hasErrors = false;

foreach (var (name, dbKey, createContext) in migrationTargets)
{
    Console.WriteLine();
    Console.WriteLine($"[{name}]");
    Console.WriteLine($"  Database: {databases[dbKey]}");

    try
    {
        using var context = createContext();

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
