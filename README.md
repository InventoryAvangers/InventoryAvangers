# Inventory Avengers 🛡️ (StockPilot)

A full-stack inventory management system built with Node.js, Express, MongoDB, and **React + Vite**.

## Features

- 🔐 **Authentication & Role-Based Access** — Owner, Manager, Staff roles with JWT
- 📦 **Inventory Management** — Full product CRUD, auto-SKU generation, barcode generation & download
- 🔲 **Barcode Scanning** — Scan barcodes in POS to instantly add products to cart
- 🧾 **Receipt Preview & PDF Download** — Full receipt preview after checkout + downloadable PDF
- 🏪 **Multi-Store Support** — Inventory per store; owner can manage all stores, staff/managers scoped to their store
- 🛒 **Point of Sale (POS)** — Real-time cart, product grid, barcode scanner, checkout
- 📊 **Reports & Analytics** — Revenue, profit, date-range filters, Chart.js graphs
- ↩️ **Returns & Refunds** — Process returns, auto-restock inventory
- ✅ **Approval Workflow** — Managers request deletions; owners approve/reject
- ⚡ **React + Vite SPA** — Hot reload in development, optimized production build with custom CSS

```bash
# 1. Clone the repository
git clone <repo-url>
cd inve-proto

# 2. Install backend dependencies
cd backend
npm install

# 3. Configure environment
cp ../.env.example .env
# Edit .env and set MONGO_URI and JWT_SECRET

# 4. Install frontend dependencies (for development/build)
cd ../frontend
npm install
```

### Running in Development

Open two terminals:

```bash

# Terminal 2 — frontend (Vite dev server with HMR + API proxy)
cd frontend
npm run dev        # starts Vite on port 5173 (proxies /api → 5000)
```

Visit **http://localhost:5173** for development.

### Building for Production

```bash
cd frontend
npm run build      # outputs to frontend/dist/
```

### Initialize Demo Data

Visit the login page and click **"Initialize Demo Data"**, or:

```bash
curl http://localhost:5000/api/auth/seed
```

## Demo Credentials

| Role    | Email               | Password    |
|---------|---------------------|-------------|
| Owner   | owner@demo.com      | password123 |
| Manager | manager@demo.com    | password123 |
| Staff   | staff@demo.com      | password123 |

## Project Structure



## Role Permissions

| Action | Owner | Manager | Staff |
|--------|-------|---------|-------|
| View products | ✅ | ✅ | ✅ |
| Add/Edit products | ✅ | ✅ | ❌ |
| Delete products | ✅ | ⚠️ (approval) | ❌ |
| Process sales | ✅ | ✅ | ✅ |
| Scan barcodes | ✅ | ✅ | ✅ |
| View reports | ✅ | ✅ | ❌ |
| Approve requests | ✅ | ❌ | ❌ |
| Manage stores | ✅ | ❌ | ❌ |
| Adjust inventory | ✅ | ✅ | ❌ |
| Switch stores | ✅ | ❌ | ❌ |
