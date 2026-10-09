using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoGnomeMod;

//Route Eco Gnome queries on this server's web server for the market's average prices (Eco loads controllers from mod DLLs).
//Eco's own auth is for players and admins; this route checks instead an HMAC signature made with the secret given to Eco
//Gnome when the server was linked. The secret never travels, which matters since Eco's web server speaks plain http.
[AllowAnonymous]
[Route("api/ecognome/v1")]
public class EcoGnomeMarketController : ControllerBase
{
    const string MarketPath = "/api/ecognome/v1/market";
    static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    [HttpGet("market")]
    public IActionResult Market([FromHeader(Name = "X-EcoGnome-Timestamp")] string? timestamp, [FromHeader(Name = "X-EcoGnome-Signature")] string? signature)
    {
        var config = EcoGnomePlugin.Obj.Config;
        if (!config.ShareMarketPrices || !EcoGnomeTokens.IsServerLinked) return this.NotFound();
        if (!IsSigned(timestamp, signature)) return this.Unauthorized();

        return this.Ok(EcoGnomeMarket.Compute(config.MarketCurrency, Math.Max(1, config.MarketWindowDays)) ?? new MarketSnapshot { WindowDays = config.MarketWindowDays });
    }

    static bool IsSigned(string? timestamp, string? signature)
    {
        if (!long.TryParse(timestamp, out var seconds) || signature is null) return false;
        if ((DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(seconds)).Duration() > MaxClockSkew) return false;

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(EcoGnomeTokens.MarketSecret), Encoding.UTF8.GetBytes($"GET\n{MarketPath}\n{timestamp}"));
        try { return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature)); }
        catch (FormatException) { return false; }
    }
}
