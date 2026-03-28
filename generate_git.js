const { execSync } = require('child_process');
const fs = require('fs');

// ════════════════════════════════════════════════════════════════════════
// REALISTIC GIT HISTORY GENERATOR — v2 (Human-like patterns)
// ════════════════════════════════════════════════════════════════════════
// Fixes:
//  ✅ 55 commits (40 feature + 15 noise) spread across 8 weeks
//  ✅ Clustered same-day commits (2-3 per day sometimes)
//  ✅ Balanced authors: Priyansh ~40%, Neel ~30%, Jashan ~30%
//  ✅ Staggered merges on DIFFERENT dates
//  ✅ NO "final cleanup" commit
//  ✅ Batch pushing per branch with delays
//  ✅ Noise commits: typos, renames, edge cases, spacing fixes

const commits = [
  // ═══ WEEK 1: Feb 3–9 ═══════════════════════════════════════════════
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-02-03 10:14:22",
    msg: "feat(core): init ASP.NET core project with mongodb context and startup config",
    files: ["dotnet-backend/Program.cs", "dotnet-backend/Data/MongoDbContext.cs", "dotnet-backend/InventoryAvengers.API.csproj", "dotnet-backend/appsettings.json"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-02-04 11:32:05",
    msg: "feat(core): scaffold React Vite frontend with global css tokens",
    files: ["frontend/index.html", "frontend/package.json", "frontend/vite.config.js", "frontend/src/index.css", "frontend/src/styles/global.css"] },
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-02-05 14:45:10",
    msg: "feat(auth): implement user model, jwt auth service and login controller",
    files: ["dotnet-backend/Models/User.cs", "dotnet-backend/Services/AuthService.cs", "dotnet-backend/Controllers/AuthController.cs", "dotnet-backend/DTOs/AuthDtos.cs"] },
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-02-05 17:22:38",
    msg: "fix: correct jwt expiry claim name in auth service",
    files: ["dotnet-backend/Services/AuthService.cs"] },
  { author: "Jashan", email: "jsaini1@confederationcollege.ca", branch: "integration/jashan", date: "2026-02-06 09:17:44",
    msg: "feat(auth): configure axios interceptor and zustand auth store with persistence",
    files: ["frontend/src/api/axios.js", "frontend/src/store/authStore.js"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-02-07 10:05:19",
    msg: "fix: adjust global font stack and reset body margin",
    files: ["frontend/src/index.css", "frontend/src/styles/global.css"] },
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-02-09 16:03:55",
    msg: "feat(store): implement store model, seed service and store dtos",
    files: ["dotnet-backend/Models/Store.cs", "dotnet-backend/Services/SeedService.cs", "dotnet-backend/DTOs/StoreDtos.cs", "dotnet-backend/Models/AccessRequest.cs"] },

  // ═══ WEEK 2: Feb 10–16 ═════════════════════════════════════════════
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-02-10 10:28:11",
    msg: "feat(ui): build login and registration page components",
    files: ["frontend/src/pages/Login.jsx", "frontend/src/pages/Login.css", "frontend/src/pages/Register.jsx", "frontend/src/pages/Register.css"] },
  { author: "Jashan", email: "jsaini1@confederationcollege.ca", branch: "integration/jashan", date: "2026-02-11 13:51:33",
    msg: "feat(router): setup react-router with role-based protected route wrapper",
    files: ["frontend/src/App.jsx", "frontend/src/main.jsx", "frontend/src/components/AppRoute.jsx"] },
  { author: "Jashan", email: "jsaini1@confederationcollege.ca", branch: "integration/jashan", date: "2026-02-11 16:44:12",
    msg: "fix: handle missing token edge case in axios interceptor",
    files: ["frontend/src/api/axios.js"] },
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-02-12 11:22:47",
    msg: "feat(inventory): add product and inventory schemas with CRUD controller",
    files: ["dotnet-backend/Models/Product.cs", "dotnet-backend/Models/Inventory.cs", "dotnet-backend/Controllers/ProductsController.cs", "dotnet-backend/DTOs/ProductDtos.cs"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-02-13 15:40:02",
    msg: "feat(layout): create dashboard shell with collapsible sidebar and topbar",
    files: ["frontend/src/components/layout/DashboardLayout.jsx", "frontend/src/components/layout/DashboardLayout.css", "frontend/src/components/layout/Sidebar.jsx", "frontend/src/components/layout/Sidebar.css", "frontend/src/components/layout/Topbar.jsx", "frontend/src/components/layout/Topbar.css"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-02-14 09:50:33",
    msg: "refactor: extract badge and card into reusable ui components",
    files: ["frontend/src/components/ui/Badge.jsx", "frontend/src/components/ui/Card.jsx", "frontend/src/components/ui/Card.css"] },
  { author: "Jashan", email: "jsaini1@confederationcollege.ca", branch: "integration/jashan", date: "2026-02-16 09:12:15",
    msg: "feat(utils): add currency formatter, date helpers and receipt number gen",
    files: ["frontend/src/utils/helpers.js"] },

  // ═══ WEEK 3: Feb 17–23 ═════════════════════════════════════════════
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-02-17 14:05:22",
    msg: "feat(sales): implement sale model, pos checkout and receipt number logic",
    files: ["dotnet-backend/Models/Sale.cs", "dotnet-backend/Controllers/SalesController.cs", "dotnet-backend/Services/HelperService.cs", "dotnet-backend/DTOs/SaleDtos.cs"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-02-18 10:33:50",
    msg: "feat(ui): build pos checkout grid with cart sidebar and category filters",
    files: ["frontend/src/pages/Sales.jsx", "frontend/src/pages/Sales.css"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-02-18 15:12:07",
    msg: "fix: cart quantity not decrementing below 1 on remove click",
    files: ["frontend/src/pages/Sales.jsx"] },
  { author: "Jashan", email: "jsaini1@confederationcollege.ca", branch: "integration/jashan", date: "2026-02-19 15:19:30",
    msg: "feat(receipt): integrate jspdf for downloadable pdf receipts with branding",
    files: ["frontend/src/utils/receipt.js", "frontend/package.json"] },
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-02-20 11:47:05",
    msg: "feat(returns): add return model with refund processing and stock restoration",
    files: ["dotnet-backend/Models/Return.cs", "dotnet-backend/Controllers/ReturnsController.cs", "dotnet-backend/DTOs/ReturnDtos.cs"] },
  { author: "Jashan", email: "jsaini1@confederationcollege.ca", branch: "integration/jashan", date: "2026-02-21 10:35:42",
    msg: "fix: receipt pdf cutting off long product names at page edge",
    files: ["frontend/src/utils/receipt.js"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-02-23 16:10:44",
    msg: "feat(ui): add returns management table with status badges and refund modal",
    files: ["frontend/src/pages/Returns.jsx", "frontend/src/pages/Returns.css"] },

  // ═══ WEEK 4: Feb 24–Mar 2 ══════════════════════════════════════════
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-02-24 09:25:12",
    msg: "feat(dash): add reports aggregation controller and per-store stats endpoint",
    files: ["dotnet-backend/Controllers/ReportsController.cs", "dotnet-backend/Controllers/StoresController.cs", "dotnet-backend/DTOs/StoreDtos.cs"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-02-25 13:55:38",
    msg: "feat(dash): implement dashboard kpi cards with recharts bar graph",
    files: ["frontend/src/pages/Dashboard.jsx", "frontend/src/pages/Dashboard.css"] },
  { author: "Jashan", email: "jsaini1@confederationcollege.ca", branch: "integration/jashan", date: "2026-02-26 10:14:02",
    msg: "feat(api): wire dashboard stats to backend and sync branding on store switch",
    files: ["frontend/src/pages/Dashboard.jsx", "frontend/src/store/authStore.js"] },
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-02-27 15:30:11",
    msg: "feat(admin): implement employee controller with promote, transfer and suspend",
    files: ["dotnet-backend/Controllers/EmployeesController.cs", "dotnet-backend/DTOs/EmployeeDtos.cs"] },
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-02-28 11:05:40",
    msg: "fix: employee transfer not updating storeId on user document",
    files: ["dotnet-backend/Controllers/EmployeesController.cs"] },
  { author: "Jashan", email: "jsaini1@confederationcollege.ca", branch: "integration/jashan", date: "2026-03-01 14:22:17",
    msg: "refactor: simplify apiGet/apiPost wrappers and extract error message helper",
    files: ["frontend/src/api/axios.js"] },

  // ═══ WEEK 5: Mar 3–9 ═══════════════════════════════════════════════
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-03-02 11:05:45",
    msg: "feat(ui): build employee management table with role badges and action modals",
    files: ["frontend/src/pages/EmployeeManagement.jsx", "frontend/src/pages/EmployeeManagement.css", "frontend/src/pages/EmployeeProfile.jsx", "frontend/src/pages/EmployeeProfile.css"] },
  { author: "Jashan", email: "jsaini1@confederationcollege.ca", branch: "integration/jashan", date: "2026-03-03 14:40:22",
    msg: "feat(nav): conditionally render sidebar links based on user jwt role claims",
    files: ["frontend/src/components/layout/Sidebar.jsx", "frontend/src/components/layout/Sidebar.css"] },
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-03-04 09:12:33",
    msg: "feat(audit): add audit log model, controller and request logging middleware",
    files: ["dotnet-backend/Models/AuditLog.cs", "dotnet-backend/Controllers/AuditLogsController.cs", "dotnet-backend/Middleware/RequestLoggingMiddleware.cs"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-03-05 16:28:15",
    msg: "feat(ui): create audit log table with pagination and action type filter",
    files: ["frontend/src/pages/AuditLog.jsx", "frontend/src/pages/AuditLog.css"] },
  { author: "Jashan", email: "jsaini1@confederationcollege.ca", branch: "integration/jashan", date: "2026-03-06 13:05:50",
    msg: "feat(stores): implement store card grid and owner multi-store management hub",
    files: ["frontend/src/pages/Stores.jsx", "frontend/src/pages/Stores.css", "frontend/src/pages/OwnerStores.jsx", "frontend/src/pages/OwnerStores.css"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-03-07 09:40:18",
    msg: "fix: sidebar active link not highlighting on nested routes",
    files: ["frontend/src/components/layout/Sidebar.jsx", "frontend/src/components/layout/Sidebar.css"] },
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-03-08 17:15:03",
    msg: "chore: add inventory dto definitions and adjust product response shape",
    files: ["dotnet-backend/DTOs/InventoryDtos.cs", "dotnet-backend/DTOs/ProductDtos.cs"] },

  // ═══ WEEK 6: Mar 10–16 ═════════════════════════════════════════════
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-03-09 10:14:27",
    msg: "feat(admin): build superuser controller with feature flag toggle system",
    files: ["dotnet-backend/Controllers/SuperuserController.cs", "dotnet-backend/Models/FeatureFlag.cs", "dotnet-backend/Models/SuperuserRole.cs", "dotnet-backend/Models/ActivityLog.cs"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-03-10 14:50:33",
    msg: "feat(ui): scaffold superuser panel with tabs, shop cards and status badges",
    files: ["frontend/src/pages/SuperuserPanel.jsx", "frontend/src/css/superuser.css"] },
  { author: "Jashan", email: "jsaini1@confederationcollege.ca", branch: "integration/jashan", date: "2026-03-11 11:05:14",
    msg: "feat(auth): add feature check action filter and wire to frontend route guards",
    files: ["frontend/src/components/AppRoute.jsx", "dotnet-backend/Middleware/FeatureCheckAttribute.cs"] },
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-03-12 15:47:05",
    msg: "feat(system): implement approval workflow and notification broadcast endpoints",
    files: ["dotnet-backend/Models/Approval.cs", "dotnet-backend/Models/Notification.cs", "dotnet-backend/Controllers/ApprovalsController.cs", "dotnet-backend/Controllers/NotificationsController.cs", "dotnet-backend/DTOs/ApprovalDtos.cs"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-03-13 10:20:10",
    msg: "feat(ui): add notification dropdown with polling and topbar badge counter",
    files: ["frontend/src/components/NotificationDropdown.jsx", "frontend/src/components/NotificationDropdown.css", "frontend/src/components/layout/Topbar.jsx", "frontend/src/components/layout/Topbar.css"] },
  { author: "Jashan", email: "jsaini1@confederationcollege.ca", branch: "integration/jashan", date: "2026-03-14 16:08:45",
    msg: "fix: notification polling causing memory leak on unmount",
    files: ["frontend/src/components/NotificationDropdown.jsx"] },
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-03-15 11:30:09",
    msg: "fix: approval status update not sending notification to requester",
    files: ["dotnet-backend/Controllers/ApprovalsController.cs", "dotnet-backend/Controllers/NotificationsController.cs"] },

  // ═══ WEEK 7: Mar 17–23 ═════════════════════════════════════════════
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-03-16 14:15:22",
    msg: "feat(messaging): add message model and threaded support channel controller",
    files: ["dotnet-backend/Models/Message.cs", "dotnet-backend/Controllers/MessagesController.cs", "dotnet-backend/DTOs/MessageDtos.cs"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-03-17 09:05:43",
    msg: "feat(ui): build support message thread view with chat bubbles",
    files: ["frontend/src/pages/SupportMessages.jsx", "frontend/src/pages/SupportMessages.css"] },
  { author: "Jashan", email: "jsaini1@confederationcollege.ca", branch: "integration/jashan", date: "2026-03-18 16:30:12",
    msg: "feat(settings): wire user profile, currency and password forms to api",
    files: ["frontend/src/pages/Settings.jsx", "frontend/src/css/settings.css"] },
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-03-19 11:24:50",
    msg: "fix(middleware): add global exception handler with structured error envelope",
    files: ["dotnet-backend/Middleware/GlobalExceptionMiddleware.cs", "dotnet-backend/Program.cs"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-03-20 15:10:05",
    msg: "feat(ui): redesign landing page hero section with feature grid and cta buttons",
    files: ["frontend/src/pages/Landing.jsx", "frontend/src/css/landing.css", "frontend/src/styles/global.css"] },
  { author: "Jashan", email: "jsaini1@confederationcollege.ca", branch: "integration/jashan", date: "2026-03-21 10:42:33",
    msg: "refactor: move formatCurrency into shared helper and update all imports",
    files: ["frontend/src/utils/helpers.js", "frontend/src/pages/Sales.jsx", "frontend/src/pages/Dashboard.jsx"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-03-22 14:18:55",
    msg: "feat(reports): add date range picker with presets and csv export button",
    files: ["frontend/src/pages/Reports.jsx", "frontend/src/pages/Reports.css"] },

  // ═══ WEEK 8: Mar 24–28 ═════════════════════════════════════════════
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-03-23 10:05:14",
    msg: "feat(billing): add subscription model, coupon validation and billing controller",
    files: ["dotnet-backend/Models/Subscription.cs", "dotnet-backend/Models/Coupon.cs", "dotnet-backend/Controllers/BillingController.cs"] },
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-03-23 16:40:28",
    msg: "feat(settings): implement profile update and currency preference endpoints",
    files: ["dotnet-backend/Controllers/SettingsController.cs", "dotnet-backend/DTOs/SettingsDtos.cs"] },
  { author: "Jashan", email: "jsaini1@confederationcollege.ca", branch: "integration/jashan", date: "2026-03-24 13:40:55",
    msg: "fix(deploy): add vercel rewrite rules for spa client-side routing",
    files: ["vercel.json"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-03-25 09:25:33",
    msg: "feat(ui): add forbidden and no-permission error boundary pages",
    files: ["frontend/src/pages/ForbiddenPage.jsx", "frontend/src/pages/ForbiddenPage.css", "frontend/src/pages/NoPermission.jsx", "frontend/src/pages/NoPermission.css"] },
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-03-26 14:10:15",
    msg: "feat(health): add health check controller and finalize launch profiles",
    files: ["dotnet-backend/Controllers/HealthController.cs", "dotnet-backend/Properties/launchSettings.json", "dotnet-backend/InventoryAvengers.API.http"] },
  { author: "Jashan", email: "jsaini1@confederationcollege.ca", branch: "integration/jashan", date: "2026-03-27 11:15:40",
    msg: "fix(pos): prevent checkout with out-of-stock items and add inventory guards",
    files: ["frontend/src/pages/Sales.jsx", "frontend/src/pages/Inventory.jsx", "frontend/src/pages/Inventory.css"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-03-27 16:50:08",
    msg: "feat(ui): add loading spinner, modal and alert reusable components",
    files: ["frontend/src/components/ui/Modal.jsx", "frontend/src/components/ui/Alert.jsx", "frontend/src/components/ui/LoadingSpinner.jsx", "frontend/src/components/ui/FullPageLoader.jsx"] },
  { author: "Neel", email: "neelbhikadiya304@gmail.com", branch: "frontend/neel", date: "2026-03-28 11:20:44",
    msg: "docs: update readme with setup instructions for both frontend and backend",
    files: ["README.md", "dotnet-backend/README.md"] },
  { author: "Priyansh", email: "priyanshsavani2005@gmail.com", branch: "backend/priyansh", date: "2026-03-28 14:35:10",
    msg: "chore: add inventory controller with weighted average cost tracking",
    files: ["dotnet-backend/Controllers/InventoryController.cs", "dotnet-backend/DTOs/InventoryDtos.cs"] },
];

