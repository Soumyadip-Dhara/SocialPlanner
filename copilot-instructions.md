# Gatherly — Copilot Instructions

## Architecture Rules

This project uses a **strict 3-layer backend architecture**. Never violate these rules:

```
Controller → Service → Repository → PostgreSQL
```

### Layer Responsibilities

| Layer | Can Do | Cannot Do |
|-------|--------|-----------|
| **Controller** | HTTP binding, call services, return responses, extract claims | Business logic, DB access, direct repo calls |
| **Service** | Business logic, coordinate repos, transform DTOs, throw domain exceptions | Raw SQL, direct DbContext, HTTP concerns |
| **Repository** | EF Core queries, CRUD, filtering, pagination | Business logic, HTTP concerns |

## Tech Stack (Do Not Change Without Approval)

- Backend: **.NET 9** / ASP.NET Core Web API / C#
- ORM: **Entity Framework Core 9** with Npgsql
- Database: **PostgreSQL 17**
- Auth: **JWT ****** Refresh Tokens** (no ASP.NET Identity)
- Frontend: **Angular 20** standalone components, TypeScript strict mode
- Testing: **xUnit** + Moq + FluentAssertions
- Logging: **Serilog**
- Validation: **FluentValidation**
- Docs: **Swagger / OpenAPI**

## Coding Standards

### Backend
- Use `async/await` for all I/O operations
- Use `CancellationToken` parameters on all async public methods
- Use `AsNoTracking()` for read-only EF Core queries
- Use `decimal` / `numeric` for ALL monetary values — never `float` or `double`
- Use `DateTime.UtcNow` — never `DateTime.Now`
- Use `timestamptz` for all PostgreSQL timestamp columns
- Use DTOs for all API contracts — never expose EF entities from controllers
- Use dependency injection — never `new` a service directly
- Place ALL database logic in repositories — never in services or controllers
- Use database transactions for multi-step financial operations
- Use `ILogger<T>` — never `Console.WriteLine` for logging
- Log exceptions with context but never log passwords, tokens, or secrets

### Security
- Never store plaintext passwords — always BCrypt
- Store refresh tokens as SHA-256 hashes only
- Validate JWT issuer, audience, signature, and lifetime
- Keep JWT signing keys in environment variables — never in source code
- Apply `[Authorize]` on all protected endpoints
- Check ownership/membership on every data access — never trust client-provided IDs alone
- Use generic error messages for auth failures to prevent enumeration
- Validate and sanitize ALL inputs with FluentValidation

### Financial Logic
- Use `decimal` for all money — scale 2 for display, scale 4 for intermediary calculations
- Total allocated shares MUST equal expense total (within rounding tolerance)
- Use database transactions for all financial write operations
- Never silently create or lose money due to rounding
- All expense/settlement state changes must be auditable

### Testing
- Unit tests: mock all dependencies with Moq
- Integration tests: use `GatherlyWebApplicationFactory` (InMemory EF, no PostgreSQL needed)
- One test class per service/controller
- Test happy path AND error/edge cases
- Financial calculation tests must be extensive

## File Naming and Locations

```
src/Gatherly.Api/Controllers/         → XxxController.cs
src/Gatherly.Services/Interfaces/     → IXxxService.cs
src/Gatherly.Services/Implementations/→ XxxService.cs
src/Gatherly.Repositories/Interfaces/ → IXxxRepository.cs
src/Gatherly.Repositories/Implementations/ → XxxRepository.cs
src/Gatherly.Repositories/Persistence/Entities/ → Xxx.cs
src/Gatherly.Repositories/Persistence/Configurations/ → (EntityConfigurations.cs)
src/Gatherly.Contracts/Xxx/           → XxxRequests.cs / XxxResponses.cs
tests/Gatherly.UnitTests/Services/Xxx/ → XxxServiceTests.cs
tests/Gatherly.IntegrationTests/Xxx/  → XxxControllerTests.cs
```

## API Response Structure

Always return `ApiResponse<T>` or `ApiResponse`:

```csharp
// Success with data
return Ok(ApiResponse<UserDto>.Ok(data));

// Success no data
return Ok(ApiResponse.Ok("Message"));

// Error
return BadRequest(ApiResponse.Fail("Validation error"));
```

## Exception Handling Pattern

```csharp
// Controller
try
{
    var result = await _service.DoSomethingAsync(request, userId);
    return Ok(ApiResponse<ResultDto>.Ok(result));
}
catch (ArgumentException ex)     { return BadRequest(ApiResponse.Fail(ex.Message)); }
catch (KeyNotFoundException ex)  { return NotFound(ApiResponse.Fail(ex.Message)); }
catch (UnauthorizedAccessException ex) { return Unauthorized(ApiResponse.Fail(ex.Message)); }
catch (Exception ex)
{
    _logger.LogError(ex, "Error doing something for user {UserId}", userId);
    return StatusCode(500, ApiResponse.Fail("An unexpected error occurred."));
}
```

## DI Registration

Add new services/repos to:
- `src/Gatherly.Services/DependencyInjection.cs`
- `src/Gatherly.Repositories/DependencyInjection.cs`

## Development Phases

| Phase | Status | Description |
|-------|--------|-------------|
| 0 | ✅ Done | Architecture and Planning |
| 1 | ✅ Done | Project Foundation |
| 2 | ✅ Done | Authentication and JWT |
| 3 | 🔜 Next | Users and Friends |
| 4 | 🔜 | Events and Invitations |
| 5 | 🔜 | Polls, Tasks, Reminders |
| 6 | 🔜 | Budget and Expenses |
| 7 | 🔜 | Expense Splitting |
| 8 | 🔜 | Settlement |
| 9 | 🔜 | Ordering |
| 10 | 🔜 | Production Readiness |

## What NOT to Do

- Do NOT use MongoDB, SQL Server, MediatR, CQRS, or Clean Architecture
- Do NOT use `float`/`double` for money
- Do NOT expose EF Core entities directly from API
- Do NOT put business logic in controllers
- Do NOT put DB queries in services
- Do NOT put HTTP concerns in services or repositories
- Do NOT use `DateTime.Now` — use `DateTime.UtcNow`
- Do NOT commit secrets or connection strings with real credentials
- Do NOT use `Console.WriteLine` — use Serilog `ILogger<T>`
- Do NOT mark payments as successful based only on frontend input
