

Requirements checklist · MD
Requirements Checklist
Tracks every functional requirement (FR-01 to FR-22) from the TradeFolio specification.

Status values: Not started, In progress, Done. Rule: a requirement is only Done when it has a passing test or a working demo step. Fill in the last two columns as you build.

ID	Requirement	Built in task	Status	Code location	Test or demo
FR-01	Registration: validated, passwords never stored in plain text	5.1	Not started		
FR-02	Authentication: login returns a JWT	5.2	Not started		
FR-03	User isolation: users only access their own data	5.3, 5.4, 7.6	Not started		
FR-04	Trades: create, read, list with validation	3.5, 3.6, 3.8	Not started		
FR-05	Validation: reject invalid symbols, quantities, prices, sides	3.4, 10.4	Not started		
FR-06	No overselling, including under concurrent requests	3.6, 4.3, 4.4	Not started		
FR-07	Atomicity: trade and portfolio changes commit together	3.5, 3.6	Not started		
FR-08	FIFO: partial sells consume the oldest lots first	2.1 to 2.4, 3.6	Not started		
FR-09	Events: a completed trade produces TradeCompleted	6.1, 6.2, 6.4, 6.5	Not started		
FR-10	Notifications: service consumes events and creates a notification	7.1	Not started		
FR-11	Idempotency: repeated delivery does not duplicate notifications	7.2	Not started		
FR-12	Retry and DLQ: transient failures retry, exhausted ones go to a DLQ	6.3, 7.4	Not started		
FR-13	Market data: prices through a free provider adapter	8.1, 8.4	Not started		
FR-14	Redis: cache prices with TTL and defined failure behavior	8.2, 8.3	Not started		
FR-15	Analytics: P&L, allocation, sectors, frequency, performers	9.1 to 9.5, 9.7	Not started		
FR-16	Insights: deterministic personalized insights	9.6	Not started		
FR-17	Dashboard: React UI for portfolio, analytics, trades, notifications	10.1 to 10.7	Not started		
FR-18	BI: Power BI or Tableau on the same data	11.1 to 11.3	Not started		
FR-19	Pagination: pagination with stable ordering on collections	1.4, 3.8	In progress (types done, endpoints pending)	src/BuildingBlocks/Common/Pagination	tests/Common.UnitTests/PaginationTests.cs
FR-20	Errors: consistent ProblemDetails-style responses	1.4, 3.4	In progress (ApiProblem done, not yet used by real validation)	src/BuildingBlocks/Common/Errors/ApiProblem.cs	tests/Common.UnitTests/ApiProblemTests.cs
FR-21	Rate limiting: token bucket or equivalent	5.6	Not started		
FR-22	Versioning: public APIs start at /api/v1	1.3, 5.5	In progress (/api/v1/ping exists, gateway pending)	src/Services/TradeService/Program.cs	
Final check
Before calling the project finished, also go through the Definition of Done (spec section 26) and the demo script (spec section 27).
