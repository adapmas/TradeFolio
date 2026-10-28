
BEGIN;

CREATE TEMP TABLE bench_users AS
SELECT n, ('00000000-0000-4000-8000-' || lpad(n::text, 12, '0'))::uuid AS user_id
FROM generate_series(1, 50) AS n;

CREATE TEMP TABLE bench_instruments AS
SELECT id AS instrument_id, row_number() OVER (ORDER BY symbol) AS n
FROM trade.instruments;

-- 100,000 trades; user 1 is a heavy trader (about 40% of trades)
INSERT INTO trade.trades
  (id, user_id, instrument_id, side, quantity, price, executed_at, idempotency_key, cost_basis, request_hash)
SELECT
  gen_random_uuid(),
  u.user_id,
  i.instrument_id,
  CASE WHEN s.g % 3 = 0 THEN 'Sell' ELSE 'Buy' END,
  round((random() * 99 + 1)::numeric, 4),
  round((random() * 400 + 10)::numeric, 2),
  now() - (random() * interval '730 days'),
  'bench-' || s.g,
  CASE WHEN s.g % 3 = 0 THEN round((random() * 5000)::numeric, 2) END,
  'bench'
FROM (
  SELECT g,
         CASE WHEN g % 5 < 2 THEN 1 ELSE 1 + (g % 50) END AS un,
         1 + (g % 10) AS inn
  FROM generate_series(1, 100000) AS g
) s
JOIN bench_users u ON u.n = s.un
JOIN bench_instruments i ON i.n = s.inn;

-- one holding per benchmark user per instrument (500 holdings)
INSERT INTO trade.holdings (id, user_id, instrument_id, quantity, updated_at)
SELECT gen_random_uuid(), u.user_id, i.instrument_id, 0, now()
FROM bench_users u CROSS JOIN bench_instruments i;

-- lots: 5,000 for user 1's first-instrument holding, 200 for every other holding
INSERT INTO trade.holding_lots
  (id, holding_id, original_quantity, remaining_quantity, purchase_price, purchased_at)
SELECT
  gen_random_uuid(),
  x.holding_id,
  x.orig,
  round(x.orig * x.frac, 4),
  x.price,
  x.purchased_at
FROM (
  SELECT
    h.id AS holding_id,
    round((random() * 99 + 1)::numeric, 4) AS orig,
    (CASE WHEN random() < 0.6 THEN 0 ELSE random() END)::numeric AS frac,
    round((random() * 400 + 10)::numeric, 2) AS price,
    now() - (random() * interval '730 days') AS purchased_at
  FROM trade.holdings h
  JOIN bench_users u ON u.user_id = h.user_id
  JOIN bench_instruments i ON i.instrument_id = h.instrument_id
  CROSS JOIN LATERAL generate_series(
    1, CASE WHEN u.n = 1 AND i.n = 1 THEN 5000 ELSE 200 END
  ) AS k
) x;

-- keep holding quantities consistent with their lots
UPDATE trade.holdings h
SET quantity = COALESCE(
  (SELECT sum(l.remaining_quantity) FROM trade.holding_lots l WHERE l.holding_id = h.id), 0)
WHERE h.user_id::text LIKE '00000000-0000-4000-8000-%';

COMMIT;

ANALYZE trade.trades;
ANALYZE trade.holdings;
ANALYZE trade.holding_lots;
