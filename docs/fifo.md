# FIFO cost basis accounting

Code: `src/Services/TradeService/Domain/FifoCalculator.cs`
Tests: `tests/TradeService.UnitTests/FifoCalculatorTests.cs`

## The problem

When you sell shares, you need to know **what those shares originally cost**, so you can work out profit or loss. If you bought the same stock several times at different prices, "which shares did I just sell?" has more than one possible answer. FIFO (first in, first out) gives one clear rule.

## The rule

A sale consumes the **oldest open BUY lots first**. Each BUY creates one lot (quantity, price, time). A sale takes shares from the oldest lot, then the next oldest, and so on until the sale is covered.

- **Cost basis** = sum of (shares taken from each lot x that lot's purchase price)
- **Proceeds** = shares sold x sell price
- **Realized P&L** = proceeds - cost basis

## Worked example (from the specification)

```
BUY 10 @ 100
BUY  5 @ 120
SELL 12 @ 150
```

- Take 10 from lot 1: 10 x 100 = 1000. Still need 2.
- Take 2 from lot 2: 2 x 120 = 240. Done.
- Cost basis = 1000 + 240 = **1240**
- Proceeds = 12 x 150 = **1800**
- Realized P&L = **560**
- Remaining: **3 shares @ 120** (lot 2, partly used)

## A sale spanning three lots

Lots: 4 @ 10, 6 @ 20, 10 @ 30. Sell 12 @ 40.

- Lot 1 fully used: 4 x 10 = 40
- Lot 2 fully used: 6 x 20 = 120
- Lot 3 partly used (2 of 10): 2 x 30 = 60
- Cost basis 220, proceeds 480, P&L **260**, remaining **8 @ 30**

## The algorithm

1. Ignore lots with nothing left. Sort the rest by purchase time, then by id (so equal timestamps always give the same order).
2. Add up the shares available. If the sale is bigger, throw `InsufficientQuantityException`.
3. Walk through the sorted lots. From each, take the smaller of "what the lot has" and "what is still needed". Add `taken x purchase price` to the cost basis and record the consumption.
4. A lot with shares left goes into `RemainingLots` with the reduced quantity. Lots never reached are copied over unchanged.
5. Compute proceeds and P&L.

## Design decisions

- **Pure function.** No database, no clock, no HTTP. The same input always gives the same output, which makes it easy to test and safe to reuse in analytics.
- **Immutable lots.** The function never changes the lots you pass in. It returns new values (`lot with { RemainingQuantity = ... }`).
- **`decimal` everywhere.** Floats cannot represent values like 0.1 exactly, so money and quantity never use them.
- **Deterministic tie-break.** Equal purchase times are ordered by lot id.
- **Input checks.** Quantity and price must be positive (`ArgumentOutOfRangeException`). A sale larger than the holding throws `InsufficientQuantityException`.
- **`Consumptions` output.** It says exactly which lots were used and by how much, so the database layer (Phase 3) can update or delete the right rows.

## Complexity

Let **n** be the number of open lots.

- Filtering lots: O(n)
- Sorting: O(n log n)
- Summing available shares: O(n)
- Walking the lots: O(n)
- **Total time: O(n log n). Extra space: O(n)** for the sorted copy and the result lists.

In practice n is small (a user rarely has hundreds of open lots per instrument).

Possible improvement: if the database already returns lots in order, the sort can be skipped (the index on `HoldingLots(holding_id, purchased_at)` exists for this). The walk can also stop as soon as the sale is covered. Then the work is proportional to **k**, the number of lots actually consumed, instead of n. In Phase 3 the SELL path will read lots in index order and only touch the consumed ones.

## Why FIFO

- It creates a real algorithmic problem (partial lots, ordering, edge cases) on top of the database work.
- It is a widely used, easy-to-explain cost basis method, and it gives one deterministic answer.
- Other methods exist (LIFO, average cost, choosing specific lots). They give different realized P&L for the same trades. FIFO was chosen for simplicity and predictability. Rules about which method is allowed differ between countries, and TradeFolio does not give tax or financial advice.

## Edge cases covered by tests

- The specification example (P&L 560, 3 left @ 120)
- Selling exactly one lot
- Selling everything (no lots left)
- Selling more than owned (throws)
- Equal purchase times (ordered by id)
- Using only part of one lot
- Lots passed in the wrong order (the function sorts them itself)
- Lots with nothing remaining (skipped)
- Fractional quantities and prices (exact)
- Zero or negative quantity or price (rejected)
- A sale spanning three lots

## In my own words

*(Write this part yourself after reading the code once more. Aim for 4 to 6 sentences, then try saying it out loud without looking.)*

- What FIFO is and what problem it solves:
- How my code works, step by step:
- Why the function is pure and the lots are immutable:
- Its time complexity and why:
- One thing that surprised me or went wrong while building it: