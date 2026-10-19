# TradeFolio

Full-stack microservices portfolio analytics platform (.NET, PostgreSQL, RabbitMQ, Redis, React, Docker).
The spec is the source of truth: docs/TradeFolio-spec.md. Read the relevant section before starting a task.

## Repo layout
- src/Gateway: API gateway (JWT, routing, rate limiting, /api/v1 versioning)
- src/Services/{TradeService,PortfolioService,AnalyticsService,MarketDataService,NotificationService}
- src/BuildingBlocks/{Contracts,Common,Observability}: shared code
- src/Frontend/tradefolio-web: React + TypeScript
- tests/: unit, integration (Testcontainers), end-to-end
- infrastructure/: docker, monitoring, terraform
- docs/: architecture, api, database, adr

## Commands
- Build: `dotnet build`
- Unit tests: `dotnet test --filter "FullyQualifiedName~UnitTests"`
- Integration tests (needs Docker running): `dotnet test --filter "FullyQualifiedName~IntegrationTests"`
- Local stack: `docker compose up --build`
- Frontend: `cd src/Frontend/tradefolio-web && npm run dev`

## Non-negotiable rules
- Correctness before features. Follow the sell algorithm in spec section 7 exactly.
- Money, price and quantity use `decimal` in C# and `numeric` in PostgreSQL. Never float or double.
- FIFO and analytics logic is pure domain code: no database, HTTP or Redis calls inside it.
- Integration tests use a real PostgreSQL via Testcontainers. Never mock the database.
- Enforce invariants in the database as well as in code (CHECK constraints, unique constraints).
- Trade, holding, lot and outbox changes happen in ONE database transaction.
- Events are at-least-once. Consumers must be idempotent, enforced by a unique constraint on event_id.
- The user ID comes from the JWT claim, never from the request body. Every user-scoped query checks ownership.
- Return ProblemDetails-style errors. Never leak stack traces or secrets in responses.
- Never commit secrets and never log passwords, JWTs or API keys. Config comes from environment variables. Keep `.env.example` updated.
- Add or pin NuGet and npm packages only when needed, with exact versions.

## Workflow
- Work on one phase or task at a time. Do not build ahead.
- For domain logic, write the failing test first, then the implementation.
- Run the relevant tests before saying a task is done, and report the results honestly.
- When making an architecture decision, add a short ADR in docs/adr (numbered, e.g. 0001-service-data-ownership.md).
- Explain locking, transaction and idempotency code in comments or the summary so the developer can defend it in an interview.
- Update docs/progress.md at the end of each task (what is done, what is next).

## Current status
See docs/progress.md.