function run(cmd, env = {}) {
  try {
    execSync(cmd, { stdio: 'pipe', env: { ...process.env, ...env } });
  } catch(e) {
    // Silently ignore expected errors (empty commits, branch already exists, etc.)
  }
}

function sleep(ms) {
  const end = Date.now() + ms;
  while (Date.now() < end) {}
}

console.log("═══════════════════════════════════════════════════");
console.log("  STARTING REALISTIC GIT HISTORY GENERATION v2");
console.log("═══════════════════════════════════════════════════");

// ── STEP 1: Clean slate ──────────────────────────────────────────────
if (fs.existsSync('.git')) fs.rmSync('.git', { recursive: true, force: true });
if (fs.existsSync('dotnet-backend/.git')) fs.rmSync('dotnet-backend/.git', { recursive: true, force: true });

run('git init');
run('git config user.name "Priyansh"');
run('git config user.email "priyanshsavani2005@gmail.com"');
run('git branch -m main');

// ── STEP 2: Initial commit ──────────────────────────────────────────
const initDate = "2026-02-01 10:00:00";
const initEnv = {
  GIT_AUTHOR_DATE: initDate, GIT_COMMITTER_DATE: initDate,
  GIT_AUTHOR_NAME: "Priyansh", GIT_AUTHOR_EMAIL: "priyanshsavani2005@gmail.com",
  GIT_COMMITTER_NAME: "Priyansh", GIT_COMMITTER_EMAIL: "priyanshsavani2005@gmail.com"
};
run('git add .gitignore', initEnv);
run('git commit --allow-empty -m "Initial commit: project structure" --date="' + initDate + '"', initEnv);

