### Technical Specification: SMS Management System

> **About this document.** I, Kseniia Salatanov, wrote this specification from the assignment requirements, with
> the help of NotebookLM. It is written as instructions to the AI that generated the code. I made the later
> decisions during development with Claude Code, which also generated the implementation.
>
> *  Sections 1–8 describe the system as built.
> *  Section 9 lists every decision added during development, with its reason, including where it changed my
>    original specification.

#### Instructions to AI & Mission Statement
You're a Senior Full Stack Developer creating an enterprise-grade, highly maintainable RESTful API and a light
visualization dashboard for an internal SMS Management System using .NET 10 / ASP.NET Core 10, Entity Framework
Core and SQL Server.

##### Implementation directives
*  Scaffold with standard `dotnet new` commands a 4-project solution `SmsManagement.sln`:
    *  `SmsManagement.Api`: Web API, controllers, `wwwroot/index.html` dashboard host, OpenAPI configuration.
    *  `SmsManagement.Core`: domain entities, DTOs, interfaces (`ISmsProvider`), validation rules.
    *  `SmsManagement.Infrastructure`: `ApplicationDbContext`, EF Core configurations, `SmsProviderFactory`,
       `MockSmsProvider`, `SecondarySmsProvider`.
    *  `SmsManagement.Tests`: xUnit, FluentAssertions, NSubstitute, `WebApplicationFactory` integration tests.
*  All database creation scripts, seed data and EF Core migrations live in a dedicated root folder `/db/`
   (`/db/scripts/`, `/db/migrations/`), never in the repository root.
*  Structured, compilable code without placeholders, stubs or `// TODO` comments in interfaces or business logic.

--------------------------------------------------------------------------------

#### 1. System Overview & Domain Rules
The application sends, tracks and filters SMS messages sent to agricultural/institutional employees.

##### Core Domain Entities & Rules:
1.  **Employee Identity & Security Constraints:**
    *  Employees **do NOT** have an Employee ID, due to security restrictions.
    *  The unique natural composite identity of an employee is FirstName + LastName + Address.
    *  An employee can have **multiple phone numbers**. Phone numbers are not globally unique across employees
       (e.g. a shared office line), but one employee cannot list the same number twice.
    *  Employee phone numbers are stored in E.164 form (e.g. `+972501234567`).
2.  **Message Management:**
    *  Each message is validated before sending:
        *  To: valid E.164 or Israeli phone number (e.g. `+972501234567` or `0501234567`). Spaces and hyphens
           used as separators are ignored. The number is stored as entered (without separators).
        *  Body: non-empty (not only whitespace), max 160 characters (single SMS payload limit), measured as
           `string.Length`.
        *  Provider: **optional**. `Mock` or `Secondary` (case-insensitive); when omitted, `Mock` is used.
    *  Initial status upon creation: Pending.
    *  Updated status after the dispatch attempt: Sent (the provider accepted the message) or Failed.
    *  A recipient that matches no employee is **still sent**; a warning is logged.

--------------------------------------------------------------------------------

#### 2. Architecture & Multiple Providers Pattern
The SMS delivery layer implements the **Strategy & Provider Factory** pattern: the caller can explicitly select
the provider for each message.

##### Provider Interface:
*  `ISmsProvider`:
    *  `string ProviderName { get; }`
    *  `Task<SmsDeliveryResult> SendSmsAsync(string to, string body, CancellationToken cancellationToken)`
*  Providers do not throw for delivery problems (unreachable endpoint, rejection, timeout, malformed response);
   they return a failure result. Only cancellation propagates as an exception.
*  Sends are **not retried** automatically, so the same SMS is never sent twice.

##### Concrete Implementations:
1.  **MockSmsProvider (Default / Primary):**
    *  Identifier: `Mock`.
    *  Sends each message to the simulated SMS provider as a JSON `POST /api/sms/send`.
    *  Its base URL comes from configuration (default `http://localhost:5000`).
2.  **SecondarySmsProvider (Alternative Provider):**
    *  Identifier: `Secondary`.
    *  Simulates a Twilio-style API to demonstrate a provider with a different API: form-encoded
       `POST /2010-04-01/Accounts/{AccountSid}/Messages.json`, Basic authentication, E.164 recipients (national
       numbers are converted), and a message resource with a lifecycle `status`.

##### Factory Pattern:
*  `SmsProviderFactory` exposes `ISmsProvider GetProvider(string providerName)` and resolves the selected strategy
   by name (case-insensitive) from all providers registered in dependency injection.
*  There is **no automatic failover** between providers; the provider is chosen explicitly, or defaults to `Mock`.

