
using NasaApodGallery.Data; // Imports our ADO.NET repository
using NasaApodGallery.Services; // Imports our NASA API service

// Creates the web application builder
var builder = WebApplication.CreateBuilder(args);

// Dynamically bind to the PORT environment variable provided by Railway / cloud hosts
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Adds MVC controllers and views
builder.Services.AddControllersWithViews();

// Registers our NASA API service with HttpClient
builder.Services.AddHttpClient<NasaApiService>();

// Register our ADO.NET repository
builder.Services.AddScoped<ApodRepository>();

// Builds the web application
var app = builder.Build();

// Shows error pages outside development mode
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Redirects HTTP requests to HTTPS when running locally with HTTPS configured
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PORT")))
{
    app.UseHttpsRedirection();
}


// Enables routing
app.UseRouting();

// Enables authorization middleware
app.UseAuthorization();

// Serve static files
app.MapStaticAssets();

// Defines the default MVC route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Starts the application
app.Run();
