

Progress · MD
TradeFolio Progress
Last updated: 20 October 2026

Right now
Working on: Task 1.5 (docs and tracking files). Next up: Phase 2, Task 2.1 (FIFO domain types).

Phase 1: Foundation
 1.1 Solution skeleton (global.json, solution, shared build settings, three projects)
 1.2 Docker Compose with PostgreSQL, Redis, RabbitMQ
 1.3 TradeService runnable with Swagger, /health and environment config
 1.4 Shared error and pagination types, with unit tests
 1.5 Docs and tracking files (this file, requirements checklist, ADR 0001)
Phases 2 to 12
Not started. See the build plan for the full task list.

Decisions so far
.NET SDK 10.0.402 pinned in global.json; solution file is TradeFolio.slnx.
Central package management: package versions live in Directory.Packages.props, never in individual project files.
Warnings are treated as errors.
Money, price and quantity use decimal in C# and numeric in PostgreSQL.
Paging corrects bad input instead of rejecting it: page below 1 becomes 1, missing or invalid size becomes 20, size above 100 becomes 100.
Configuration comes from environment variables and a local .env file (never committed).
Service data ownership: see docs/adr/0001-service-data-ownership.md.
Lessons and gotchas
An empty (0-byte) global.json, Directory.Build.props or Directory.Packages.props crashes every dotnet command. Check file sizes first when you see "Root element is missing" or "no JSON tokens".
NU1008 means a project file contains a package Version. With central package management, remove it from the .csproj and put it in Directory.Packages.props.
Keep only one solution file (.sln or .slnx) in the repo root.
dotnet new ... --force regenerates template files, including any edits you made to them.
Commands I use often
Build: dotnet build
Tests: dotnet test
Run TradeService: dotnet run --project src/Services/TradeService (listens on http://localhost:5158)
Start infrastructure: docker compose up -d (Docker Desktop must be running)
Check infrastructure: docker compose ps

