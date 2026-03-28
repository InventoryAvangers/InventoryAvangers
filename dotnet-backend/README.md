# InventoryAvengers.API — ASP.NET Core Backend

This is the migrated ASP.NET Core Web API backend for Inventory Avengers, replacing the Node.js (Express) backend while preserving all existing API contracts.

## Architecture

```
dotnet-backend/
├── Controllers/        # Route handlers (maps 1:1 to Node Express routes)
├── Data/               # MongoDB context
├── DTOs/               # Request/response data transfer objects
├── Middleware/         # Request logging, global exception handling
├── Models/             # C# entity classes (MongoDB BSON-mapped)
├── Services/           # Business logic (AuthService, HelperService)
├── Program.cs          # App startup, DI configuration
└── appsettings.json    # Configuration (override via env vars)
```

## Architecture Mapping

| Node.js (Original)       | ASP.NET Core (This)         |
|--------------------------|-----------------------------|
| `server.js`              | `Program.cs`                |
| `middleware/auth.js`     | JWT Bearer middleware        |
| `middleware/logger.js`   | `RequestLoggingMiddleware`  |
| `utils/errorHandler.js`  | `GlobalExceptionMiddleware` |
| `middleware/featureCheck`| Feature gating (inline)     |
| `models/*.js`            | `Models/*.cs`               |
| `routes/auth.js`         | `AuthController`            |
| `routes/products.js`     | `ProductsController`        |
| `routes/sales.js`        | `SalesController`           |
| `routes/returns.js`      | `ReturnsController`         |
| `routes/reports.js`      | `ReportsController`         |
| `routes/inventory.js`    | `InventoryController`       |
| `routes/employees.js`    | `EmployeesController`       |
| `routes/stores.js`       | `StoresController`          |
| `routes/notifications.js`| `NotificationsController`   |
| `routes/approvals.js`    | `ApprovalsController`       |
| `routes/auditLogs.js`    | `AuditLogsController`       |
| `routes/settings.js`     | `SettingsController`        |
| `routes/billing.js`      | `BillingController`         |
| `routes/messages.js`     | `MessagesController`        |
| `routes/superuser.js`    | `SuperuserController`       |

## Setup

### Prerequisites
- .NET 10 SDK
- MongoDB connection string

### Environment Variables
```bash
MONGO_URI=mongodb+srv://user:pass@cluster.mongodb.net/inventory-avengers
JWT_SECRET=your_super_secret_jwt_key_here
JWT_EXPIRES_IN=7d
```

### Run
```bash
cd dotnet-backend
dotnet run
```

The API listens on port 5000 by default (configurable via `$PORT` or `appsettings.json`).

## API Compatibility

All endpoints, response shapes, HTTP status codes, and JWT token structure are preserved from the original Node.js backend. The frontend requires **zero changes**.

### Authentication
- JWT Bearer token in `Authorization: Bearer <token>` header
- Token claims: `id`, `name`, `email`, `role`, `storeId`
- Same expiry defaults (`7d`)

## Database
Uses MongoDB (same database as Node.js backend). No data migration required — the .NET driver reads the same BSON documents written by Mongoose.

## NuGet Packages
- `MongoDB.Driver` — MongoDB client
- `Microsoft.AspNetCore.Authentication.JwtBearer` — JWT middleware
- `BCrypt.Net-Next` — password hashing (bcrypt, compatible with Mongoose bcryptjs)
- `AspNetCoreRateLimit` — rate limiting (mirrors express-rate-limit)
