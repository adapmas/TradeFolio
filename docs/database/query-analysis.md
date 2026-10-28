\# Query analysis (EXPLAIN ANALYZE)



\## Setup

\- PostgreSQL 17.11 in Docker, run locally.

\- Synthetic data: 100,005 trades, 500 holdings, 104,800 lots (scripts/seed-benchmark.sql); `ANALYZE` run after seeding.

\- "Before" plans: SET enable\_indexscan/bitmapscan/indexonlyscan = off.



\## Query A: trade history by user

```sql

(paste the query)

```

\### With index

(paste plan)

\### Without index

(paste plan)

✍ Explain: which index was used, why the Incremental Sort appears, why only 21 rows were read.



\## Query B: FIFO lot lookup

```sql

(paste the query)

```

\### With index

(paste plan)

\### Without index

(paste plan)

✍ Explain: why a bitmap scan, why the Sort node, what the filter removed.



\## Summary

(table from above)



\## Index reasoning

✍ Why is the equality column (user\_id / holding\_id) first and the sort column second?



\## Possible improvements

(the two ideas above, one line each)



\## Caveats

(the three points above)

