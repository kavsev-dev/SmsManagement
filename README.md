# SMS Management System

A service for sending SMS messages to employees through a simulated SMS provider. It has a REST API, SQL Server
storage and a small dashboard. This is Part A of the Full Stack assignment; Part B is submitted as a separate
document.

**Stack:** .NET 10 / ASP.NET Core 10, EF Core 10, SQL Server (LocalDB), xUnit. The dashboard is plain HTML and
JavaScript with Tailwind CSS.

The specification is in [SPEC.md](SPEC.md). The system runs locally; there is no user authentication and no
deployment.

## How to run (one command)

**Prerequisites**

| Requirement | Details |
|-------------|---------|
| Windows 10 or 11 | SQL Server LocalDB and `run.cmd` are Windows-only. |
| SQL Server LocalDB | Instance `(localdb)\MSSQLLocalDB`. Installed with Visual Studio, or with SQL Server Express (LocalDB feature). Check with `sqllocaldb info`. |
| Internet access on the first run | To restore NuGet packages and, if needed, download the .NET 10 SDK (about 300 MB). |
| Free ports 5080 and 5000 | 5080: API and dashboard. 5000: the simulated SMS provider. |


Visual Studio is not required. If you use it, note that .NET 10 is officially supported in Visual Studio 2026; an
older Visual Studio may fail to build the solution, so run from the command line as shown below.

### Quick start

Run all commands from the repository root.

#### With the .NET 10 SDK installed

```bash
dotnet run --project src/SmsManagement.Api
```

#### Without the .NET 10 SDK

Use `run.cmd`. In PowerShell type `.\run`; in cmd, plain `run` also works:

```powershell
.\run
```

On the first run, `run.cmd` downloads a private .NET 10 SDK (about 300 MB) into `.dotnet\` inside the repository,
using Microsoft's official `dotnet-install.ps1`. This needs no admin rights and leaves the machine's existing .NET
versions alone; later runs reuse it. If a global .NET 10 SDK is installed, `run.cmd` simply uses it.
`.\run` was tested on a second Windows machine.

Then open **http://localhost:5080**. Press Ctrl+C to stop.

**What starts** (one process):

| URL | What |
|-----|------|
| http://localhost:5080 | Dashboard |
| http://localhost:5080/swagger and /scalar | Interactive API documentation |
| http://localhost:5080/health | Health check, including the database |
| http://localhost:5000 | Simulated SMS provider (inside the same process) |

On startup the API creates the `SmsManagement` database on LocalDB, with the Hebrew collation, applies the
migrations and seeds 9 employees with Hebrew names. Ready-made requests are in
`src/SmsManagement.Api/SmsManagement.Api.http`.

## What I built

| Requirement | Implementation |
|-------------|----------------|
| 1. `POST /messages` with `to` and `body`, returns a message ID | Returns **201** with the stored message, including its `id`, and a `Location` header. |
| 2. `GET /messages/:id` returns the status | **200** with the message, or **404** with problem details. |
| 3. `GET /messages` with a status filter | `?status=Pending\|Sent\|Failed`, or empty for all; newest first. |
| 4. Input validation (phone number, message length) | `PhoneNumberValidator`, `MessageBodyValidator`, `SendMessageRequestValidator` in `SmsManagement.Core`. Errors are RFC 7807 problem details per field. |
| 5. Messages stored in a database | SQL Server with EF Core: `Employees`, `EmployeePhones` and `Messages` tables. Scripts and migrations are in `/db`. |
| 6. Automated tests for the critical logic | 198 tests: unit, persistence, API integration, and optional SQL Server tests. See [Tests](#tests). |
| 7. Simple UI (optional) | Dashboard at `/`: send form with a 160-character countdown, status filter, and a message table with auto-refresh. |

**Beyond the requirements:**
- Two SMS providers behind a Strategy + Factory design: `Mock`, and `Secondary`, which simulates a Twilio-style
  API with a different request format and authentication.
- The simulated provider runs inside the API process but is called over real HTTP, so one command starts
  everything.
- Interactive API documentation, generated from the native .NET 10 OpenAPI document: Scalar at `/scalar` (linked
  from the dashboard header) and Swagger UI at `/swagger`.

**Structure:** `SmsManagement.Core` holds the domain, validation and the send use case (`MessageService`). It has
no infrastructure dependencies. `SmsManagement.Infrastructure` holds EF Core and the providers.
`SmsManagement.Api` holds the controllers, OpenAPI, the dashboard and the simulated provider.
`SmsManagement.Tests` holds the tests.

## Assumptions

- **No mock SMS provider was supplied,** although the assignment mentions one, so I built my own. Its API is
  documented in SPEC.md, sections 2 and 7.
- **Employees** are identified by first name + last name + address, enforced by a unique index. An employee can
  have several phone numbers, and a number can belong to several employees (e.g. a shared office line).
- **Employees are reference data,** seeded on startup. Managing employees through the API is out of scope.
- **Phone number formats:** E.164 (`+972501234567`) or Israeli national format (`0501234567`); spaces and hyphens
  are ignored.
- **Message length:** at most 160 characters, counted as `string.Length`, for Hebrew and English alike.
- **`Sent` means the provider accepted the message,** not that it was delivered to the phone. There are no
  delivery receipts.
- **Numbers that match no employee are still sent,** with a warning in the log. The assignment doesn't restrict
  recipients to employees.
- **Hebrew text must be supported:** employee names, addresses and message text.
- **A single local instance,** with no authentication and no deployment, as the assignment states.

## Key decisions

- **Save first, then send.** `MessageService` stores the message as `Pending` before calling the provider, then
  stores `Sent` or `Failed`, even if the client disconnects. Every attempt is on record.
- **A provider failure is not an HTTP error.** The API returns **201** with `status: "Failed"` and an
  `errorMessage`: the message was created and its outcome recorded. Providers return failures instead of throwing.
- **No automatic retries and no automatic failover.** Without an idempotency key, either one can send the same SMS
  twice.
- **Providers are strategies.** `ISmsProvider` is implemented by `MockSmsProvider` and `SecondarySmsProvider`, and
  `SmsProviderFactory.GetProvider(name)` picks the one named in the request (`Mock` when none is named). Adding a
  provider means adding a class and registering it.
- **Messages store only the recipient number;** there is no foreign key to employees:
  - a shared number would make a single link ambiguous;
  - message history must survive employee changes;
  - it respects the security restriction on employee identity.

  Employee names are resolved by phone number when messages are read (`employeeNames` in responses).
- **Hebrew:** the database collation `Hebrew_100_CI_AS_SC` is set once, at database level. All text columns are
  `nvarchar`, the API returns Hebrew unescaped, and the dashboard shows it right-to-left.
- **The dashboard is served by the API at `/`.** There is no separate frontend, and no fallback route, so unknown
  API paths still return 404.
- **Security and privacy:**
  - employee names are never written to logs;
  - message text is displayed as text, never as HTML;
  - a transitive package with known CVEs is pinned to a patched version;
  - FluentAssertions stays on 7.x, because 8.x requires a commercial license.

The complete list, with reasons, is in SPEC.md, section 9.

## Deliberate deviation from the assignment

The assignment specifies `POST /messages` with `to` and `body`. I added an **optional** `provider` field (`Mock` or
`Secondary`), so the caller can choose between the two providers. When it is omitted, `Mock` is used, so a request
with only `to` and `body` works exactly as the assignment describes. The response also returns the whole stored
message, not only its ID.

## What I chose not to do, and why

- **Authentication and deployment:** out of scope, as the assignment states.
- **Background sending (queue + worker):** sending inside the request is simpler and enough for individual
  messages. Load is discussed in Part B.
- **Automatic retries and failover between providers:** both need an idempotency key first; otherwise they risk
  duplicate SMS.
- **Delivery receipts:** the simulated provider has a status endpoint, but `Sent` is recorded when the provider
  accepts the message.
- **SMS segment counting for Hebrew (70 characters per segment):** the limit is a fixed 160 characters
  (`string.Length`) for every language, as the specification defines it. A 160-character Hebrew text is therefore
  accepted, although carriers send it as 3 segments, which affects cost.
- **Messages longer than one SMS (several SMS per text):** the assignment asks to validate the message length, so a
  message is one SMS of at most 160 characters; longer texts are rejected with 400.
- **Bulk send:** `POST /messages` sends one message to one recipient, as the assignment specifies. Sending a
  campaign to many recipients is discussed in Part B.
- **Paging on `GET /messages` and an API for managing employees:** not required at this size.

## What I would add with more time

In order:
1. An idempotency key on `POST /messages`, so a double click or a client retry doesn't send twice.
2. Sending through an outbox and a background worker, so messages left `Pending` after a crash are picked up
   again, and campaigns don't block requests.
3. Delivery status tracking, by polling the provider's status endpoint or receiving webhooks.
4. Provider routing by cost, limits and health, with a circuit breaker and safe failover.
5. Paging and date filters on `GET /messages`.
6. Metrics and alerts: failure rate per provider, latency, and the age of the oldest `Pending` message.
7. SMS segment counting for Hebrew (70 characters per segment), shown in the dashboard countdown, and its effect on
   cost.
8. Messages longer than one SMS, sent as several concatenated SMS, with the limit based on the number of segments.

## Tests

```bash
dotnet test
```

Without a global .NET 10 SDK, run `.\run` once first; it downloads the SDK into `.dotnet\`. Then run the tests
with that SDK:

```powershell
.\.dotnet\dotnet test
```

198 tests (194 run by default; 4 SQL Server tests are optional). They cover:
- phone and body validation;
- provider selection by the factory;
- the send flow in `MessageService`: `Pending` then `Sent`/`Failed`, the default provider, provider exceptions and
  cancellation;
- both providers' HTTP contracts, including calls to the simulated provider over real HTTP;
- the EF model and seeding;
- the full API flow through `WebApplicationFactory`.

The default run needs no database server; it uses SQLite in memory.

The optional SQL Server tests verify the Hebrew collation, Hebrew round-trips and the real migration path:

```bash
SMS_SQLSERVER_TEST_CONNECTION="Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True" dotnet test
```

The system was also checked end to end:
- a full run against LocalDB;
- scripted browser checks of the dashboard: the countdown, the 160 limit, a Hebrew send, filters, HTML in a
  message shown as text, a new message going from `Pending` to `Sent`, the Employee column, the live phone input
  checks and the auto-refresh toggle;
- running `.\run` from a fresh unpacked zip on a machine without the .NET 10 SDK.

### Test numbers for the simulated provider

| Recipient | Mock provider | Secondary provider |
|-----------|---------------|--------------------|
| ends in `0000` (e.g. `0500000000`) | `Failed`: HTTP 503, simulated outage | `Failed`: HTTP 400, invalid number |
| ends in `9999` (e.g. `0509999999`) | `Sent` | `Failed`: status `failed`, unreachable handset |
| anything else | `Sent` | `Sent` |

## Use of AI

- **Specification:** I wrote SPEC.md with NotebookLM, from the assignment requirements. This turned the
  requirements into an explicit, testable specification before any code was written.
- **Implementation:** the code, tests, database scripts and documentation were generated with Claude Code
  (Anthropic) from that specification, step by step, with review checkpoints. This let me cover the requirements,
  tests and verification within the time limit.
- **Verification:** AI ran the automated tests and the end-to-end and browser checks. I tested `.\run` on a second
  Windows machine.

As the assignment requires, the full specification, including every decision added during development, is in
[SPEC.md](SPEC.md).

## Time spent

About 4 hours in total, for Part A and Part B.
