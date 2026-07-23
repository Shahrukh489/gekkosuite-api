using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

using GekkoSuite.Api.Authentication;
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
builder.Services.AddSingleton<IOfferingService, OfferingService>();

// Dev-only CORS: lets the gekkosuite-ui Vite dev server (a different origin) call this API from the
// browser. Scoped to Development so a real deployment doesn't inherit an open policy by accident — prod
// CORS (the real UI's deployed origin) gets configured deliberately when that's known.
const string DevUiCorsPolicy = "DevUiCorsPolicy";
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(DevUiCorsPolicy, policy =>
        {
            policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod();
        });
    });
}

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
builder.Services.AddSingleton<IOfferingRepository, OfferingRepository>();

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

// Validates the access token POST /auth/login issues. Pinned to HS256 explicitly (ValidAlgorithms) so
// neither "alg: none" nor an RS256-signed token can be accepted — closes auth.md's Security Review, R1.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        };

        options.Events = new JwtBearerEvents
        {
            // Cryptographic validation only proves the token hasn't been tampered with and hasn't
            // expired — it says nothing about whether the account is still active. A user disabled a
            // minute ago still holds a perfectly valid token until it expires, so this re-reads
            // user.is_active fresh from the database on every request (see auth.md, §1).
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.GetUserId();
                var organizationId = context.Principal?.GetOrganizationId();
                if (userId is null || organizationId is null)
                {
                    context.Fail("Token is missing a valid userId/organizationId claim.");
                    return;
                }

                var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                var user = await userRepository.FindByIdAsync(organizationId.Value, userId.Value);
                if (user is null || !user.IsActive)
                {
                    context.Fail("Account is inactive or no longer exists.");
                }
            },
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(DevUiCorsPolicy);
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
