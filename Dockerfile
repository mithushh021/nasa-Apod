# ========================================================
# Dockerfile for NASA APOD Gallery on Railway / Cloud
# ========================================================

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["NasaApodGallery.csproj", "./"]
RUN dotnet restore "NasaApodGallery.csproj"

# Copy all source files and publish release
COPY . .
RUN dotnet publish "NasaApodGallery.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime Environment
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Copy pre-populated SQLite database and ensure directory exists
COPY --from=build /src/Database ./Database

# Default port (Railway dynamically overrides via $PORT environment variable)
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "NasaApodGallery.dll"]
