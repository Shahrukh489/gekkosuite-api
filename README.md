# Local Development
- Install docker and docker compose
- Install .Net10
- Run `docker compose up`
- In browser go to localhost:9000
    - select system dropdown as `PostgreSql`
    - type server as `db`
    - username and password are both  `postgres`
    - database is `gekkosuite`

# Db Migrations
- cd into GekkoSuite.Database 
- update the database connection string in appsettings.Local.json, make sure to never check this in github
- add migration sql files in Migrations/
- run the project with the command `dotnet run migrate`
- confirm with YES two times