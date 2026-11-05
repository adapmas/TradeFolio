\# Manual checks



Run these by hand against the running stack after the build is complete (and again before

the final audit, task 12.8). Tick each one when done. Automated tests cover the logic; these

prove the real, running application behaves the same way.



\## Setup



\- \[ ] Docker Desktop running; `docker compose up -d`; postgres healthy

\- \[ ] In this PowerShell window set `ConnectionStrings\_\_TradeDb` and `Jwt\_\_SigningKey`

\- \[ ] Start the service: `dotnet run --project src\\Services\\TradeService`

\- \[ ] Note the port, then in a second window: `$base = "http://localhost:XXXX"`

\- \[ ] `$cred = @{ email = "alice@example.com"; password = "Sup3rSecret99" } | ConvertTo-Json`



\## 5.1 Registration



\- \[ ] Register `$cred`: `Invoke-RestMethod -Method Post -Uri "$base/api/v1/auth/register" -ContentType "application/json" -Body $cred` returns 201 with id, email, createdAt, and no password or hash

\- \[ ] Register the same body again: 409

\- \[ ] Register `ALICE@example.com`: 409 (case-insensitive)

\- \[ ] Register `{"email":"nope","password":"short"}`: 400 with errors for email and password

\- \[ ] Database check: `docker compose exec postgres psql -U tradefolio -d tradefolio -c "SELECT email, left(password\_hash, 20) AS hash\_start, status FROM trade.users;"` shows a hash, not the password



\## 5.2 Login and JWT



\- \[ ] Login: `$login = Invoke-RestMethod -Method Post -Uri "$base/api/v1/auth/login" -ContentType "application/json" -Body $cred; $token = $login.accessToken; $login.expiresIn` gives 900

\- \[ ] `Invoke-RestMethod -Uri "$base/api/v1/users/me" -Headers @{ Authorization = "Bearer $token" }` returns the profile

\- \[ ] `/users/me` with no token: 401 (`try { Invoke-RestMethod -Uri "$base/api/v1/users/me" } catch { $\_.Exception.Response.StatusCode.value\_\_ }`)

\- \[ ] Tampered token: `$p = $token.Split('.'); $bad = ($p\[0], $p\[1], ("A" \* $p\[2].Length)) -join "."` then call `/users/me` with it: 401

\- \[ ] Wrong password and unknown email both return 401 from login

\- \[ ] Expiry (optional): set `$env:Jwt\_\_AccessTokenMinutes = "1"`, restart, log in, wait about 100 seconds, call `/users/me`: 401



\## Added by later tasks

## 5.3 Ownership and isolation

- [ ] Register and log in as Alice and Bob; build `$alice` and `$bob` header tables with each bearer token
- [ ] Alice BUY via `POST /api/v1/trades` (with an `Idempotency-Key` header) returns 201
- [ ] Alice `GET /api/v1/trades/{id}` returns her trade
- [ ] Bob `GET /api/v1/trades/{id}` for Alice's trade returns 404
- [ ] Bob's `GET /api/v1/trades` and `/api/v1/holdings` have `total` 0
- [ ] `GET /api/v1/trades` with no token returns 401
- [ ] Bob's token plus an `X-User-Id` header for Alice still returns only Bob's data

## 5.5 Gateway and versioning

- [ ] Run TradeService with `--urls http://localhost:5100` and the gateway with `--urls http://localhost:5080`, same `Jwt__SigningKey` in both
- [ ] With `$base = "http://localhost:5080"`, repeat the 5.1 to 5.3 checks through the gateway; results must be identical
- [ ] `GET /api/v1/instruments` returns a GUID in the `X-Correlation-Id` response header
- [ ] A GUID sent as `X-Correlation-Id` is echoed back; the `traceId` in a 401 body equals it
- [ ] `GET /api/v2/trades` returns 404

## 5.6 Rate limiting and CORS

- [ ] 70 quick `GET $base/api/v1/instruments` calls through the gateway: about 60 return 200, the rest 429
- [ ] 8 quick login attempts with a wrong password: five 401, then 429 with a `Retry-After` header
- [ ] After the lockout (wait about a minute or restart the gateway), a correct login works again
- [ ] A preflight from `http://localhost:5173` (`Invoke-WebRequest -Method Options` with `Origin`, `Access-Control-Request-Method: POST` headers) returns 204 with `Access-Control-Allow-Origin`
- [ ] The same preflight from `http://evil.example` returns no `Access-Control-Allow-Origin`

## 5.7 Security hardening

- [ ] A 100 KB request body to `POST /api/v1/auth/login` returns 413
- [ ] Malformed JSON to login returns 400 in the standard shape, with no stack trace
- [ ] `GET /health` through the gateway has nosniff, frame-deny and referrer-policy headers, and no `Server` header
- [ ] With PostgreSQL stopped, an authenticated `GET /api/v1/trades` returns 500 with only a trace id (restart PostgreSQL afterwards)

## 6.2 Outbox in the same transaction

- [ ] After one BUY through the gateway, `select event_id, event_type, created_at, published_at, attempts from outbox_messages order by created_at desc limit 5;` shows exactly one new row with `published_at` empty and `attempts` 0
- [ ] The `payload` column of that row is valid JSON and matches spec section 11 (same fields, `executedAt` ending in Z)
- [ ] A rejected oversell (SELL more than owned) adds no outbox row
- [ ] Sending the same BUY twice with the same Idempotency-Key leaves exactly one trade row and one outbox row

## 6.3 RabbitMQ topology

- [ ] After a fresh `docker compose up -d rabbitmq`, the management UI (http://localhost:15672) lists exchanges `tradefolio.events` (topic) and `tradefolio.events.dlx` (direct), both durable
- [ ] Queues `notification.trade-completed` and `notification.trade-completed.dlq` exist, both durable, and the main queue shows a dead-letter exchange of `tradefolio.events.dlx`
- [ ] Bindings: `tradefolio.events` routes `trade.completed` to the main queue, and `tradefolio.events.dlx` routes `trade.completed` to the DLQ
- [ ] After `docker compose down -v` and `docker compose up -d rabbitmq`, the same topology is back without any manual step

(Each task adds its manual checks here as it is finished.)

