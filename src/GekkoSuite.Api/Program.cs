using GekkoSuite.Api.Configuration;
using GekkoSuite.Api.Repositories;
using GekkoSuite.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Local-only overrides (gitignored — see .gitignore) so a developer sets the JWT secret / connection
// string once here instead of exporting env vars every session. Optional: absent in every real
// deployment, where these come from the environment/secret store instead (see the fallbacks below).
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSingleton<IOrganizationService, OrganizationService>();
builder.Services.AddSingleton<IAuthService, AuthService>();

// Postgres connection string: env var first (12-factor / containers), config fallback.
var connectionString =
    Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "No Postgres connection string. Set POSTGRES_CONNECTION_STRING or ConnectionStrings:Postgres.");

// NpgsqlDataSource IS the pooled connection source — registered once, injected into repositories.
builder.Services.AddNpgsqlDataSource(connectionString);
builder.Services.AddSingleton<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();

// JWT signing settings: env var first (same 12-factor pattern as the connection string above), config
// fallback. The secret must never be checked in — set it via the environment in every real deployment.
var jwtOptions = new JwtOptions
{
    Secret =
        Environment.GetEnvironmentVariable("JWT_SECRET")
        ?? builder.Configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("No JWT secret. Set JWT_SECRET or Jwt:Secret."),
    Issuer =
        Environment.GetEnvironmentVariable("JWT_ISSUER")
        ?? builder.Configuration["Jwt:Issuer"]
        ?? "gekkosuite-api",
    Audience =
        Environment.GetEnvironmentVariable("JWT_AUDIENCE")
        ?? builder.Configuration["Jwt:Audience"]
        ?? "gekkosuite-clients",
    AccessTokenLifetimeMinutes = 15
};
builder.Services.AddSingleton(jwtOptions);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
