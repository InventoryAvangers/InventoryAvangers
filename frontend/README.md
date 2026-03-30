# StockPilot Frontend

React + Vite single-page application for the Inventory Avengers inventory management system.

## Tech Stack

- **React 18** — UI library
- **Vite 5** — Build tool with HMR
- **React Router 6** — Client-side routing
- **Zustand** — Lightweight state management
- **Axios** — HTTP client for API calls
- **Chart.js / react-chartjs-2** — Reports & analytics charts
- **JsBarcode** — Barcode generation
- **html5-qrcode** — Barcode scanner (POS)
- **jsPDF** — Receipt PDF generation
- **react-icons** — Icon library

## Project Structure

```
frontend/
├── src/
│   ├── api/                # Axios instance & API helpers
│   ├── components/
│   │   ├── layout/         # Sidebar, Header layout components
│   │   └── ui/             # Reusable UI components
│   ├── pages/              # Route-level page components
│   │   ├── Dashboard.jsx       # Overview dashboard with charts
│   │   ├── Inventory.jsx       # Product management (CRUD)
│   │   ├── Sales.jsx           # Point of Sale with barcode scanner
│   │   ├── Reports.jsx         # Revenue/profit analytics
│   │   ├── Returns.jsx         # Returns & refunds processing
│   │   ├── EmployeeManagement.jsx  # Staff management
│   │   ├── Stores.jsx         # Store management (multi-store)
│   │   ├── OwnerStores.jsx    # Owner-level store admin
│   │   ├── Settings.jsx       # Application settings
│   │   ├── AuditLog.jsx       # Activity audit trail
│   │   ├── SupportMessages.jsx # Support messaging
│   │   ├── SuperuserPanel.jsx  # Superuser administration
│   │   ├── Login.jsx / Register.jsx  # Authentication
│   │   └── Landing.jsx        # Public landing page
│   ├── store/              # Zustand state stores
│   ├── css/                # Component-level stylesheets
│   ├── styles/             # Global/shared styles
│   ├── utils/              # Utility functions
│   ├── App.jsx             # Root component with routing
│   ├── main.jsx            # App entry point
│   └── index.css           # Global CSS reset
├── index.html              # SPA HTML shell
├── vite.config.js          # Vite config (dev proxy → :5000)
└── package.json
```

## Getting Started

### Prerequisites

- Node.js 18+
- npm

### Install & Run

```bash
npm install
npm run dev
```

The dev server starts on **http://localhost:5173** with hot module replacement. API requests to `/api/*` are proxied to `http://localhost:5000`.

### Build for Production

```bash
npm run build     # Output → dist/
npm run preview   # Preview the production build locally
```

## API Proxy

In development, Vite proxies all `/api` requests to the backend:

```js
// vite.config.js
proxy: {
  '/api': {
    target: 'http://localhost:5000',
    changeOrigin: true,
  }
}
```

## Key Pages

| Page               | Route              | Access          |
|--------------------|--------------------|-----------------|
| Dashboard          | `/`                | All roles       |
| Inventory          | `/inventory`       | Owner, Manager  |
| Point of Sale      | `/sales`           | All roles       |
| Reports            | `/reports`         | Owner, Manager  |
| Returns            | `/returns`         | Owner, Manager  |
| Employees          | `/employees`       | Owner           |
| Stores             | `/stores`          | Owner           |
| Settings           | `/settings`        | All roles       |
| Audit Log          | `/audit-log`       | Owner, Manager  |
| Support Messages   | `/support`         | All roles       |
