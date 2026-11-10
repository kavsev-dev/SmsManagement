# Database artifacts

Everything that defines or populates the SMS Management database lives in this folder.

| Path | Contents |
|------|----------|
| `scripts/000_create_database.sql` | Creates the `SmsManagement` database with the Hebrew collation `Hebrew_100_CI_AS_SC`. Run it connected to `master`. |
| `scripts/001_create_schema.sql` | Idempotent schema script, generated from the migrations (`dotnet ef migrations script --idempotent`). Do not edit it by hand. |
| `scripts/002_seed_employees.sql` | Idempotent seed of the mock employees (Hebrew names and addresses) and their phone numbers. |
| `migrations/` | EF Core migrations and model snapshot. They are compiled into `SmsManagement.Infrastructure` through a `<Compile Include>` link in its `.csproj`. |
| `add-migration.ps1` | Helper for adding a migration. It also regenerates `001_create_schema.sql`. |

## Hebrew support

- **The collation is set at database level only**, to `Hebrew_100_CI_AS_SC`:
  - `Hebrew_100`: the newest Hebrew collation version in SQL Server 2022.
  - `CI`: case-insensitive, for any Latin text mixed with the Hebrew.
  - `AS`: accent-sensitive, so text with niqqud is distinct from text without it.
  - `SC`: supplementary characters, so emoji count as one character.
- **No column overrides the collation**, so every text column inherits it.
- **All text columns are `nvarchar`** (UTF-16), so Hebrew is stored without loss. The collation decides how
  text sorts and compares.
- **The database collation is set in two places:**
  - When the app creates the database (EF Core runs `CREATE DATABASE ... COLLATE Hebrew_100_CI_AS_SC` from
    `modelBuilder.UseCollation`).
  - In `000_create_database.sql`, for manual provisioning.
- **`000_create_database.sql` fails if the database already exists with a different collation**, because the
  tables would inherit the wrong one. At startup the app logs a warning in the same situation.
- **The scripts are UTF-8 with BOM.** Always pass `-f 65001` to `sqlcmd`, or the Hebrew literals may be read in
  the client's ANSI code page and corrupted.
- **The seed script's temporary table uses `COLLATE DATABASE_DEFAULT`.** Table variables live in `tempdb` and
  would otherwise take the *server* collation, which causes collation-conflict errors on servers whose default
  is not Hebrew.

## How the database is provisioned

### Automatically, on application startup (the default, and what `dotnet run` uses)

`IServiceProvider.InitializeDatabaseAsync()` runs at startup and does three things:

1. **Waits for SQL Server, then applies migrations.** A server that is still starting can take several
   seconds to accept connections; retries are configured by `Database:StartupMaxAttempts` and `Database:StartupRetryDelaySeconds`. If the
   database does not exist, EF Core creates it with the Hebrew collation.
2. **Checks the database collation** and logs a warning if it is not `Hebrew_100_CI_AS_SC`.
3. **Seeds employees** from `EmployeeSeedData`. The seed is idempotent: it inserts only missing employees
   (matched on the natural key) and missing phone numbers.

The migration and seed steps can be switched off with `Database:ApplyMigrationsOnStartup` and
`Database:SeedOnStartup`.

### Manually, by a DBA

Run the scripts in order:

```bash
sqlcmd -S <server> -d master        -f 65001 -i db/scripts/000_create_database.sql
sqlcmd -S <server> -d SmsManagement -f 65001 -i db/scripts/001_create_schema.sql
sqlcmd -S <server> -d SmsManagement -f 65001 -i db/scripts/002_seed_employees.sql
```

Each script can be run again safely.

## Schema notes

- **Employees** have no business identifier, because of security restrictions. Their natural identity is
  enforced by the unique index `UX_Employees_FirstName_LastName_Address`.
- **EmployeePhones** store numbers in E.164 form, e.g. `+972501234567`. A number may belong to several
  employees, so `IX_EmployeePhones_PhoneNumber` is deliberately **not** unique. One employee cannot list the
  same number twice (`UX_EmployeePhones_EmployeeId_PhoneNumber`).
- **Messages** store `Status` as a string, restricted by `CK_Messages_Status`. The indexes on
  `(Status, CreatedAt DESC)` and `(CreatedAt DESC)` serve `GET /messages`, with and without the status filter.

## Adding a migration

```powershell
./db/add-migration.ps1 -Name <MigrationName>
```

Run the script instead of calling `dotnet ef migrations add` directly. `dotnet ef` would write the model
snapshot inside the Infrastructure project rather than here, and the script moves it back.

## Testing against SQL Server

The default test run uses SQLite and needs no SQL Server. To also run the SQL Server tests, set a connection
string; the database name in it is ignored, because each test creates and drops its own database. These tests
check the collation, Hebrew round-trips and the real migration path.

```bash
SMS_SQLSERVER_TEST_CONNECTION="Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True" dotnet test
```