##### Send flow (`MessageService`):
1.  Validate the request.
2.  Look up the employees who own the recipient number. Only the match count is logged; the names are returned in
    the response (`employeeNames`).
3.  Store the message as `Pending`.
4.  Dispatch it through the selected provider.
5.  Store the outcome (`Sent` or `Failed`), even if the client disconnects mid-request.

--------------------------------------------------------------------------------

#### 3. Database & Persistence Layer (.NET 10 + EF Core + SQL Server)

##### Tables / Schemas:
###### Employees
*  Id (long identity, internal database PK)
*  FirstName (nvarchar(100), Required)
*  LastName (nvarchar(100), Required)
*  Address (nvarchar(255), Required)
*  *Composite Unique Index:* (FirstName, LastName, Address)

###### EmployeePhones
*  Id (long identity, PK)
*  EmployeeId (FK -> Employees.Id, cascade delete)
*  PhoneNumber (nvarchar(20), Required, E.164)
*  *Non-unique index* on PhoneNumber; *unique index* on (EmployeeId, PhoneNumber).

###### Messages
*  Id (uniqueidentifier, PK)
*  To (nvarchar(20), Required)
*  Body (nvarchar(160), Required)
*  Status (nvarchar(20), Pending / Sent / Failed, enforced by a CHECK constraint)
*  ProviderName (nvarchar(50), the provider chosen and used)
*  ErrorMessage (nvarchar(max), Nullable)
*  CreatedAt (datetimeoffset, UTC)
*  SentAt (datetimeoffset, Nullable)
*  Indexes on (Status, CreatedAt DESC) and (CreatedAt DESC) for the list endpoint.
*  **No foreign key to Employees or EmployeePhones.** A message stores the number only.

##### Hebrew support:
*  The database collation is **`Hebrew_100_CI_AS_SC`**, set at **database level only**. Text columns inherit it
   and have no column-level `COLLATE`.
*  All text columns are `nvarchar`, so Hebrew is stored losslessly.

##### Provisioning (`/db/`):
*  `db/scripts/000_create_database.sql`: creates the database with the Hebrew collation. It fails if the database
   already exists with a different collation.
*  `db/scripts/001_create_schema.sql`: idempotent schema script generated from the EF Core migrations.
*  `db/scripts/002_seed_employees.sql`: idempotent seed script.
*  `db/migrations/`: EF Core migrations, compiled into `SmsManagement.Infrastructure`.
*  SQL scripts are UTF-8 with BOM (run with `sqlcmd -f 65001`). The seed script's table variable uses
   `COLLATE DATABASE_DEFAULT`.
*  On startup the API creates the database if needed (with the Hebrew collation), applies migrations, logs a
   warning if the database collation is not `Hebrew_100_CI_AS_SC`, and seeds the employees idempotently.

