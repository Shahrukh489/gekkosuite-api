using System.Reflection;
using DbUp;

namespace GekkoSuite.Database.Services;

/// <summary>
/// Runs DbUp migrations against a connection string the operator passes in full. Nothing is read from a
/// config file, so the tool runs exactly what was typed. Before applying, the operator must confirm three
/// times — re-type the environment, re-paste the exact connection string, then acknowledge the action is
/// undoable — so an autopilot run or a wrong target aborts. A prod run additionally shows a red warning and
/// requires the correct secret key (matched against the contents of sensitive/db_migrate_secret.txt, a
/// gitignored file). Intended to be run manually (or by CI) as the DB owner role — never by the running app.
/// </summary>
public static class MigrationService
{
    /// <summary>
    /// Runs migrations against the given connection string for the named environment.
    /// </summary>
    /// <param name="env">The target environment: "local" or "prod".</param>
    /// <param name="connectionString">The full Postgres connection string to migrate.</param>
    /// <param name="secret">The prod secret key supplied via --secret; required (and checked) only when env is "prod".</param>
    /// <returns>A process exit code (0 = success, non-zero = failure/abort).</returns>
    public static int Run(string env, string connectionString, string? secret)
    {
        if (env != "local" && env != "prod")
        {
            Console.Error.WriteLine($"Unknown env '{env}'. Use 'local' or 'prod'.");
            return 1;
        }

        // A prod run must present the secret key; anything else aborts before we touch the database.
        if (env == "prod" && !IsProdSecretValid(secret))
        {
            Console.Error.WriteLine("Prod migration requires a valid --secret (must match sensitive/db_migrate_secret.txt). Aborted.");
            return 1;
        }

        // Loud red banner so a prod run is impossible to mistake for a local one.
        if (env == "prod")
        {
            WriteRed("=================================================");
            WriteRed("  WARNING: you are migrating the PRODUCTION database");
            WriteRed("=================================================");
        }

        // Double confirmation: re-type the env, then re-paste the exact connection string. Both must match
        // what was passed on the command line, so an autopilot run or a wrong target aborts here.
        Console.WriteLine($"About to run migrations against [{env}].");
        Console.Write($"Confirm the environment (type '{env}'): ");
        if (Console.ReadLine() != env)
        {
            Console.WriteLine("Environment did not match. Aborted.");
            return 1;
        }

        Console.Write("Re-paste the exact connection string to confirm: ");
        if (Console.ReadLine() != connectionString)
        {
            Console.WriteLine("Connection string did not match. Aborted.");
            return 1;
        }

        // Final gate: migrations are not automatically reversible, so make the operator acknowledge it.
        Console.WriteLine("This action is undoable");
        Console.Write("Please type in \"I understandd what I am do1ng\": ");
        if (Console.ReadLine() != "I understandd what I am do1ng")
        {
            Console.WriteLine("Acknowledgement did not match. Aborted.");
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

    /// <summary>
    /// Writes a line to the console in red, then restores the previous color.
    /// </summary>
    /// <param name="message">The text to write.</param>
    private static void WriteRed(string message)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ForegroundColor = previous;
    }

    /// <summary>
    /// Checks the supplied prod secret against the contents of the project-root sensitive/db_migrate_secret.txt
    /// (gitignored, never committed). Crashes if the file is missing — the secret must exist to migrate prod.
    /// </summary>
    /// <param name="secret">The secret passed on the command line via --secret.</param>
    /// <returns>True if a secret was supplied and it matches the file's contents; false otherwise.</returns>
    private static bool IsProdSecretValid(string? secret)
    {
        // BaseDirectory is <root>/tools/GekkoSuite.Database/bin/Debug/net10.0, so the project root is five
        // levels up. The secret lives ONLY at <root>/sensitive/db_migrate_secret.txt.
        var secretPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "sensitive", "db_migrate_secret.txt");
        var expected = File.ReadAllText(secretPath).Trim();

        return !string.IsNullOrWhiteSpace(secret) && secret == expected;
    }
}
