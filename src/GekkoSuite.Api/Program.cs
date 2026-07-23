using GekkoSuite.Api.Configuration;
using GekkoSuite.Api.Repositories;
using GekkoSuite.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();


// DB Connection
var connectionString =
    Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "No Postgres connection string. Set POSTGRES_CONNECTION_STRING or ConnectionStrings:Postgres.");

builder.Services.AddNpgsqlDataSource(connectionString);

// Register App Services
builder.Services.AddSingleton<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IOrganizationService, OrganizationService>();
builder.Services.AddSingleton<IAuthService, AuthService>();

// JWT signing settings
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
