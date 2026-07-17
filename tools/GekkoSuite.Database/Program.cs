using GekkoSuite.Database.Services;

// Standalone migration runner. Run manually (or from CI) as the DB owner role — NOT the app.
// The operator types the FULL connection string and the target env; nothing is read from a config file,
// so the tool runs exactly what you pass. A prod run additionally requires the prod secret key, so it
// can never happen by accident.
//   dotnet run migrate --env local --conn "Host=localhost;Port=5432;Database=gekkosuite;Username=postgres;Password=postgres"
//   dotnet run migrate --env prod  --conn "Host=...;Database=...;..." --secret <PROD_KEY>
var action = args.FirstOrDefault();
if (action != "migrate")
{
    Console.Error.WriteLine("Unknown action. Usage: migrate --env <local|prod> --conn <connectionString> [--secret <key>]");
    return 1;
}

var env = GetArg(args, "--env");
var connectionString = GetArg(args, "--conn");
var secret = GetArg(args, "--secret");

if (env is null || connectionString is null)
{
    Console.Error.WriteLine("Usage: migrate --env <local|prod> --conn <connectionString> [--secret <key>]");
    return 1;
}

return MigrationService.Run(env, connectionString, secret);

// Reads the value after the given flag from the args, or null if the flag is absent / has no value.
static string? GetArg(string[] args, string flag)
{
    var index = Array.IndexOf(args, flag);
    if (index >= 0 && index + 1 < args.Length)
    {
        return args[index + 1];
    }
    return null;
}
