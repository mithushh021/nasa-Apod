
using System.Text.Json;
using NasaApodGallery.DTOs;

namespace NasaApodGallery.Services
{
    // Service responsible for consuming NASA APOD API
    public class NasaApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NasaApiService> _logger;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _fallbackUrl;

        public NasaApiService(HttpClient httpClient, IConfiguration configuration, ILogger<NasaApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _baseUrl = configuration["NasaApi:BaseUrl"] ?? "https://api.nasa.gov/planetary/apod";
            _apiKey = configuration["NasaApi:ApiKey"] ?? "DEMO_KEY";
            _fallbackUrl = configuration["NasaApi:FallbackUrl"] ?? "https://science.nasa.gov/wp-json/wp/v2/apod-basic";

            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; NASA-APOD-Gallery/1.0)");
            }

            _httpClient.Timeout = TimeSpan.FromSeconds(12);
        }

        // Fetches NASA APOD data for a given date range
        public async Task<List<ApodDto>> GetApodAsync(string startDate, string endDate)
        {
            // Normalize dates to YYYY-MM-DD
            DateTime startDt = DateTime.Parse(startDate);
            DateTime endDt = DateTime.Parse(endDate);

            // Ensure start <= end and not in future
            if (startDt > endDt)
            {
                (startDt, endDt) = (endDt, startDt);
            }

            if (endDt > DateTime.UtcNow.Date)
            {
                endDt = DateTime.UtcNow.Date;
            }

            string startStr = startDt.ToString("yyyy-MM-dd");
            string endStr = endDt.ToString("yyyy-MM-dd");

            List<ApodDto> results = new List<ApodDto>();
            bool hasPlaceholders = false;

            // 1. Primary: Official NASA APOD API endpoint (api.nasa.gov) per assignment spec
            string officialUrl = $"{_baseUrl}?api_key={_apiKey}&start_date={startStr}&end_date={endStr}&thumbs=true";
            
            try
            {
                _logger.LogInformation("Requesting NASA APOD from official API: {Url}", officialUrl);
                using var response = await _httpClient.GetAsync(officialUrl);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var pictures = JsonSerializer.Deserialize<List<ApodDto>>(json, options);

                    if (pictures != null && pictures.Count > 0)
                    {
                        foreach (var pic in pictures)
                        {
                            if (IsPlaceholder(pic))
                            {
                                hasPlaceholders = true;
                            }
                            else
                            {
                                NormalizeItem(pic);
                                results.Add(pic);
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("NASA official API returned status {StatusCode}. Falling back to content API.", response.StatusCode);
                    hasPlaceholders = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch from official NASA API. Falling back to alternative endpoint.");
                hasPlaceholders = true;
            }

            // 2. If official API was rate-limited or returned placeholder logos ("NASA Science"),
            // backfill with genuine data from science.nasa.gov
            if (hasPlaceholders || results.Count == 0)
            {
                try
                {
                    string from = startDt.ToString("yyMMdd");
                    string to = endDt.ToString("yyMMdd");
                    string fallbackUrl = $"{_fallbackUrl}?date_from={from}&date_to={to}";

                    _logger.LogInformation("Backfilling real APOD imagery from: {Url}", fallbackUrl);
                    string json = await _httpClient.GetStringAsync(fallbackUrl);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var fallbackPictures = JsonSerializer.Deserialize<List<ApodDto>>(json, options);

                    if (fallbackPictures != null)
                    {
                        foreach (var fb in fallbackPictures)
                        {
                            if (!IsPlaceholder(fb))
                            {
                                NormalizeItem(fb);

                                // Replace or add if missing from official API
                                int existingIndex = results.FindIndex(r => r.Date == fb.Date);
                                if (existingIndex >= 0)
                                {
                                    results[existingIndex] = fb;
                                }
                                else
                                {
                                    results.Add(fb);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to backfill APOD imagery from fallback endpoint.");
                }
            }

            // Return records ordered newest first
            return results.OrderByDescending(r => r.Date).ToList();
        }

        // Detects if a record is NASA's generic placeholder logo
        private static bool IsPlaceholder(ApodDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Url))
                return true;

            if (dto.Title.Equals("NASA Science", StringComparison.OrdinalIgnoreCase))
                return true;

            if (dto.Url.Contains("nasa-logo", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(dto.Hdurl) && dto.Hdurl.Contains("nasa-logo", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        // Ensures direct image URL is used for displaying cards
        private static void NormalizeItem(ApodDto dto)
        {
            // If Hdurl is a direct asset image, prefer it as Url so the <img> tag loads the picture
            if (!string.IsNullOrWhiteSpace(dto.Hdurl) &&
                !dto.Hdurl.Contains("nasa-logo", StringComparison.OrdinalIgnoreCase) &&
                (dto.Url.Contains("/image-article/") || dto.Hdurl.Contains("assets.science.nasa.gov")))
            {
                dto.Url = dto.Hdurl;
            }

            // Remove any raw HTML markup or footer artifacts from the description
            dto.Explanation = Helpers.TextHelper.CleanExplanation(dto.Explanation);
        }
    }
}
