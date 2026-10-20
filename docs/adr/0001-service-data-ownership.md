# ADR 0001: Service data ownership and the trade transaction

- **Status:** Accepted
- **Date:** 20 October 2026

## Context

The specification gives each service a responsibility:

- Trade Service: trade execution, trade records, transaction boundaries, outbox events.
- Portfolio Service: holdings, FIFO lots, portfolio state.

But the sell algorithm (spec section 7) must, in **one database transaction**: lock the holding, check the quantity, consume FIFO lots, update the holding, insert the trade, and insert the outbox event. Overselling is prevented by the row lock on the holding inside that transaction.

A single transaction cannot span two separate databases without distributed transactions or compensating workflows. So the question is: who owns the holdings and lots tables, and where do they live?

## Options

### Option 1: One PostgreSQL database, Trade Service is the only writer (recommended)

- One database, one schema per service area (for example `trade` and `notification`).
- Trade Service is the **only writer** of trades, holdings, lots and outbox rows, so one local transaction covers the whole sell.
- Portfolio Service and Analytics Service **read** holdings and lots through read-only database access (a read-only role, or views that act as the contract).
- Notification Service has its own tables and never reads trade tables.

Good: simple, strong consistency, a real row lock, easy to test with one Testcontainers PostgreSQL.
Bad: services share a database, so a schema change in the trade tables can affect readers.

### Option 2: Separate database per service, events between them

- Trade Service owns trades; Portfolio Service owns holdings and lots in its own database.
- Portfolio is updated through events or a saga.

Good: textbook microservice data ownership.
Bad: the sell can no longer be atomic. Two simultaneous sells could both pass the check before the holding is updated, which breaks FR-06 and FR-07. Fixing this needs sagas, reservations or distributed locking, which is far too much for six weeks and goes against "avoid microservice theater".

### Option 3: Merge Portfolio into Trade Service

- One service owns trades, holdings and lots.

Good: simplest and fully consistent.
Bad: removes the Portfolio boundary the specification asks for, and weakens the microservices story.

## Decision

**Option 1.** Trade Service is the single writer for trades, holdings, lots and the outbox, inside one local PostgreSQL transaction. Portfolio Service and Analytics Service are read-only consumers of that data.



## Consequences

- The concurrency guarantee (spec section 7) is achievable with Read Committed plus a row lock.
- Portfolio and Analytics must never write to trade, holding or lot tables. Enforce this with a read-only database role.
- Readers depend on the table structure. Keep the schema stable, and prefer views for anything Analytics and BI read, so tables can change without breaking readers.
- Notification Service stays independent: it only uses events and its own data.
- If a service ever needs to scale or move to its own database, the reads can be switched from direct database access to an API or an event-fed read model.

## Short answer for interviews

"The sell has to be atomic, so one service owns the write path and one PostgreSQL transaction covers the trade, the holding, the FIFO lots and the outbox row. Other services only read. I chose this over separate databases because splitting the write across two databases would make overselling prevention depend on distributed coordination, which adds complexity without adding value here. I would revisit it if a service needed to scale or deploy completely independently."