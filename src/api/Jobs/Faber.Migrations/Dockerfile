FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Shared build settings and central package versions; restore needs them before any project.
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]

COPY ["src/api/Jobs/Faber.Migrations/Faber.Migrations.csproj", "src/api/Jobs/Faber.Migrations/"]
COPY ["src/api/Aspire/Faber.ServiceDefaults/Faber.ServiceDefaults.csproj", "src/api/Aspire/Faber.ServiceDefaults/"]
COPY ["src/api/Modules/Resumes/Faber.Modules.Resumes.Infrastructure/Faber.Modules.Resumes.Infrastructure.csproj", "src/api/Modules/Resumes/Faber.Modules.Resumes.Infrastructure/"]
COPY ["src/api/Modules/Resumes/Faber.Modules.Resumes.Domain/Faber.Modules.Resumes.Domain.csproj", "src/api/Modules/Resumes/Faber.Modules.Resumes.Domain/"]
COPY ["src/api/Modules/Auth/Faber.Modules.Auth.Infrastructure/Faber.Modules.Auth.Infrastructure.csproj", "src/api/Modules/Auth/Faber.Modules.Auth.Infrastructure/"]

RUN dotnet restore "src/api/Jobs/Faber.Migrations/Faber.Migrations.csproj"

COPY . .

WORKDIR "/src/src/api/Jobs/Faber.Migrations"
RUN dotnet publish "Faber.Migrations.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Faber.Migrations.dll"]
