# InventoryAvengers.API — ASP.NET Core Backend

ASP.NET Core Web API backend for Inventory Avengers, migrated from Node.js/Express while preserving all existing API contracts.

## Architecture

```
dotnet-backend/
├── Controllers/        # API route handlers (16 controllers)
├── Data/               # MongoDbContext — database access
├── DTOs/               # Request/response data transfer objects
├── Middleware/          # Request logging, global exception handling
├── Models/             # C# entity classes (MongoDB BSON-mapped)
├── Services/           # Business logic
│   ├── AuthService     # JWT token generation & validation
│   ├── HelperService   # Shared utility methods
│   └── SeedService     # Demo data initialization
├── Program.cs          # App startup & dependency injection
└── appsettings.json    # Configuration (override via env vars)
```

## Controllers

| Controller                | Route Prefix               | Description                          |
|---------------------------|----------------------------|--------------------------------------|
| `AuthController`          | `/api/auth`                | Login, register, token refresh, seed |
| `ProductsController`      | `/api/products`            | Product CRUD, search, bulk ops       |
| `SalesController`         | `/api/sales`               | Checkout, transaction history        |
| `ReturnsController`       | `/api/returns`             | Return processing, inventory restock |
| `ReportsController`       | `/api/reports`             | Revenue, profit, analytics queries   |
| `InventoryController`     | `/api/inventory`           | Stock adjustments, audit trail       |
| `EmployeesController`     | `/api/employees`           | Staff management, role assignments   |
| `StoresController`        | `/api/stores`              | Multi-store CRUD, store switching    |
| `ApprovalsController`     | `/api/approvals`           | Deletion approval workflow           |
| `NotificationsController` | `/api/notifications`       | In-app notification management       |
| `AuditLogsController`     | `/api/audit-logs`          | Activity log queries                 |
| `SettingsController`      | `/api/settings`            | Feature flags & app configuration    |
| `BillingController`       | `/api/billing`             | Subscription & billing management    |
| `MessagesController`      | `/api/messages`            | Support messaging system             |
| `SuperuserController`     | `/api/superuser`           | Platform-wide admin operations       |
| `HealthController`        | `/api/health`              | Health check endpoint                |

## Models

| Model            | Collection           | Description                    |
|------------------|----------------------|--------------------------------|
| `User`           | `users`              | User accounts & credentials    |
| `Product`        | `products`           | Product catalog                |
| `Sale`           | `sales`              | Sales transactions             |
| `Return`         | `returns`            | Return/refund records          |
| `Store`          | `stores`             | Store information              |
| `Inventory`      | `inventories`        | Stock levels per product/store |
| `Approval`       | `approvals`          | Pending deletion requests      |
| `AuditLog`       | `auditlogs`          | Audit trail entries            |
| `Notification`   | `notifications`      | User notifications             |
| `Message`        | `messages`           | Support messages               |
| `Coupon`         | `coupons`            | Discount coupons               |
| `Subscription`   | `subscriptions`      | Billing subscriptions          |
| `FeatureFlag`    | `featureflags`       | Feature toggles                |
| `AccessRequest`  | `accessrequests`     | Access request records         |
| `ActivityLog`    | `activitylogs`       | User activity logs             |
| `SuperuserRole`  | `superuserroles`     | Superuser role definitions     |

## Setup

### Prerequisites

- .NET 10 SDK
- MongoDB connection string

### Environment Variables

```bash
MONGO_URI=mongodb+srv://user:pass@cluster.mongodb.net/inventory-avengers
JWT_SECRET=your_super_secret_jwt_key_here
JWT_EXPIRES_IN=7d
REFRESH_TOKEN_SECRET=your_refresh_secret
REFRESH_TOKEN_EXPIRES_IN=90d
```

### Run

```bash
cd dotnet-backend
dotnet run
```

The API listens on **port 5000** by default (configurable via `$PORT` or `appsettings.json`).

## Architecture Mapping (from Node.js)

| Node.js (Original)        | ASP.NET Core (This)          |
|----------------------------|------------------------------|
| `server.js`                | `Program.cs`                 |
| `middleware/auth.js`       | JWT Bearer middleware        |
| `middleware/logger.js`     | `RequestLoggingMiddleware`   |
| `utils/errorHandler.js`   | `GlobalExceptionMiddleware`  |
| `models/*.js`              | `Models/*.cs`                |
| `routes/*.js`              | `Controllers/*Controller.cs` |

## API Compatibility

All endpoints, response shapes, HTTP status codes, and JWT token structure are preserved from the original Node.js backend. The frontend requires **zero changes**.

### Authentication

- JWT Bearer token in `Authorization: Bearer <token>` header
- Token claims: `id`, `name`, `email`, `role`, `storeId`
- Default expiry: `7d`

## Database

Uses MongoDB (same database as Node.js backend). No data migration required — the .NET driver reads the same BSON documents written by Mongoose.

## NuGet Packages

| Package                                         | Purpose                                    |
|--------------------------------------------------|--------------------------------------------|
| `MongoDB.Driver`                                 | MongoDB client                             |
| `Microsoft.AspNetCore.Authentication.JwtBearer`  | JWT middleware                             |
| `BCrypt.Net-Next`                                | Password hashing (bcrypt-compatible)       |
| `AspNetCoreRateLimit`                            | Rate limiting (mirrors express-rate-limit) |
