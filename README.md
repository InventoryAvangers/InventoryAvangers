# Inventory Avengers 🛡️ (StockPilot)

A full-stack inventory management system built with **ASP.NET Core**, **MongoDB**, and **React + Vite**.

## Features

- 🔐 **Authentication & Role-Based Access** — Superuser, Owner, Manager, Staff roles with JWT
- 📦 **Inventory Management** — Full product CRUD, auto-SKU generation, barcode generation & download
- 🧾 **Receipt Preview & PDF Download** — Full receipt preview after checkout + downloadable PDF
- 🛒 **Point of Sale (POS)** — Real-time cart, product grid, barcode scanner, checkout
- 📊 **Reports & Analytics** — Revenue, profit, date-range filters, Chart.js graphs
- ↩️ **Returns & Refunds** — Process returns, auto-restock inventory
- ✅ **Approval Workflow** — Managers request deletions; owners approve/reject
- 💬 **Support Messaging** — In-app messaging between users and support
- ⚡ **React + Vite SPA** — Hot reload in development, optimized production build

## Project Structure

```
InventoryAvangers/
├── dotnet-backend/          # ASP.NET Core Web API (C#)
│   ├── Controllers/         # API route handlers
│   ├── Models/              # MongoDB entity models
│   ├── Services/            # Business logic (Auth, Seed, Helpers)
│   ├── Data/                # MongoDB context
│   ├── DTOs/                # Request/response models
│   ├── Middleware/           # Logging, exception handling
│   └── Program.cs           # App entry point & DI config
├── frontend/                # React + Vite SPA
│   ├── src/
│   │   ├── pages/           # Route-level page components
│   │   ├── components/      # Shared UI & layout components
│   │   ├── api/             # Axios API client
│   │   ├── store/           # Zustand state management
│   │   ├── css/             # Component stylesheets
│   │   └── utils/           # Utility functions
│   ├── index.html           # SPA entry point
│   └── vite.config.js       # Vite configuration
├── .env                     # Environment variables
└── vercel.json              # Vercel deployment config
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/) & npm
- MongoDB instance (local or Atlas)

### 1. Clone & Configure

```bash
git clone <repo-url>
cd InventoryAvangers

# Configure environment variables
cp .env .env.local
# Edit .env and set MONGO_URI, JWT_SECRET, etc.
```

### 2. Start the Backend

```bash
cd dotnet-backend
dotnet run
# API runs on http://localhost:5000
```

### 3. Start the Frontend

```bash
cd frontend
npm install
npm run dev
# Dev server runs on http://localhost:5173 (proxies /api → :5000)
```

### 4. Initialize Demo Data

Visit the login page and click **"Initialize Demo Data"**, or:

```bash
curl http://localhost:5000/api/auth/seed
```

## Demo Credentials

| Role    | Email              | Password      |
|---------|--------------------|---------------|
| Owner   | owner@demo.com     | password123   |
| Manager | manager@demo.com   | password123   |
| Staff   | staff@demo.com     | password123   |

## Environment Variables

| Variable                 | Description                      | Default   |
|--------------------------|----------------------------------|-----------|
| `PORT`                   | Backend server port              | `5000`    |
| `MONGO_URI`              | MongoDB connection string        | —         |
| `JWT_SECRET`             | Secret key for JWT signing       | —         |
| `JWT_EXPIRES_IN`         | JWT token expiry                 | `7d`      |
| `REFRESH_TOKEN_SECRET`   | Secret for refresh tokens        | —         |
| `REFRESH_TOKEN_EXPIRES_IN` | Refresh token expiry           | `90d`     |

## Role Permissions

| Action              | Owner | Manager        | Staff |
|---------------------|-------|----------------|-------|
| View products       | ✅    | ✅             | ✅    |
| Add/Edit products   | ✅    | ✅             | ❌    |
| Delete products     | ✅    | ⚠️ (approval)  | ❌    |
| Process sales       | ✅    | ✅             | ✅    |
| View reports        | ✅    | ✅             | ❌    |
| Approve requests    | ✅    | ❌             | ❌    |
| Manage stores       | ✅    | ❌             | ❌    |
| Adjust inventory    | ✅    | ✅             | ❌    |

## Deployment

### Vercel (Frontend)

The project includes a `vercel.json` for deploying the frontend SPA to Vercel. Update the API rewrite destination to point to your deployed backend URL.

### Backend

Deploy the .NET backend to any hosting provider that supports ASP.NET Core (Azure App Service, Railway, Render, etc.). Ensure environment variables are configured.

## Tech Stack

| Layer     | Technology                                                   |
|-----------|--------------------------------------------------------------|
| Frontend  | React 18, Vite 5, Zustand, Axios, Chart.js, React Router 6  |
| Backend   | ASP.NET Core (.NET 10), C#                                   |
| Database  | MongoDB (via MongoDB.Driver)                                 |
| Auth      | JWT Bearer tokens, BCrypt password hashing                   |
| Tooling   | JsBarcode, html5-qrcode, jsPDF, react-icons                 |