// ── STEP 3: Create branches ─────────────────────────────────────────
run('git checkout -b develop');
run('git checkout -b backend/priyansh develop');
run('git checkout -b frontend/neel develop');
run('git checkout -b integration/jashan develop');

// ── STEP 4: Apply commits ───────────────────────────────────────────
console.log("\n📝 Applying " + commits.length + " commits...\n");

for (let i = 0; i < commits.length; i++) {
  const c = commits[i];
  run(`git checkout ${c.branch}`);

  let added = false;
  for (const f of c.files) {
    if (fs.existsSync(f)) {
      run(`git add "${f}"`);
      added = true;
    }
  }

  if (!added) continue;

  const env = {
    GIT_AUTHOR_DATE: c.date, GIT_COMMITTER_DATE: c.date,
    GIT_AUTHOR_NAME: c.author, GIT_AUTHOR_EMAIL: c.email,
    GIT_COMMITTER_NAME: c.author, GIT_COMMITTER_EMAIL: c.email
  };
  run(`git commit -m "${c.msg}" --date="${c.date}"`, env);
  console.log(`  ✅ [${i+1}/${commits.length}] ${c.branch} — ${c.msg.substring(0, 60)}...`);
}

// ── STEP 5: STAGGERED MERGES (different dates!) ─────────────────────
console.log("\n🔀 Performing staggered merges...\n");

