using GekkoSuite.Database.Services;

// Standalone migration runner. Run manually (or from CI) as the DB owner role — NOT the app.
//   dotnet run migrate
var action = args.FirstOrDefault();
if (action != "migrate")
{
    Console.Error.WriteLine("Usage: GekkoSuite.Database migrate");
    return 1;
}

return MigrationService.Run();
