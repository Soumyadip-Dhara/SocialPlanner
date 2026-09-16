# Gatherly — Social Planning, Ordering & Expense Splitting Platform

Gatherly is a production-quality web application for planning group events, managing budgets, splitting expenses, and coordinating activities.

## Technology Stack

| Layer | Technology |
|---|---|
| Backend API | .NET 9 / ASP.NET Core Web API |
| ORM | Entity Framework Core 9 |
| Database | PostgreSQL 17 |
| Authentication | JWT + Refresh Tokens |
| Frontend | Angular 20 (Standalone Components) |
| Containers | Docker / Docker Compose |

## Project Structure

```
Gatherly/
├── src/
│   ├── Gatherly.Api/           # Controllers, Middleware, Extensions
│   ├── Gatherly.Services/      # Business logic (Service layer)
│   ├── Gatherly.Repositories/  # Data access, EF Core, entities
│   └── Gatherly.Contracts/     # DTOs shared across layers
├── tests/
│   ├── Gatherly.UnitTests/
│   └── Gatherly.IntegrationTests/
├── frontend/
│   └── gatherly-ui/            # Angular 20 application
├── docker/
│   ├── docker-compose.yml
│   └── Dockerfile.api
└── docs/
```

## Quick Start

### Prerequisites
- .NET 9 SDK
- Node.js 22+
- Docker & Docker Compose
- PostgreSQL 17 (or use Docker)

### Run with Docker Compose

```bash
cd docker
JWT_SECRET_KEY=your-super-secret-key-change-in-production docker compose up -d
```

API available at: `http://localhost:8080`
Swagger UI: `http://localhost:8080/swagger`

### Run Locally (development)

1. **Start PostgreSQL**
   ```bash
   docker run -e POSTGRES_USER=gatherly -e POSTGRES_PASSWORD=gatherly_secret \
     -e POSTGRES_DB=gatherly_dev -p 5432:5432 postgres:17-alpine
   ```

2. **Apply EF Core migrations**
   ```bash
   cd src/Gatherly.Api
   dotnet ef database update
   ```

3. **Run the API**
   ```bash
   cd src/Gatherly.Api
   dotnet run
   ```

4. **Run the frontend**
   ```bash
   cd frontend/gatherly-ui
   npm install
   ng serve
   ```

### Run Tests

```bash
# Unit tests (no database required)
dotnet test tests/Gatherly.UnitTests

# Integration tests (no database required — uses InMemory EF Core)
dotnet test tests/Gatherly.IntegrationTests
```

## Architecture

The backend strictly follows a **3-layer architecture**:

```
Controller -> Service -> Repository -> PostgreSQL
```

- **Controllers** — HTTP request/response, validation, auth claims extraction
- **Services** — All business logic, calculations, workflow coordination
- **Repositories** — All database access via EF Core

## API Endpoints (Phase 2: Authentication)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/v1/auth/register` | Public | Register new user |
| POST | `/api/v1/auth/login` | Public | Login, get tokens |
| POST | `/api/v1/auth/refresh` | Public | Refresh access token |
| POST | `/api/v1/auth/logout` | Public | Revoke refresh token |
| POST | `/api/v1/auth/forgot-password` | Public | Request password reset |
| POST | `/api/v1/auth/reset-password` | Public | Reset password with token |
| POST | `/api/v1/auth/change-password` | JWT | Change password |
| GET  | `/api/v1/auth/me` | JWT | Get current user profile |
| GET  | `/health` | Public | Health check |

## Security

- JWT access tokens (short-lived, 15 min)
- Refresh token rotation with revocation
- Refresh tokens stored as SHA-256 hashes
- Password hashing with BCrypt
- Generic authentication errors (prevents enumeration)
- IP address tracking for login activities
- CORS restricted to configured origins

## Development Roadmap

| Phase | Feature |
|-------|---------|
| 0 | Architecture and Planning |
| 1 | Project Foundation |
| 2 | Authentication and JWT |
| 3 | Users and Friends |
| 4 | Events and Invitations |
| 5 | Polls, Tasks and Reminders |
| 6 | Budget and Expenses |
| 7 | Expense Splitting |
| 8 | Settlement |
| 9 | Ordering |
| 10 | Production Readiness |

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | — |
| `JwtSettings__SecretKey` | JWT signing key (min 32 chars) | — |
| `JwtSettings__Issuer` | JWT issuer | `Gatherly` |
| `JwtSettings__Audience` | JWT audience | `GatherlyUsers` |
| `JwtSettings__AccessTokenExpiryMinutes` | Access token TTL | `15` |
| `JwtSettings__RefreshTokenExpiryDays` | Refresh token TTL | `7` |
| `Cors__AllowedOrigins__0` | Allowed CORS origin | `http://localhost:4200` |

> Never commit real secrets. Use environment variables or a secrets manager in production.
