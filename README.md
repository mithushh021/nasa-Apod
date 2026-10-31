# NASA APOD Gallery - ASP.NET Core MVC

A full-stack ASP.NET Core MVC application that consumes the official NASA Astronomy Picture of the Day (APOD) REST API, stores metadata and imagery links in a database using ADO.NET (`Microsoft.Data.SqlClient`), and renders a responsive, space-themed gallery.

Developed for **Technical Assignment 02**.

---

## 🚀 Key Features

- **Official NASA APOD Integration**: Consumes `https://api.nasa.gov/planetary/apod` using strongly typed DTOs and `System.Text.Json`.
- **Date Range Query**: Allows users to select any custom date range (from NASA APOD's launch on June 16, 1995 to the current day) and fetch records on demand.
- **Direct ADO.NET Implementation**: Uses `SqlConnection`, `SqlCommand`, and `SqlParameter` with parameterized queries to prevent SQL injection.
- **Duplicate Prevention**: Idempotent insert query (`WHERE NOT EXISTS (SELECT 1 FROM Apod WHERE [Date] = @Date)`) ensures no duplicate records are stored.
- **Cross-Platform Resilience**: Configured primarily for Microsoft SQL Server / SQL Express, with automatic fallback to embedded SQLite on non-Windows/Mac environments.
- **Responsive Gallery View**:
  - Image cards with titles, dates, and media badges (Image / Video).
  - High-resolution modal viewer with full NASA explanation, copyright, and direct links.
  - Live client-side search/filter by title or keywords.
  - Quick date presets (Last 7 Days, Last 14 Days, Last 30 Days).

---

## 🛠️ Prerequisites & Requirements

- **.NET SDK**: .NET 10.0 (or .NET 8.0/9.0+)
- **IDE**: Visual Studio 2022 / VS Code / JetBrains Rider
- **Database**: Microsoft SQL Server / SQL Server Express / LocalDB (or SQLite fallback)
- **NASA API Key**: NASA Open API key (defaults to `DEMO_KEY`)

---

## ⚙️ Configuration: Setting Your NASA API Key

The application reads the NASA API Key and Base URL from `appsettings.json`.

1. Get a free API key at [api.nasa.gov](https://api.nasa.gov/).
2. Open `appsettings.json` and update the `NasaApi` section:

```json
{
  "NasaApi": {
    "BaseUrl": "https://api.nasa.gov/planetary/apod",
    "ApiKey": "YOUR_PERSONAL_NASA_API_KEY_HERE"
  }
}
```

> **Note**: If left as `"DEMO_KEY"`, the application will use NASA's public demo credentials (rate-limited to 30 requests/hour and 50 requests/day). A graceful fallback is built in to ensure continuous operation.

---

## 🗄️ Database Setup (SQL Server)

1. Open **SQL Server Management Studio (SSMS)** or **Azure Data Studio**.
2. Connect to your SQL Server instance (e.g. `localhost`, `.\SQLEXPRESS`, or `(localdb)\MSSQLLocalDB`).
3. Open and execute the provided database script:
   ```
   Database/CreateDatabase.sql
   ```
4. Update the connection string in `appsettings.json` if needed:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost;Database=NasaApodDB;Trusted_Connection=True;TrustServerCertificate=True;"
   }
   ```

### Database Schema

```sql
CREATE TABLE Apod
(
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    [Date]         DATE NOT NULL UNIQUE,
    Title          NVARCHAR(300) NOT NULL,
    Explanation    NVARCHAR(MAX) NULL,
    [Url]          NVARCHAR(2000) NOT NULL,
    Hdurl          NVARCHAR(2000) NULL,
    MediaType      NVARCHAR(50) NOT NULL,
    ServiceVersion NVARCHAR(20) NULL,
    Copyright      NVARCHAR(200) NULL,
    SavedAt        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE NONCLUSTERED INDEX IX_Apod_Date ON Apod([Date] DESC);
```

---

## ▶️ Running the Project

### Option A: Using .NET CLI (Terminal / Command Prompt)

1. Navigate to the project root directory:
   ```bash
   cd NASA-APOD-Gallery-main
   ```

2. Restore NuGet dependencies and build:
   ```bash
   dotnet build
   ```

3. Run the application:
   ```bash
   dotnet run --launch-profile http
   ```

4. Open your browser and navigate to:
   ```
   http://localhost:5182
   ```

### Option B: Using Visual Studio

1. Double-click `NasaApodGallery.csproj` or open the solution in **Visual Studio**.
2. Select the `http` or `https` launch profile.
3. Press **F5** or click **Start Debugging**.

---

## 📂 Project Architecture

```
NASA-APOD-Gallery-main/
├── Controllers/
│   └── HomeController.cs          # Gallery display, FetchRange action, Sync action
├── DTOs/
│   └── ApodDto.cs                 # NASA API JSON response deserialization model
├── Models/
│   ├── Apod.cs                    # Database entity model
│   └── ErrorViewModel.cs          # Error handling model
├── Services/
│   └── NasaApiService.cs          # Consumes NASA APOD REST API endpoint
├── Data/
│   └── ApodRepository.cs          # ADO.NET CRUD operations (SqlConnection, SqlCommand)
├── Views/
│   ├── Home/
│   │   ├── Index.cshtml           # Responsive space gallery with date picker & search
│   │   └── Privacy.cshtml
│   └── Shared/
│       ├── _Layout.cshtml         # Space theme shell and navigation
│       └── Error.cshtml
├── Database/
│   └── CreateDatabase.sql         # SQL Server DDL script
├── wwwroot/
│   ├── css/gallery.css            # Custom space-themed styling and responsive layouts
│   └── js/site.js
├── appsettings.json               # Database connections and NASA API settings
└── Program.cs                     # Dependency injection and HTTP pipeline
```

---

## 📝 Assignment Requirements Verification Checklist

| Requirement | Implementation Details | Status |
|---|---|:---:|
| **Clean MVC Structure** | Controllers, Models, Views, Services, Data | Completed |
| **Consume NASA APOD API** | `https://api.nasa.gov/planetary/apod` with start/end dates | Completed |
| **API Key Configuration** | Configurable in `appsettings.json` (`NasaApi:ApiKey`) | Completed |
| **Date Range Query** | User UI with start/end date picker and presets | Completed |
| **DTO Deserialization** | `ApodDto` matching NASA JSON response fields | Completed |
| **SQL Server Database** | `Database/CreateDatabase.sql` with table, types, and index | Completed |
| **ADO.NET (`SqlClient`)** | `SqlConnection`, `SqlCommand`, `SqlParameter`, parameterized SQL | Completed |
| **Avoid Duplicates** | `WHERE NOT EXISTS (SELECT 1 FROM Apod WHERE [Date] = @Date)` | Completed |
| **Responsive Gallery** | Cards displaying image, title, date, excerpt, modal detail | Completed |
| **Documentation** | Full run guide, setup steps, and API key instructions | Completed |

---

## 📄 License
This project was developed for educational and technical assignment evaluation. Images and descriptions are courtesy of NASA Astronomy Picture of the Day.
