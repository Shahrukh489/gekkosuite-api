using GekkoSuite.Api.Repositories;
using GekkoSuite.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSingleton<IOrganizationService, OrganizationService>();

// Postgres connection string: env var first (12-factor / containers), config fallback.
var connectionString =
    Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "No Postgres connection string. Set POSTGRES_CONNECTION_STRING or ConnectionStrings:Postgres.");

// NpgsqlDataSource IS the pooled connection source — registered once, injected into repositories.
builder.Services.AddNpgsqlDataSource(connectionString);
builder.Services.AddSingleton<IOrganizationRepository, OrganizationRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
