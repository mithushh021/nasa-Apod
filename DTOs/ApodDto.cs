
using System.Text.Json.Serialization; // Maps JSON fields to C# properties

namespace NasaApodGallery.DTOs
{
    // Represents one NASA APOD response record
    public class ApodDto
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = "";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("explanation")]
        public string Explanation { get; set; } = "";

        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        // Actual astronomy image URL from the updated NASA API
        [JsonPropertyName("hdurl")]
        public string Hdurl { get; set; } = "";

        [JsonPropertyName("media_type")]
        public string MediaType { get; set; } = "";

        [JsonPropertyName("service_version")]
        public string ServiceVersion { get; set; } = "";

        [JsonPropertyName("copyright")]
        public string? Copyright { get; set; }
    }
}

