using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.Models;

namespace InventoryAvengers.API.Middleware;

/// <summary>
/// Action filter that mirrors the Node.js featureCheck middleware.
/// Blocks the action if the store's FeatureFlag document has the feature disabled.
/// Superusers bypass this check.
/// Usage: [FeatureCheck("reports")]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class FeatureCheckAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _featureName;

    public FeatureCheckAttribute(string featureName)
    {
        _featureName = featureName;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var role = context.HttpContext.User.FindFirst("role")?.Value;

        // Superuser always has full access
        if (role == "superuser")
        {
            await next();
            return;
        }

        var storeId = context.HttpContext.User.FindFirst("storeId")?.Value;
        if (string.IsNullOrWhiteSpace(storeId))
        {
            context.Result = new ObjectResult(new
            {
                success = false,
                message = "No store associated with this account"
            })
            { StatusCode = 403 };
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<MongoDbContext>();
        var flags = await db.FeatureFlags.Find(f => f.StoreId == storeId).FirstOrDefaultAsync();

        if (flags == null)
        {
            var store = await db.Stores.Find(s => s.Id == storeId).FirstOrDefaultAsync();
            var subscription = await db.Subscriptions.Find(s => s.StoreId == storeId).FirstOrDefaultAsync();
            var plan = subscription?.Plan ?? store?.Plan ?? "free";

            // Auto-provision feature flags using the store's effective plan defaults.
            flags = new FeatureFlag
            {
                StoreId  = storeId,
                Features = FeatureFlag.GetDefaults(plan)
            };
            try
            {
                await db.FeatureFlags.InsertOneAsync(flags);
            }
            catch
            {
                flags = await db.FeatureFlags.Find(f => f.StoreId == storeId).FirstOrDefaultAsync() ?? flags;
            }
        }

        var featureEnabled = _featureName switch
        {
            "inventory"  => flags.Features?.Inventory ?? false,
            "pos"        => flags.Features?.Pos ?? false,
            "returns"    => flags.Features?.Returns ?? false,
            "reports"    => flags.Features?.Reports ?? false,
            "pdfExport"  => flags.Features?.PdfExport ?? false,
            "employees"  => flags.Features?.Employees ?? false,
            "payments"   => flags.Features?.Payments ?? false,
            "apiAccess"  => flags.Features?.ApiAccess ?? false,
            "darkMode"   => flags.Features?.DarkMode ?? false,
            _            => false
        };


        if (!featureEnabled)
        {
            context.Result = new ObjectResult(new
            {
                success = false,
                message = $"Feature \"{_featureName}\" is not available on your current plan",
                feature = _featureName,
                upgrade = true
            })
            { StatusCode = 403 };
            return;
        }

        await next();
    }
}
