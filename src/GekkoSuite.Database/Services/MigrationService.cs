using System.Reflection;
using System.Text.RegularExpressions;
using DbUp;
using Microsoft.Extensions.Configuration;

namespace GekkoSuite.Database.Services;

/// <summary>
/// Runs DbUp migrations against the configured Postgres database, with a double confirmation.
/// Intended to be executed manually (or by CI) as the DB owner role — never by the running app.
/// </summary>
public static class MigrationService
{
    // Replace the Password value so it isn't echoed to the console.
    private static string MaskPassword(string connectionString)
    {
        return Regex.Replace(connectionString, @"(Password\s*=\s*)[^;]*", "$1****", RegexOptions.IgnoreCase);
    }
    /// <summary>Runs migrations. Returns a process exit code (0 = success, non-zero = failure/abort).</summary>
    public static int Run()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .Build();

        var connectionString = config.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("Missing ConnectionStrings:Postgres in appsettings.Local.json.");
            return 1;
        }

        // Confirm before touching the database. Show the target (password masked) and require YES twice.
        Console.WriteLine("About to run migrations against:");
        Console.WriteLine($"  {MaskPassword(connectionString)}");
        Console.Write("Type YES to continue: ");
        if (Console.ReadLine() != "YES")
        {
            Console.WriteLine("Aborted.");
            return 1;
        }
        Console.Write("Are you sure? Type YES again to confirm: ");
        if (Console.ReadLine() != "YES")
        {
            Console.WriteLine("Aborted.");
            return 1;
        }

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            Console.Error.WriteLine($"Migration failed: {result.Error}");
            return 1;
        }

        Console.WriteLine("Migrations applied successfully.");
        return 0;
    }


}
