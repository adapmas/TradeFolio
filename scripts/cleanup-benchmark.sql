BEGIN;
DELETE FROM trade.trades WHERE idempotency_key LIKE 'bench-%';
DELETE FROM trade.holdings WHERE user_id::text LIKE '00000000-0000-4000-8000-%';
COMMIT;

ANALYZE trade.trades;
ANALYZE trade.holdings;
ANALYZE trade.holding_lots;