##### Seed data:
*  9 mock employees with **Hebrew names and addresses**, including:
    *  ישראל ישראלי (the specification's "Israel Israeli") with 2 phone numbers;
    *  a second ישראל ישראלי at a different address (a distinct employee under the natural key);
    *  an office line shared by two employees.
*  The C# seed data and the SQL seed script are kept identical (enforced by a unit test).

--------------------------------------------------------------------------------

#### 4. API Specification & Endpoints
All endpoints are documented with the .NET 10 native OpenAPI document (`/openapi/v1.json`), rendered by
**Swagger UI** (`/swagger`) and **Scalar** (`/scalar`). Errors use **RFC 7807 problem details**
(`application/problem+json`). JSON emits Hebrew unescaped and enums as names.

##### 1. POST /messages
*   **Description:** Validates input, creates a Message record (Pending), selects the provider via
    `SmsProviderFactory` (`Mock` when `provider` is omitted), dispatches the message, updates the status
    (Sent/Failed) and returns the stored message.
*   **Request Body:**
```json
{
  "to": "0501234567",
  "body": "Hello, your schedule has been updated.",
  "provider": "Mock"
}
```
(`provider` is optional: `Mock` or `Secondary`.)
*   **Response (201 Created):** the message, with a `Location` header for `GET /messages/{id}`:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "to": "0501234567",
  "employeeNames": ["ישראל ישראלי"],
  "body": "Hello, your schedule has been updated.",
  "status": "Sent",
  "providerName": "Mock",
  "errorMessage": null,
  "createdAt": "2026-11-08T19:00:00Z",
  "sentAt": "2026-11-08T19:00:01Z"
}
```
*   A provider failure is **not** an HTTP error: the response is still 201, with `status: "Failed"` and an
    `errorMessage`.
*   `employeeNames`: full names of the employees who own the recipient number, matched when the message is read
    (empty for an unknown number, several names for a shared number).
*   **Response (400 Bad Request):** validation errors as RFC 7807 problem details, keyed by field (`to`, `body`,
    `provider`). An unknown provider name is a validation error.

##### 2. GET /messages/{id}
*   **Description:** Retrieves the status and details of a single message.
*   **Response (200 OK):** Message object.
*   **Response (404 Not Found):** Problem details, if the message does not exist.

##### 3. GET /messages
*   **Description:** Retrieves the list of messages, optionally filtered.
*   **Query Parameters:** `?status=Sent`. Allowed values are `Pending`, `Sent` and `Failed` (case-insensitive), or
    empty for all. Any other value, including numbers, returns 400.
*   **Response (200 OK):** Array of Message objects ordered by CreatedAt descending.

##### Other endpoints:
*   `GET /health`: health check, including the database.

--------------------------------------------------------------------------------

#### 5. UI / Single Page Dashboard (API Web Host)
The API serves `wwwroot/index.html` directly at the root URL `/`, through `UseDefaultFiles()` and
`UseStaticFiles()`. There is deliberately no fallback route, so unknown API paths still return 404. The dashboard
is built with Tailwind CSS from a CDN, pinned with Subresource Integrity.

##### Features:
*   **Form:**
    *   Phone number input with live checks of allowed characters and length.
    *   Message text with a live countdown below it, in the format `142 / 160 characters remaining`. The limit is
        a fixed 160 characters, enforced by the text box; there is no SMS segment or encoding calculation.
    *   Provider dropdown (Mock Provider, Secondary Provider).
    *   Client-side validation mirroring the server rules, and a submit button with a loading spinner.
*   **Filter bar:** All, Sent, Failed, Pending.
*   **Table:** Message ID, Recipient, Employee, Body, Provider, status badge (green Sent, red Failed, yellow
    Pending, with the error under Failed), Timestamp.
*   **Auto-refresh toggle,** on by default: reloads every 5 seconds, and faster while a send is in flight. When it is
    off, the table does not reload by itself.
*   Starts in dark mode, with a light/dark toggle.
*   Hebrew text is displayed right-to-left (`dir="auto"`).
*   API data is rendered as text only, never as HTML.
*   Responsive: on narrow screens only the table scrolls horizontally.

--------------------------------------------------------------------------------

#### 6. Automated Testing Requirements
Unit tests with xUnit, FluentAssertions and NSubstitute:
*   Phone number validation (valid vs invalid formats, normalization to E.164).
*   Message body length validation.
*   `SmsProviderFactory` resolution by key (case-insensitive, unknown and duplicate names).
*   `MessageService` send flow (Pending then Sent/Failed, default provider, provider exceptions, cancellation).
*   Both providers' HTTP contracts, including against the simulated provider over real HTTP.

Integration tests for the `MessagesController` flow with `Microsoft.AspNetCore.Mvc.Testing` and
`WebApplicationFactory` (SQLite in memory, stubbed provider network).

Persistence tests: the EF model against the specified schema, migrations in sync with the model, seeding, and the
seed script matching the C# seed data. Optional SQL Server tests (run when `SMS_SQLSERVER_TEST_CONNECTION` is set)
verify the Hebrew collation, Hebrew round-trips and the real migration path.

--------------------------------------------------------------------------------

#### 7. Local Execution
No user authentication and no deployment are required. The system runs locally with a single command:
`.\run` (`run.cmd` in the repository root; works in PowerShell and cmd). It uses the globally installed .NET 10
SDK, or downloads a private copy into `.dotnet\` on first run, and then executes
`dotnet run --project src/SmsManagement.Api`. With the .NET 10 SDK installed globally, that command is equivalent.

Prerequisites: Windows 10/11, SQL Server LocalDB (`(localdb)\MSSQLLocalDB`), internet access on the first run,
free ports 5080 and 5000.

##### Components started by the command (one process):
*   **webapi:** ASP.NET Core 10 Web API and dashboard (`http://localhost:5080`).
*   **SQL Server LocalDB:** database created automatically, with migrations and seed data applied on API startup.
*   **mock-sms-provider:** simulated external provider API, embedded in the API process as a hosted service on its
    own port (`http://localhost:5000`). Providers call it over HTTP. Enabled only in the Development configuration,
    never in production or in the tests. It adds a 1.5-second delay per send in Development, so `Pending` is
    visible on the dashboard. It also answers `GET /api/sms/{id}` with a message's status; the API does not call
    this endpoint.

##### Simulated provider test numbers:
| Recipient | Mock provider | Secondary provider |
|-----------|---------------|--------------------|
| ends in `0000` | Failed: HTTP 503, simulated carrier outage | Failed: HTTP 400, invalid number (code 21211) |
| ends in `9999` | Sent | Failed: status `failed`, unreachable handset (code 30003) |
| anything else | Sent | Sent |

