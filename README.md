# Local Development
- Install docker and docker compose
- Install .Net10
- `cd local` then run `docker compose up` (starts Postgres on `5432` and pgAdmin on `5050`; if you're
  using Adminer instead it's a separate install, typically on `9000`)
- Run the DB migration against local (see **Db Migrations** below)
- Load mock data: run `local/mock_data.sql` (paste it in Adminer/pgAdmin, or `psql ... -f local/mock_data.sql`)
- pgAdmin (DB browser) at localhost:5050
    - email: `admin@gekkosuite.com`, password: `admin123`
    - register a server → host `db` (or `localhost`), port `5432`, username/password both `postgres`,
      database `gekkosuite`
- Adminer, if you have it running separately, at localhost:9000
    - system: `PostgreSQL`
    - server: `db`
    - username / password: both `postgres`
    - database: `gekkosuite`

- **Set a JWT secret before running the API — it will not start without one.** `sensitive/` is
  gitignored, so this file does not exist on a fresh clone; create it yourself:
  1. Create `sensitive/local.ps1` (the folder may not exist yet) with:
     ```powershell
     $env:JWT_SECRET = "any-long-random-string-for-local-dev-only"
     ```
  2. **Dot-source it** (note the leading `. `) every new terminal session before `dotnet run` — just
     running the script sets the variable in a throwaway child process and it's gone immediately:
     ```powershell
     . sensitive\local.ps1
     ```
  3. Verify it's actually set: `$env:JWT_SECRET` should print your value.

  (Alternatively, set `JWT_SECRET` as a permanent Windows user environment variable via `setx JWT_SECRET
  "..."` or System Properties → Environment Variables, then it's picked up automatically in every new
  terminal with no dot-sourcing needed.)
- cd src/GekkoSuite.Api and `dotnet run` (default port `5041` — this is what the UI's `downstreamUrl`
  expects; don't override `--urls` unless you also update the UI's config to match)


## Local dev test account

organization user:

```
{
    "email":    "admin@gekkosuite.com",
    "password": "DevPassword123!"
}
```

store user:

```
{
    "email":    "marcus@gekkosuite.com",
    "password": "Password123!"
}
```

# Db Migrations

The migration runner is `tools/GekkoSuite.Database`. It applies the SQL files in `Migrations/` via DbUp.
You pass the **full connection string** and the **target env** on the command line — nothing is read from a
config file, so the tool runs exactly what you type.

### Add a migration
- Add a numbered SQL file in `tools/GekkoSuite.Database/Migrations/` (e.g. `1_0_1.sql`). They run in order,
  once each, and are tracked so they never re-run.

### Run against local
```
cd tools/GekkoSuite.Database
dotnet run migrate --env local --conn "Host=localhost;Port=5432;Database=gekkosuite;Username=postgres;Password=postgres"
```
Then confirm twice: re-type the env (`local`), then re-paste the exact connection string.

### Run against prod
Prod additionally requires a secret key that must match the contents of `sensitive/db_migrate_secret.txt`
(that folder is gitignored, so the key is never committed):
```
dotnet run migrate --env prod --conn "Host=...;Database=...;Username=...;Password=..." --secret <PROD_KEY>
```
Same double confirmation applies. If the env, the re-pasted connection string, or the secret don't match,
the run aborts before touching the database.

> Safety: the target is always an explicit `--env` + `--conn` (no default, nothing from disk), prod needs
> the secret, and you must re-type the env and re-paste the connection string to proceed — so a prod
> migration can't happen by accident.
