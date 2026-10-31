using System.Net;
using System.Text.RegularExpressions;

namespace NasaApodGallery.Helpers
{
    public static class TextHelper
    {
        private static readonly Regex TomorrowRegex = new Regex(
            @"(<br\s*/?>\s*)*<strong>\s*Tomorrow'?s\s*picture:.*?$|Tomorrow'?s\s*picture:.*?$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex ExplanationPrefixRegex = new Regex(
            @"^(<strong>\s*)?Explanation:\s*(</strong>)?\s*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex HtmlTagRegex = new Regex(
            @"<[^>]+>",
            RegexOptions.Compiled);

        private static readonly Regex MultiSpaceRegex = new Regex(
            @"\s+",
            RegexOptions.Compiled);

        /// <summary>
        /// Cleans NASA APOD explanation text by removing embedded HTML tags,
        /// removing the 'Tomorrow's picture' trailer, removing 'Explanation:' header,
        /// and unescaping HTML entities.
        /// </summary>
        public static string CleanExplanation(string? rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return string.Empty;

            string text = rawText;

            // 1. Remove "Tomorrow's picture: ..." trailer
            text = TomorrowRegex.Replace(text, string.Empty);

            // 2. Remove leading "Explanation:" header
            text = ExplanationPrefixRegex.Replace(text, string.Empty);

            // 3. Strip all HTML tags
            text = HtmlTagRegex.Replace(text, string.Empty);

            // 4. Decode HTML entities
            text = WebUtility.HtmlDecode(text);

            // 5. Normalize whitespace and non-breaking spaces
            text = text.Replace('\u00A0', ' ');
            text = MultiSpaceRegex.Replace(text, " ").Trim();

            return text;
        }
    }
}

