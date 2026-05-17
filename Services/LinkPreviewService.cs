using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lock.Services
{
    public class LinkPreviewData
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string SiteName { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string FaviconUrl { get; set; } = string.Empty;
        public bool IsLoaded { get; set; } = false;
        public bool HasImage => !string.IsNullOrEmpty(ImageUrl);
    }

    public static class LinkPreviewService
    {
        private static readonly ConcurrentDictionary<string, LinkPreviewData> _cache = new();
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        static LinkPreviewService()
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (compatible; LockApp/1.0)");
        }

        public static async Task<LinkPreviewData> FetchAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return new LinkPreviewData();

            // Normalize URL
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;

            // Return cache hit immediately
            if (_cache.TryGetValue(url, out var cached))
                return cached;

            var preview = new LinkPreviewData { Url = url };

            try
            {
                var uri = new Uri(url);
                preview.Domain = uri.Host.Replace("www.", "");
                preview.FaviconUrl = $"https://www.google.com/s2/favicons?sz=64&domain={uri.Host}";
                preview.SiteName = preview.Domain.ToUpperInvariant();

                var html = await _http.GetStringAsync(url);

                // OG Title → fallback to <title>
                preview.Title = GetMetaContent(html, "og:title")
                             ?? GetMetaContent(html, "twitter:title")
                             ?? GetHtmlTitle(html)
                             ?? preview.Domain;

                // OG Description → fallback to meta description
                preview.Description = GetMetaContent(html, "og:description")
                                   ?? GetMetaContent(html, "twitter:description")
                                   ?? GetMetaContent(html, "description")
                                   ?? string.Empty;

                // OG Image → fallback to twitter:image
                var rawImage = GetMetaContent(html, "og:image")
                            ?? GetMetaContent(html, "twitter:image");

                if (!string.IsNullOrEmpty(rawImage))
                {
                    // Make absolute if relative
                    if (rawImage.StartsWith("//"))
                        rawImage = "https:" + rawImage;
                    else if (rawImage.StartsWith("/"))
                        rawImage = $"{uri.Scheme}://{uri.Host}{rawImage}";

                    preview.ImageUrl = rawImage;
                }

                // OG Site name
                var ogSite = GetMetaContent(html, "og:site_name");
                if (!string.IsNullOrEmpty(ogSite))
                    preview.SiteName = ogSite;

                // Trim description
                if (preview.Description.Length > 120)
                    preview.Description = preview.Description.Substring(0, 120) + "…";

                preview.IsLoaded = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LinkPreviewService error for {url}: {ex.Message}");
                // Still return partial data (domain + favicon)
                preview.IsLoaded = true;
            }

            _cache[url] = preview;
            return preview;
        }

        private static string? GetMetaContent(string html, string property)
        {
            // Match og:xxx / twitter:xxx / name="description"
            var patterns = new[]
            {
                $@"<meta[^>]*property=[""']{Regex.Escape(property)}[""'][^>]*content=[""']([^""']*)[""']",
                $@"<meta[^>]*content=[""']([^""']*)[""'][^>]*property=[""']{Regex.Escape(property)}[""']",
                $@"<meta[^>]*name=[""']{Regex.Escape(property)}[""'][^>]*content=[""']([^""']*)[""']",
                $@"<meta[^>]*content=[""']([^""']*)[""'][^>]*name=[""']{Regex.Escape(property)}[""']",
            };

            foreach (var pattern in patterns)
            {
                var m = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (m.Success)
                {
                    var val = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value.Trim());
                    if (!string.IsNullOrEmpty(val)) return val;
                }
            }

            return null;
        }

        private static string? GetHtmlTitle(string html)
        {
            var m = Regex.Match(html, @"<title[^>]*>([^<]+)</title>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return m.Success
                ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value.Trim())
                : null;
        }

        public static void ClearCache() => _cache.Clear();
    }
}