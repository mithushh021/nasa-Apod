
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using NasaApodGallery.DTOs;
using NasaApodGallery.Models;

namespace NasaApodGallery.Data
{
    // Handles database operations using ADO.NET (SqlConnection, SqlCommand, SqlParameter)
    // with automatic SQLite fallback for cross-platform execution
    public class ApodRepository
    {
        private readonly string _connectionString;
        private readonly string _sqliteConnectionString;
        private bool _useSqlite;

        public ApodRepository(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";

            // Setup local SQLite database path for cross-platform development/demo
            string dbDir = Path.Combine(environment.ContentRootPath, "Database");
            if (!Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }
            string sqlitePath = Path.Combine(dbDir, "NasaApod.db");
            _sqliteConnectionString = $"Data Source={sqlitePath}";

            // Auto-detect whether to use SQLite or SQL Server
            if (_connectionString.Contains(".db") ||
                (_connectionString.Contains("Data Source=") && !_connectionString.Contains("Server=")) ||
                !OperatingSystem.IsWindows() ||
                _connectionString.Contains("DESKTOP-"))
            {
                _useSqlite = true;
            }
            else
            {
                _useSqlite = false;
            }

            EnsureSqliteSchema();
        }

        private void EnsureSqliteSchema()
        {
            try
            {
                using var connection = new SqliteConnection(_sqliteConnectionString);
                connection.Open();

                string sql = @"
                    CREATE TABLE IF NOT EXISTS Apod
                    (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        [Date] TEXT NOT NULL UNIQUE,
                        Title TEXT NOT NULL,
                        Explanation TEXT NULL,
                        [Url] TEXT NOT NULL,
                        Hdurl TEXT NULL,
                        MediaType TEXT NOT NULL,
                        ServiceVersion TEXT NULL,
                        Copyright TEXT NULL,
                        SavedAt TEXT NOT NULL
                    );";

                using var command = new SqliteCommand(sql, connection);
                command.ExecuteNonQuery();

                // Ensure columns exist if table was created in an earlier session
                string checkColsSql = "PRAGMA table_info(Apod);";
                using var pragmaCmd = new SqliteCommand(checkColsSql, connection);
                using var reader = pragmaCmd.ExecuteReader();
                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (reader.Read())
                {
                    columns.Add(reader.GetString(1));
                }
                reader.Close();

                if (!columns.Contains("Hdurl"))
                {
                    using var addCol = new SqliteCommand("ALTER TABLE Apod ADD COLUMN Hdurl TEXT NULL;", connection);
                    addCol.ExecuteNonQuery();
                }
                if (!columns.Contains("Copyright"))
                {
                    using var addCol = new SqliteCommand("ALTER TABLE Apod ADD COLUMN Copyright TEXT NULL;", connection);
                    addCol.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing SQLite schema: {ex.Message}");
            }
        }

        // Tests the database connection
        public async Task<bool> TestConnectionAsync()
        {
            if (_useSqlite)
            {
                using var connection = new SqliteConnection(_sqliteConnectionString);
                await connection.OpenAsync();
                return connection.State == ConnectionState.Open;
            }
            else
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                return connection.State == ConnectionState.Open;
            }
        }

