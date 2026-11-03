[1mdiff --git a/src/Services/TradeService/Api/TradeEndpoints.cs b/src/Services/TradeService/Api/TradeEndpoints.cs[m
[1mindex e4c4719..7c1725a 100644[m
[1m--- a/src/Services/TradeService/Api/TradeEndpoints.cs[m
[1m+++ b/src/Services/TradeService/Api/TradeEndpoints.cs[m
[36m@@ -188,7 +188,7 @@[m [mpublic static class TradeEndpoints[m
         var row = await ([m
                 from t in db.Trades.AsNoTracking()[m
                 join i in db.Instruments on t.InstrumentId equals i.Id[m
[31m-                where t.Id == tradeId && t.UserId == userId[m
[32m+[m[32m                where t.Id == tradeId[m
                 select new TradeRow(t.Id, i.Symbol, t.Side, t.Quantity, t.Price, t.ExecutedAt, t.CostBasis))[m
             .FirstOrDefaultAsync(ct);[m
 [m
