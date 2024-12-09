using Newtonsoft.Json;

namespace EcoGnomeMod;

public static class EcoGnomeApi
{
    public static async Task<string> RegisterServerAsync(string joinCode, string ecoServerId)
    {
        try
        {
            using var httpClient = new HttpClient();
            var requestUrl = $"{EcoGnomePlugin.Obj.Config.EcoGnomeUrl}/api/eco/register-server?joinCode={Uri.EscapeDataString(joinCode)}&ecoServerId={Uri.EscapeDataString(ecoServerId)}";
            var response = await httpClient.GetAsync(requestUrl);

            return response.StatusCode switch
            {
                System.Net.HttpStatusCode.OK => "OK - Server successfully registered!",
                System.Net.HttpStatusCode.NotFound => "NotFound - Server not found.",
                System.Net.HttpStatusCode.BadRequest => "BadRequest - Server is already registered to an other eco server.",
                _ => $"Unexpected response: {response.StatusCode}"
            };
        }
        catch (HttpRequestException ex)
        {
            return $"HTTP request error: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Unexpected error: {ex.Message}";
        }
    }

    public static async Task<string> RegisterUserAsync(string ecoServerId, string userSecretId, string ecoUserId, string serverPseudo)
    {
        try
        {
            using var httpClient = new HttpClient();
            var requestUrl = $"{EcoGnomePlugin.Obj.Config.EcoGnomeUrl}/api/eco/register-user" +
                             $"?ecoServerId={Uri.EscapeDataString(ecoServerId)}" +
                             $"&userSecretId={Uri.EscapeDataString(userSecretId)}" +
                             $"&ecoUserId={Uri.EscapeDataString(ecoUserId)}" +
                             $"&serverPseudo={Uri.EscapeDataString(serverPseudo)}";
            var response = await httpClient.GetAsync(requestUrl);

            return response.StatusCode switch
            {
                System.Net.HttpStatusCode.OK => "OK - User registered successfully",
                System.Net.HttpStatusCode.NotFound => "NotFound - Make sure the server is registered by the admin, verify your userSecretId, and verify you joined the server on Eco Gnome.",
                System.Net.HttpStatusCode.BadRequest => "BadRequest - This EcoGnome user is already associated to an other eco user in this server.",
                _ => $"Unexpected response: {response.StatusCode}"
            };
        }
        catch (HttpRequestException ex)
        {
            return $"HTTP request error: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Unexpected error: {ex.Message}";
        }
    }

    public static async Task<List<EcoGnomePrice>> GetUserPricesAsync(string ecoServerId, string ecoUserId)
    {
        using var httpClient = new HttpClient();
        var requestUrl = $"{EcoGnomePlugin.Obj.Config.EcoGnomeUrl}/api/eco/user-prices" +
                         $"?ecoServerId={Uri.EscapeDataString(ecoServerId)}" +
                         $"&ecoUserId={Uri.EscapeDataString(ecoUserId)}";
        var response = await httpClient.GetAsync(requestUrl);

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            throw new EcoApiException("BadRequest");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var prices = JsonConvert.DeserializeObject<List<EcoGnomePrice>>(jsonResponse);
            return prices ?? [];
        }

        throw new EcoApiException($"Unexpected response: {response.StatusCode}");
    }
}

public class EcoGnomePrice(string name, decimal price)
{
    [JsonProperty(nameof(Name))]
    public string Name { get; set; } = name;

    [JsonProperty(nameof(Price))]
    public decimal Price { get; set; } = price;
}

public class EcoApiException(string message) : Exception(message);
