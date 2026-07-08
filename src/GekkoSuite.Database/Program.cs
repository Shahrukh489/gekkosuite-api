using GekkoSuite.Database.Services;

// Standalone migration runner. Run manually (or from CI) as the DB owner role — NOT the app.
//   dotnet run migrate
var action = args.FirstOrDefault();
if (action == "migrate")
{
    return MigrationService.Run();
}

Console.Error.WriteLine("Unknown Action Argument");
return 1;

