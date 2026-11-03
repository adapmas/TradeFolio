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



(Each task adds its manual checks here as it is finished.)

