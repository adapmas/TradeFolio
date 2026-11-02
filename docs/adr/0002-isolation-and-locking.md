\# ADR 0002: Isolation level and locking for trade execution



\- Status: Accepted

\- Date: 2026-11-02



\## Context



A SELL must never spend shares the user does not own, even when two requests for the

same holding arrive at the same moment (FR-06, spec section 7). The naive flow is

"read the quantity, check it, then write the new quantity". Under concurrency, two

requests can both read 10, both pass the check for 7, and both sell:



&#x20;   A reads 10 -> ok      B reads 10 -> ok

&#x20;   A writes 3            B writes 3        (14 shares sold from 10)



The same holding is also changed by BUYs, by the FIFO lots that belong to it, and by

retries that reuse an Idempotency-Key. A BUY for a brand new holding has its own race:

two first BUYs can both find "no holding yet".



\## Options considered



1\. \*\*Read Committed plus an explicit row lock\*\* (`SELECT ... FOR UPDATE` on the holding).

2\. \*\*Serializable isolation\*\* with a retry loop on serialization failures.

3\. \*\*Repeatable Read\*\* with a retry loop. In PostgreSQL a concurrent update of a row this

&#x20;  transaction already read fails with a serialization error, so it also needs retries.

4\. \*\*Optimistic concurrency\*\* (a row version column) with retries.

5\. \*\*A single conditional UPDATE\*\* (`SET quantity = quantity - 7 WHERE quantity >= 7`).

&#x20;  This protects the holding row alone, but a SELL also has to read and update several lot

&#x20;  rows in FIFO order, so one statement is not enough.



\## Decision



Use PostgreSQL's default \*\*Read Committed\*\* isolation plus an \*\*explicit row lock on the

holding\*\* being changed.



\- A SELL starts a Read Committed transaction and locks the holding row and reads its

&#x20; quantity in one statement (`SELECT \* FROM trade.holdings WHERE user\_id = ... AND

&#x20; instrument\_id = ... FOR UPDATE`). The lock is taken before the quantity is used.

\- A competing request waits at that statement. When the first transaction commits,

&#x20; PostgreSQL returns the updated row to the waiting request, so it sees the new quantity.

\- Only after holding the lock does the request read the open lots, consume them in FIFO

&#x20; order, update the lots and the holding, and insert the trade, all in the same transaction.

\- A BUY creates or updates the holding with `INSERT ... ON CONFLICT (user\_id,

&#x20; instrument\_id) DO UPDATE`, which takes the same row lock and relies on the unique

&#x20; index to make the first-BUY race safe.

\- The unique index on `(user\_id, idempotency\_key)` makes duplicate keys safe. A duplicate

&#x20; that loses the race gets the first request's result instead of an error.



\## What this prevents



\- \*\*Overselling and negative holdings.\*\* The check and the write are serialised per holding.

\- \*\*Lost updates\*\* on the holding quantity and on lot remaining quantities. Only the

&#x20; transaction holding the holding lock touches that holding's lots.

\- \*\*Two holdings for one user and instrument\*\* (unique index plus `ON CONFLICT`).

\- \*\*Duplicate trades from retries\*\* (unique index, plus the replay logic).

\- \*\*Partial results.\*\* Trade, holding and lots commit together or not at all (FR-07).



\## Why not the alternatives



\- \*\*Serializable / Repeatable Read:\*\* correct, but the database aborts one of two

&#x20; competing transactions and the application must retry. For one hot row per user and

&#x20; instrument, queueing behind a lock is simpler and gives the second request the right

&#x20; answer directly, with no retry loop to write and test.

\- \*\*Optimistic concurrency:\*\* same retry cost, and it needs an extra column.

\- \*\*Conditional UPDATE alone:\*\* does not cover the FIFO lots.



\## What Read Committed still allows



Each statement sees data committed before it started, so a transaction can see different

data in two statements (non-repeatable reads, phantoms). That is acceptable here because

everything this transaction depends on is guarded by the holding lock, and the lots are

read after the lock is held. Any new code that changes holdings or lots must take the

same lock first.



\## Deadlocks and lock scope



\- The lock is on one row per (user, instrument). Trades for other users or other

&#x20; instruments never wait on each other.

\- A trade transaction locks only one holding, so two trade transactions cannot wait on

&#x20; each other in a cycle. If a future feature changes several holdings in one transaction,

&#x20; lock them in a fixed order (for example by id).

\- Locks are held for a short transaction (a few statements).



\## When Serializable would be better



\- When an invariant spans many rows and there is no single row to lock, for example a

&#x20; portfolio-wide limit across all of a user's holdings, or rules that depend on a count

&#x20; or sum of rows that other requests may insert.

\- When the database should catch mistakes (for example a forgotten lock) rather than

&#x20; relying on every code path doing the right thing.

\- When contention is low and retries are cheap.



Costs: serialization failures (SQLSTATE 40001) that must be retried, extra tracking

overhead, and false positives that abort transactions that were actually safe.



\## Consequences



\- Trades for the same holding run one after another; under heavy contention they queue.

\- Correctness depends on every code path taking the lock. The tests guard this.

\- Could add a `lock\_timeout` later so a stuck transaction fails fast.



\## Evidence



\- `ConcurrencyTests`: two simultaneous SELL 7 against a holding of 10, real PostgreSQL

&#x20; container, exactly one succeeds. Removing `FOR UPDATE` makes it fail (both sells succeed).

\- `RaceTests`: the same race repeated 50 times, BUY with SELL, two first BUYs, and the

&#x20; same Idempotency-Key for BUY and SELL.

