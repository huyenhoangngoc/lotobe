# CLAUDE.md - Backend (.NET 10)

## Overview
Lo To Online backend - .NET 10 API voi SignalR real-time, Clean Architecture.

## Tech Stack
- .NET 10 (latest)
- ASP.NET Core Web API
- SignalR (real-time communication)
- Npgsql / Dapper (Supabase PostgreSQL)
- Serilog (structured logging)
- Swagger/OpenAPI (API docs)
- FluentValidation (input validation)
- Mapster (object mapping)

## Architecture: Clean Architecture

```
src/
├── LoTo.Domain/          # Entities, Enums, Interfaces (KHONG dependency nao)
├── LoTo.Application/     # Use Cases, DTOs, Validators (chi depend Domain)
├── LoTo.Infrastructure/  # DB, SignalR, External Services (depend Application)
└── LoTo.WebApi/          # Controllers, Middleware, Config (depend tat ca)
```

### Dependency Rules
- Domain KHONG reference bat ky project nao khac
- Application chi reference Domain
- Infrastructure reference Application (va Domain gian tiep)
- WebApi reference tat ca, nhung chi de DI registration

## Code Standards

### Naming
- Files/Classes: `PascalCase`
- Methods: `PascalCase`
- Variables/Fields: `camelCase`
- Private fields: `_camelCase`
- Constants: `UPPER_SNAKE_CASE` hoac `PascalCase`
- Interfaces: `IPascalCase`

### Patterns
- CQRS-lite: tach Command/Query trong Application layer
- Repository pattern cho data access
- Service pattern cho business logic
- Mediator pattern (optional, MediatR) cho use cases

### Rules
- Moi Controller method phai co `[ProducesResponseType]` attributes
- Moi endpoint phai co Swagger documentation
- Input validation dung FluentValidation, KHONG validate trong controller
- Exception handling qua global middleware, KHONG try-catch trong controller
- Logging: dung ILogger, structured logging voi Serilog
- Async/await cho tat ca I/O operations
- CancellationToken cho tat ca async methods

### SignalR Rules
- 1 Hub duy nhat: `GameHub`
- Group per room: `Room_{roomCode}`
- Dung `HubException` cho errors, KHONG throw raw exceptions
- Connection mapping luu in-memory (ConcurrentDictionary)
- Auto-reconnect: 30s timeout truoc khi remove player

### Security
- JWT authentication cho hosts
- Session token cho players (anonymous)
- Rate limiting tren tat ca endpoints
- CORS whitelist
- Input sanitization
- MoMo signature verification (HMAC SHA256)

## Database
- Supabase PostgreSQL
- Connection string: env var `DATABASE_URL` hoac `SUPABASE_CONNECTION_STRING`
- Dung parameterized queries (KHONG BAO GIO string concatenation)
- Service role key cho backend operations (bypass RLS)

## Environment Variables
```
SUPABASE_URL=
SUPABASE_SERVICE_KEY=
SUPABASE_CONNECTION_STRING=
GOOGLE_CLIENT_ID=
GOOGLE_CLIENT_SECRET=
JWT_SECRET=
JWT_ISSUER=
MOMO_PARTNER_CODE=
MOMO_ACCESS_KEY=
MOMO_SECRET_KEY=
MOMO_API_URL=
CORS_ORIGINS=
```

## Commands
```bash
# Run
dotnet run --project src/LoTo.WebApi

# Test
dotnet test

# Build
dotnet build

# Swagger
# Navigate to /swagger khi chay
```

## File Naming
- Controllers: `{Entity}Controller.cs`
- Services: `{Name}Service.cs`
- Repositories: `{Entity}Repository.cs`
- DTOs: `{Action}{Entity}Dto.cs` (vd: `CreateRoomDto.cs`)
- Validators: `{Dto}Validator.cs`
- Hubs: `{Name}Hub.cs`
