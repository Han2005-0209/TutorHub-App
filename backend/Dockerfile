# Stage 1: Build & Publish .NET 10 API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/TutorHub.Domain/TutorHub.Domain.csproj src/TutorHub.Domain/
COPY src/TutorHub.Application/TutorHub.Application.csproj src/TutorHub.Application/
COPY src/TutorHub.Infrastructure/TutorHub.Infrastructure.csproj src/TutorHub.Infrastructure/
COPY src/TutorHub.Api/TutorHub.Api.csproj src/TutorHub.Api/
RUN dotnet restore src/TutorHub.Api/TutorHub.Api.csproj

COPY src/ src/
RUN dotnet publish src/TutorHub.Api/TutorHub.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Minimal ASP.NET Core 10 Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN mkdir -p /app/storage/public /app/storage/private && chown -R app:app /app/storage
COPY --from=build /app/publish ./

USER app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TutorHub.Api.dll"]
