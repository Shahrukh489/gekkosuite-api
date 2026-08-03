# Local Development
- Install docker and docker compose
- Install .Net10
- `cd local` then run `docker compose up` (starts Postgres on `5432` and Adminer on `9000`)
- Run the DB migration against local (see **Db Migrations** below)
- Load mock data: run `local/mock_data.sql` (paste it in Adminer, or `psql ... -f local/mock_data.sql`)
- Adminer (DB browser) at localhost:9000
    - system: `PostgreSQL`
    - server: `db`
    - username / password: both `postgres`
    - database: `gekkosuite`

- run script sensitive\local.env.ps1
- cd api/GekkoSuite.Api and "dotnet run"


## Local dev test accounts

All accounts below are seeded by `local/mock_data.sql`.

| Email | Password | Scope | Role | Store |
| --- | --- | --- | --- | --- |
| `admin@gekkosuite.com` | `DevPassword123!` | Organization | Org Admin (owner) | — |
| `steven@gekkosuite.com` | `Password123!` | Organization | Organization Reader | — |
| `rico@gekkosuite.com` | `Password123!` | Organization | Organization Inventory Manager | — |
| `marcus@gekkosuite.com` | `Password123!` | Store | Store Manager | Downtown + Online |
| `john@gekkosuite.com` | `Password123!` | Store | Cashier | Downtown |
| `nadia@gekkosuite.com` | `Password123!` | Store | Cashier | Online |

> Every account except `admin` shares the password `Password123!`.

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