--------------------------------------------------------------------------------

#### 8. Non-functional Requirements
*   **Privacy:** employee names and other identity data are never written to logs. Only phone numbers and match
    counts are logged.
*   **Quality gates:** the build treats warnings, including NuGet vulnerability warnings, as errors. Package
    versions are managed centrally in `Directory.Packages.props`.
*   **Dependencies:**
    *   `System.Security.Cryptography.Pkcs` (pulled in by SqlClient) is pinned to 10.0.14, fixing CVE-2026-84008 and
        CVE-2026-70162.
    *   FluentAssertions stays on 7.x, the last Apache-2.0 release; 8.x requires a commercial license.
*   **Configuration:** no real secrets in committed files. Development uses Windows authentication for LocalDB and
    an obviously fake token for the simulated provider. Options are validated at startup.

--------------------------------------------------------------------------------

#### 9. Decisions Added During Development
Each item was decided after the initial specification. "Changed" marks where it changed the initial version.

| # | Decision | Reason |
|---|----------|--------|
| 1 | **Build our own simulated SMS provider.** | The assignment says a mock provider is supplied, but no endpoint was provided. Its contract is defined in sections 2 and 7. |
| 2 | **`provider` is optional, defaulting to `Mock`** (changed: the initial version made it required). | The assignment specifies `POST /messages` with `to` and `body` only. The optional field keeps that contract working while still letting the caller choose between the two providers. This is a deliberate deviation from the assignment. |
| 3 | **Messages have no foreign key to Employees.** Employee names are resolved by phone number when messages are read (`employeeNames`). | A shared number would make a single link ambiguous. Message history must survive employee changes. It also respects the security restriction on employee identity, and matches the initial schema. |
| 4 | **Recipients that match no employee are sent, with a warning.** | The assignment does not restrict recipients to employees, and arbitrary numbers can be tested. |
| 5 | **Hebrew support:** collation `Hebrew_100_CI_AS_SC`, Hebrew seed data, unescaped Hebrew in JSON, right-to-left text in the dashboard. | The employees' names and addresses are Hebrew. |
| 6 | **The collation is set at database level only;** text columns have no column-level collation. | The database is the single place that defines the collation. |
| 7 | **A database creation script** (`000_create_database.sql`) that fails on a wrong collation. | Tables inherit the database collation, so the database must be created with it, and a wrong collation must be caught before any table is created. |
| 8 | **Body limit is a fixed 160 characters,** with a countdown in the format `142 / 160 characters remaining` and no segment or encoding logic. | Keeps strictly to the specification. Known limitation: a Hebrew SMS segment holds 70 characters, so 160 Hebrew characters are sent as 3 segments. |
| 9 | **Dashboard served at `/`** with `UseDefaultFiles()` + `UseStaticFiles()`, and no fallback route. | Opening the API URL shows the dashboard directly. Without a fallback, unknown API paths still return a proper 404. |
| 10 | **A provider failure returns 201 with status `Failed`,** not an HTTP error. | The message was created and its outcome recorded; the request itself succeeded. |
| 11 | **Store `Pending` before calling the provider;** store the outcome even if the client disconnects. | Every dispatch attempt is on record, and a message is not left `Pending` because of a dropped connection. |
| 12 | **No automatic retries and no automatic failover** between providers. | Retrying or failing over without an idempotency key can send the same SMS twice. |
| 13 | **Strict status filter:** only `Pending`, `Sent` or `Failed` are accepted. | Default enum binding would also accept numbers and undefined values. |
| 14 | **The simulated provider is embedded in the API process,** on its own port, in Development only. | One process and one command start the whole system. Providers still call it over real HTTP, as they would a third party. |
| 15 | **`run.cmd` bootstrap,** which downloads a private .NET 10 SDK if none is installed. | Lets the reviewer run the system without installing .NET system-wide. |
| 16 | **Swagger UI and Scalar** render the same native OpenAPI document. | Both are common; Scalar's client examples are limited to C#, JavaScript fetch and curl. |
| 17 | **Dashboard refinements:** an Employee column; live phone input checks; auto-refresh on by default; a 1.5-second simulated delay so `Pending` is visible; starts in dark mode. | Makes the send → `Pending` → `Sent`/`Failed` lifecycle visible. |
| 18 | **Never log employee names.** | Employee identity is security-restricted. |
| 19 | **FluentAssertions 7.x and a pinned `System.Security.Cryptography.Pkcs`.** | Licensing (8.x is commercial) and known CVEs in a transitive dependency. |
