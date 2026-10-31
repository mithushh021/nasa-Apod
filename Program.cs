
using NasaApodGallery.Data; // Imports our ADO.NET repository
using NasaApodGallery.Services; // Imports our NASA API service

// Creates the web application builder
var builder = WebApplication.CreateBuilder(args);

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

// Redirects HTTP requests to HTTPS when configured
app.UseHttpsRedirection();


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
