
using Microsoft.AspNetCore.Mvc;
using NasaApodGallery.Data;
using NasaApodGallery.DTOs;
using NasaApodGallery.Models;
using NasaApodGallery.Services;

namespace NasaApodGallery.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApodRepository _repository;
        private readonly NasaApiService _nasaApiService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            ApodRepository repository,
            NasaApiService nasaApiService,
            ILogger<HomeController> logger)
        {
            _repository = repository;
            _nasaApiService = nasaApiService;
            _logger = logger;
        }

        // Displays NASA pictures: either filtered for a requested date / range, or all saved pictures
        public async Task<IActionResult> Index(string? startDate = null, string? endDate = null, bool all = false)
        {
            List<Apod> pictures;

            // If a specific date or date range is requested by the user
            if (!all && !string.IsNullOrWhiteSpace(startDate))
            {
                if (DateTime.TryParse(startDate, out DateTime startDt))
                {
                    DateTime endDt = startDt;
                    if (!string.IsNullOrWhiteSpace(endDate) && DateTime.TryParse(endDate, out DateTime parsedEnd))
                    {
                        endDt = parsedEnd;
                    }

                    if (startDt > endDt)
                    {
                        (startDt, endDt) = (endDt, startDt);
                    }

                    // Query database specifically for this requested date or date range
                    pictures = await _repository.GetApodsByDateRangeAsync(startDt, endDt);

                    // If not all pictures for this date range are saved in database yet, fetch on-demand and store them
                    int expectedCount = (int)(endDt - startDt).TotalDays + 1;
                    if (pictures.Count < expectedCount)
                    {
                        try
                        {
                            var remote = await _nasaApiService.GetApodAsync(
                                startDt.ToString("yyyy-MM-dd"),
                                endDt.ToString("yyyy-MM-dd"));

                            if (remote.Count > 0)
                            {
                                await _repository.SaveApodsBatchAsync(remote);
                                pictures = await _repository.GetApodsByDateRangeAsync(startDt, endDt);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to fetch on-demand for range {Start} to {End}", startDate, endDate);
                        }
                    }

                    ViewBag.StartDate = startDt.ToString("yyyy-MM-dd");
                    ViewBag.EndDate = endDt.ToString("yyyy-MM-dd");
                    ViewBag.IsFiltered = true;
                    ViewBag.DateLabel = startDt == endDt ? startDt.ToString("dd MMM yyyy") : $"{startDt:dd MMM yyyy} → {endDt:dd MMM yyyy}";
                    return View(pictures);
                }
            }

            // Default or "All Stored": retrieve all saved pictures
            pictures = await _repository.GetAllApodsAsync();

            // If completely empty initially, fetch last 9 days
            if (pictures.Count == 0)
            {
                try
                {
                    string s = DateTime.UtcNow.AddDays(-8).ToString("yyyy-MM-dd");
                    string e = DateTime.UtcNow.ToString("yyyy-MM-dd");
                    var remote = await _nasaApiService.GetApodAsync(s, e);
                    await _repository.SaveApodsBatchAsync(remote);
                    pictures = await _repository.GetAllApodsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to automatically fetch initial NASA APOD pictures.");
                }
            }

            ViewBag.IsFiltered = false;
            ViewBag.StartDate = null;
            ViewBag.EndDate = null;
            return View(pictures);
        }

        // Fetches APOD data for a specific date or date range requested by the user
        [HttpPost]
        public async Task<IActionResult> FetchRange(string? dateInput, string? startDate, string? endDate)
        {
            // 1. Explicit "Today" keyword
            if (!string.IsNullOrWhiteSpace(dateInput) && dateInput.Trim().Equals("Today", StringComparison.OrdinalIgnoreCase))
            {
                startDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                endDate = startDate;
            }
            // 2. If hidden inputs startDate and endDate are already populated, keep them!
            // Only fall back to parsing dateInput if startDate or endDate is missing:
            else if (string.IsNullOrWhiteSpace(startDate) || string.IsNullOrWhiteSpace(endDate))
            {
                if (!string.IsNullOrWhiteSpace(dateInput))
                {
                    if (dateInput.Contains(" to ", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = dateInput.Split(new[] { " to " }, StringSplitOptions.RemoveEmptyEntries);
                        startDate = parts[0].Trim();
                        endDate = parts.Length > 1 ? parts[1].Trim() : parts[0].Trim();
                    }
                    else if (dateInput.Contains(" → "))
                    {
                        var parts = dateInput.Split(new[] { " → " }, StringSplitOptions.RemoveEmptyEntries);
                        startDate = parts[0].Trim();
                        endDate = parts.Length > 1 ? parts[1].Trim() : parts[0].Trim();
                    }
                    else
                    {
                        startDate = dateInput.Trim();
                        endDate = dateInput.Trim();
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(startDate))
            {
                TempData["ErrorMessage"] = "Please select a date or date range.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(endDate))
            {
                endDate = startDate;
            }

            if (!DateTime.TryParse(startDate, out DateTime startDt) || !DateTime.TryParse(endDate, out DateTime endDt))
            {
                TempData["ErrorMessage"] = "Invalid date format. Please select a valid date.";
                return RedirectToAction(nameof(Index));
            }

            DateTime earliestNasaDate = new DateTime(1995, 6, 16);
            if (startDt < earliestNasaDate) startDt = earliestNasaDate;
            if (endDt > DateTime.UtcNow.Date) endDt = DateTime.UtcNow.Date;
            if (startDt > endDt) (startDt, endDt) = (endDt, startDt);

            // Cap date range to at most 100 days per request to prevent API overload
            if ((endDt - startDt).TotalDays > 100)
            {
                startDt = endDt.AddDays(-100);
                TempData["WarningMessage"] = "Requested range was capped to 100 days.";
            }

            string sStr = startDt.ToString("yyyy-MM-dd");
            string eStr = endDt.ToString("yyyy-MM-dd");

            try
            {
                // Check what we already have stored in our database
                var existingInDb = await _repository.GetApodsByDateRangeAsync(startDt, endDt);
                int expectedCount = (int)(endDt - startDt).TotalDays + 1;

                // If some or all pictures for this range are not in database yet, fetch them from NASA API
                if (existingInDb.Count < expectedCount)
                {
                    List<ApodDto> remotePictures = await _nasaApiService.GetApodAsync(sStr, eStr);

                    if (remotePictures.Count > 0)
                    {
                        int newlySaved = await _repository.SaveApodsBatchAsync(remotePictures);
                        string label = sStr == eStr ? sStr : $"{sStr} → {eStr}";
                        TempData["SuccessMessage"] = $"Retrieved {label}: {remotePictures.Count} picture(s) loaded ({newlySaved} new saved).";
                    }
                }
                else
                {
                    string label = sStr == eStr ? sStr : $"{sStr} → {eStr}";
                    TempData["SuccessMessage"] = $"Loaded {existingInDb.Count} picture(s) for {label}.";
                }

                // Redirect to Index specifically displaying the fetched date / range!
                return RedirectToAction(nameof(Index), new { startDate = sStr, endDate = eStr });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching APOD data for range {Start} to {End}", sStr, eStr);

                // Even if remote API times out, check if we have any pictures in database to display
                var cached = await _repository.GetApodsByDateRangeAsync(startDt, endDt);
                if (cached.Count > 0)
                {
                    TempData["WarningMessage"] = $"Displayed {cached.Count} stored picture(s). NASA API connection timed out.";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Failed to fetch data from NASA API: {ex.Message}";
                }

                return RedirectToAction(nameof(Index), new { startDate = sStr, endDate = eStr });
            }
        }

        // Quick action to fetch the last 14 days of NASA APOD
        [HttpPost]
        public async Task<IActionResult> SyncLatest()
        {
            try
            {
                string startDate = DateTime.UtcNow.AddDays(-14).ToString("yyyy-MM-dd");
                string endDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

                List<ApodDto> remotePictures = await _nasaApiService.GetApodAsync(startDate, endDate);
                int newlySaved = await _repository.SaveApodsBatchAsync(remotePictures);
                TempData["SuccessMessage"] = $"Synced latest NASA pictures: {newlySaved} new added.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync latest NASA APOD pictures.");
                TempData["ErrorMessage"] = "Failed to sync latest NASA pictures.";
            }

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
        }
    }
}