        // Saves a NASA picture if its date does not already exist; returns true if newly inserted
        public async Task<bool> SaveApodAsync(ApodDto picture)
        {
            // Reject any false placeholder records (e.g. generic NASA Science logo)
            if (string.IsNullOrWhiteSpace(picture.Title) ||
                picture.Title.Equals("NASA Science", StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(picture.Url) && picture.Url.Contains("nasa-logo", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (!_useSqlite)
            {
                try
                {
                    return await SaveApodSqlServerAsync(picture);
                }
                catch
                {
                    _useSqlite = true;
                }
            }

            return await SaveApodSqliteAsync(picture);
        }

        // Saves a batch of APOD records and returns the count of newly inserted items
        public async Task<int> SaveApodsBatchAsync(IEnumerable<ApodDto> pictures)
        {
            int insertedCount = 0;
            foreach (var picture in pictures)
            {
                if (await SaveApodAsync(picture))
                {
                    insertedCount++;
                }
            }
            return insertedCount;
        }

        private async Task<bool> SaveApodSqlServerAsync(ApodDto picture)
        {
            using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string sql = @"
                INSERT INTO Apod
                    ([Date], Title, Explanation, [Url], Hdurl, MediaType, ServiceVersion, Copyright)
                SELECT
                    @Date, @Title, @Explanation, @Url, @Hdurl, @MediaType, @ServiceVersion, @Copyright
                WHERE NOT EXISTS
                (
                    SELECT 1 FROM Apod WHERE [Date] = @Date
                );";

            using SqlCommand command = new SqlCommand(sql, connection);

            DateTime parsedDate = DateTime.TryParse(picture.Date, out var dt) ? dt : DateTime.UtcNow;
            command.Parameters.Add(new SqlParameter("@Date", SqlDbType.Date) { Value = parsedDate });
            command.Parameters.Add(new SqlParameter("@Title", SqlDbType.NVarChar, 300) { Value = (object?)picture.Title ?? "" });
            command.Parameters.Add(new SqlParameter("@Explanation", SqlDbType.NVarChar) { Value = (object?)picture.Explanation ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@Url", SqlDbType.NVarChar, 2000) { Value = (object?)picture.Url ?? "" });
            command.Parameters.Add(new SqlParameter("@Hdurl", SqlDbType.NVarChar, 2000) { Value = (object?)picture.Hdurl ?? (object?)picture.Url ?? "" });
            command.Parameters.Add(new SqlParameter("@MediaType", SqlDbType.NVarChar, 50) { Value = (object?)picture.MediaType ?? "image" });
            command.Parameters.Add(new SqlParameter("@ServiceVersion", SqlDbType.NVarChar, 20) { Value = (object?)picture.ServiceVersion ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@Copyright", SqlDbType.NVarChar, 200) { Value = (object?)picture.Copyright ?? DBNull.Value });

            int rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        private async Task<bool> SaveApodSqliteAsync(ApodDto picture)
        {
            using var connection = new SqliteConnection(_sqliteConnectionString);
            await connection.OpenAsync();

            string sql = @"
                INSERT OR IGNORE INTO Apod
                    ([Date], Title, Explanation, [Url], Hdurl, MediaType, ServiceVersion, Copyright, SavedAt)
                VALUES
                    (@Date, @Title, @Explanation, @Url, @Hdurl, @MediaType, @ServiceVersion, @Copyright, @SavedAt);";

            using var command = new SqliteCommand(sql, connection);

            DateTime parsedDate = DateTime.TryParse(picture.Date, out var dt) ? dt : DateTime.UtcNow;
            command.Parameters.AddWithValue("@Date", parsedDate.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@Title", picture.Title ?? "");
            command.Parameters.AddWithValue("@Explanation", (object?)picture.Explanation ?? DBNull.Value);
            command.Parameters.AddWithValue("@Url", picture.Url ?? "");
            command.Parameters.AddWithValue("@Hdurl", (object?)picture.Hdurl ?? (object?)picture.Url ?? DBNull.Value);
            command.Parameters.AddWithValue("@MediaType", picture.MediaType ?? "image");
            command.Parameters.AddWithValue("@ServiceVersion", (object?)picture.ServiceVersion ?? DBNull.Value);
            command.Parameters.AddWithValue("@Copyright", (object?)picture.Copyright ?? DBNull.Value);
            command.Parameters.AddWithValue("@SavedAt", DateTime.UtcNow.ToString("o"));

            int rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        // Retrieves all saved NASA pictures ordered newest first
        public async Task<List<Apod>> GetAllApodsAsync()
        {
            if (!_useSqlite)
            {
                try
                {
                    return await GetAllApodsSqlServerAsync();
                }
                catch
                {
                    _useSqlite = true;
                }
            }

            return await GetAllApodsSqliteAsync();
        }

        // Retrieves saved NASA pictures filtered by a specific date or date range
        public async Task<List<Apod>> GetApodsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            if (!_useSqlite)
            {
                try
                {
                    return await GetApodsByDateRangeSqlServerAsync(startDate, endDate);
                }
                catch
                {
                    _useSqlite = true;
                }
            }

            return await GetApodsByDateRangeSqliteAsync(startDate, endDate);
        }

        private async Task<List<Apod>> GetApodsByDateRangeSqlServerAsync(DateTime startDate, DateTime endDate)
        {
            List<Apod> pictures = new List<Apod>();
            using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string sql = @"
                SELECT Id, [Date], Title, Explanation, [Url],
                       ISNULL(Hdurl, [Url]), MediaType, ServiceVersion,
                       ISNULL(Copyright, ''), SavedAt
                FROM Apod
                WHERE [Date] >= @StartDate AND [Date] <= @EndDate
                ORDER BY [Date] DESC;";

            using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.Add(new SqlParameter("@StartDate", SqlDbType.Date) { Value = startDate.Date });
            command.Parameters.Add(new SqlParameter("@EndDate", SqlDbType.Date) { Value = endDate.Date });
            using SqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                Apod picture = new Apod
                {
                    Id = reader.GetInt32(0),
                    Date = reader.GetDateTime(1),
                    Title = reader.GetString(2),
                    Explanation = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Url = reader.GetString(4),
                    Hdurl = reader.IsDBNull(5) ? reader.GetString(4) : reader.GetString(5),
                    MediaType = reader.GetString(6),
                    ServiceVersion = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    Copyright = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    SavedAt = reader.GetDateTime(9)
                };
                pictures.Add(picture);
            }
            return pictures;
        }

        private async Task<List<Apod>> GetApodsByDateRangeSqliteAsync(DateTime startDate, DateTime endDate)
        {
            List<Apod> pictures = new List<Apod>();
            using var connection = new SqliteConnection(_sqliteConnectionString);
            await connection.OpenAsync();

            string sql = @"
                SELECT Id, [Date], Title, Explanation, [Url],
                       COALESCE(Hdurl, [Url]), MediaType, ServiceVersion,
                       COALESCE(Copyright, ''), SavedAt
                FROM Apod
                WHERE date([Date]) >= date(@StartDate) AND date([Date]) <= date(@EndDate)
                ORDER BY [Date] DESC;";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@StartDate", startDate.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@EndDate", endDate.ToString("yyyy-MM-dd"));
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string rawDate = reader.GetString(1);
                DateTime picDate = DateTime.TryParse(rawDate, out var d) ? d : DateTime.MinValue;

                string rawSaved = reader.GetString(9);
                DateTime savedAt = DateTime.TryParse(rawSaved, out var s) ? s : DateTime.UtcNow;

                Apod picture = new Apod
                {
                    Id = reader.GetInt32(0),
                    Date = picDate,
                    Title = reader.GetString(2),
                    Explanation = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Url = reader.GetString(4),
                    Hdurl = reader.IsDBNull(5) ? reader.GetString(4) : reader.GetString(5),
                    MediaType = reader.GetString(6),
                    ServiceVersion = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    Copyright = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    SavedAt = savedAt
                };
                pictures.Add(picture);
            }
            return pictures;
        }

        private async Task<List<Apod>> GetAllApodsSqlServerAsync()
        {
            List<Apod> pictures = new List<Apod>();
            using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string sql = @"
                SELECT Id, [Date], Title, Explanation, [Url],
                       ISNULL(Hdurl, [Url]), MediaType, ServiceVersion,
                       ISNULL(Copyright, ''), SavedAt
                FROM Apod
                ORDER BY [Date] DESC;";

            using SqlCommand command = new SqlCommand(sql, connection);
            using SqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                Apod picture = new Apod
                {
                    Id = reader.GetInt32(0),
                    Date = reader.GetDateTime(1),
                    Title = reader.GetString(2),
                    Explanation = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Url = reader.GetString(4),
                    Hdurl = reader.IsDBNull(5) ? reader.GetString(4) : reader.GetString(5),
                    MediaType = reader.GetString(6),
                    ServiceVersion = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    Copyright = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    SavedAt = reader.GetDateTime(9)
                };

                pictures.Add(picture);
            }

            return pictures;
        }

        private async Task<List<Apod>> GetAllApodsSqliteAsync()
        {
            List<Apod> pictures = new List<Apod>();
            using var connection = new SqliteConnection(_sqliteConnectionString);
            await connection.OpenAsync();

            string sql = @"
                SELECT Id, [Date], Title, Explanation, [Url],
                       COALESCE(Hdurl, [Url]), MediaType, ServiceVersion,
                       COALESCE(Copyright, ''), SavedAt
                FROM Apod
                ORDER BY [Date] DESC;";

            using var command = new SqliteCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string rawDate = reader.GetString(1);
                DateTime picDate = DateTime.TryParse(rawDate, out var d) ? d : DateTime.MinValue;

                string rawSaved = reader.GetString(9);
                DateTime savedAt = DateTime.TryParse(rawSaved, out var s) ? s : DateTime.UtcNow;

                Apod picture = new Apod
                {
                    Id = reader.GetInt32(0),
                    Date = picDate,
                    Title = reader.GetString(2),
                    Explanation = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Url = reader.GetString(4),
                    Hdurl = reader.IsDBNull(5) ? reader.GetString(4) : reader.GetString(5),
                    MediaType = reader.GetString(6),
                    ServiceVersion = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    Copyright = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    SavedAt = savedAt
                };

                pictures.Add(picture);
            }

            return pictures;
        }
    }
}
