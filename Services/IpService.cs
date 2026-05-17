// Services/IpService.cs
using System.Net.Http;
using System.Text.Json;
using System.Diagnostics;

namespace Lock.Services
{
    public static class IpService
    {
        private const string ApiToken = "16ce2be99838bc";
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        // Full IP info result
        public class IpInfo
        {
            public string Ip { get; set; } = string.Empty;
            public string City { get; set; } = string.Empty;
            public string Region { get; set; } = string.Empty;
            public string Country { get; set; } = string.Empty;
            public string Location => string.Join(", ", new[] { City, Region, Country }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        /// <summary>
        /// Returns full IP info (ip, city, region, country).
        /// Never throws — returns empty IpInfo on failure.
        /// </summary>
        public static async Task<IpInfo> GetIpInfoAsync()
        {
            try
            {
                _http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiToken);

                // /lite/me = current device's IP info
                var json = await _http.GetStringAsync("https://api.ipinfo.io/lite/me");
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var info = new IpInfo
                {
                    Ip = root.TryGetProperty("ip", out var ip) ? ip.GetString() ?? "" : "",
                    City = root.TryGetProperty("city", out var city) ? city.GetString() ?? "" : "",
                    Region = root.TryGetProperty("region", out var region) ? region.GetString() ?? "" : "",
                    Country = root.TryGetProperty("country", out var country) ? country.GetString() ?? "" : "",
                };

                Debug.WriteLine($"[IPINFO] IP: {info.Ip} | {info.Location}");
                return info;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IPINFO] Failed (non-fatal): {ex.Message}");
                return new IpInfo(); // empty, non-fatal
            }
        }

        /// <summary>
        /// Convenience — returns just the raw IP string.
        /// </summary>
        public static async Task<string> GetPublicIpAsync()
        {
            var info = await GetIpInfoAsync();
            return info.Ip;
        }
    }
}