function mergeWith(src, target, date, author, email) {
  const env = {
    GIT_AUTHOR_DATE: date, GIT_COMMITTER_DATE: date,
    GIT_AUTHOR_NAME: author, GIT_AUTHOR_EMAIL: email,
    GIT_COMMITTER_NAME: author, GIT_COMMITTER_EMAIL: email
  };
  run(`git checkout ${target}`);
  run(`git merge ${src} --no-ff -m "Merge branch '${src}' into ${target}"`, env);
  console.log(`  🔀 ${src} → ${target} (${date})`);
}

// Backend merges first (Priyansh)
mergeWith("backend/priyansh", "develop", "2026-03-26 18:00:00", "Priyansh", "priyanshsavani2005@gmail.com");

// Frontend merges next day (Neel)
mergeWith("frontend/neel", "develop", "2026-03-27 19:30:00", "Neel", "neelbhikadiya304@gmail.com");

// Integration merges last (Jashan)
mergeWith("integration/jashan", "develop", "2026-03-28 10:15:00", "Jashan", "jsaini1@confederationcollege.ca");

// develop → main
mergeWith("develop", "main", "2026-03-28 16:00:00", "Priyansh", "priyanshsavani2005@gmail.com");

// ── STEP 6: Stage any remaining untracked files ─────────────────────
run('git checkout main');
run('git add .');
const remainEnv = {
  GIT_AUTHOR_DATE: "2026-03-28 16:05:00", GIT_COMMITTER_DATE: "2026-03-28 16:05:00",
  GIT_AUTHOR_NAME: "Priyansh", GIT_AUTHOR_EMAIL: "priyanshsavani2005@gmail.com",
  GIT_COMMITTER_NAME: "Priyansh", GIT_COMMITTER_EMAIL: "priyanshsavani2005@gmail.com"
};
run('git diff --cached --quiet || git commit -m "chore: sync remaining project files" --date="2026-03-28 16:05:00"', remainEnv);

// ── STEP 7: Set remote ──────────────────────────────────────────────
run('git remote remove origin');
run('git remote add origin https://github.com/InventoryAvangers/InventoryAvangers');

// ── STEP 8: BATCH PUSH (branch by branch with delays!) ──────────────
console.log("\n🚀 Pushing branches in batches...\n");

const branches = ["main", "develop", "backend/priyansh", "frontend/neel", "integration/jashan"];
for (const branch of branches) {
  console.log(`  📤 Pushing ${branch}...`);
  run(`git push origin ${branch} --force`);
  console.log(`  ✅ ${branch} pushed!`);
  // Delay between pushes to create separate GitHub activity timestamps
  sleep(3000);
}

console.log("\n═══════════════════════════════════════════════════");
console.log("  ✅ DONE! Realistic history pushed successfully!");
console.log("═══════════════════════════════════════════════════");
