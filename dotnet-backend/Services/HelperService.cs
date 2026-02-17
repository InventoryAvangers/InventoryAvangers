namespace InventoryAvengers.API.Services;

public static class HelperService
{
    private static readonly char[] SkuChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();
    private static readonly Random Rng = Random.Shared;

    public static string GenerateSku()
    {
        var chars = new char[6];
        for (int i = 0; i < 6; i++)
            chars[i] = SkuChars[Rng.Next(SkuChars.Length)];
        return "SKU-" + new string(chars);
    }

    public static string GenerateReceiptNumber()
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var rand = Path.GetRandomFileName().Replace(".", "")[..4].ToUpper();
        return $"RCP-{date}-{rand}";
    }

    public static string GenerateStoreCode()
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var result = new System.Text.StringBuilder();
        while (timestamp > 0)
        {
            result.Insert(0, chars[(int)(timestamp % 36)]);
            timestamp /= 36;
        }
        return "STR-" + result.ToString();
    }
}
