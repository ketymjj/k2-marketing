# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY K2Marketing.sln ./
COPY src/K2Marketing.Domain/K2Marketing.Domain.csproj src/K2Marketing.Domain/
COPY src/K2Marketing.Application/K2Marketing.Application.csproj src/K2Marketing.Application/
COPY src/K2Marketing.Infrastructure/K2Marketing.Infrastructure.csproj src/K2Marketing.Infrastructure/
COPY src/K2Marketing.Api/K2Marketing.Api.csproj src/K2Marketing.Api/
RUN dotnet restore src/K2Marketing.Api/K2Marketing.Api.csproj
COPY src/ src/
RUN dotnet publish src/K2Marketing.Api/K2Marketing.Api.csproj -c Release -o /app --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
# tzdata: fuso America/Sao_Paulo (data local da agência); curl: healthcheck.
RUN apt-get update && apt-get install -y --no-install-recommends tzdata curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app ./
# Pasta dos criativos enviados (montada como volume persistente no docker-compose).
RUN mkdir -p /app/storage && chown -R app:app /app/storage
ENV ASPNETCORE_URLS=http://+:8080 \
    Storage__RootPath=/app/storage
EXPOSE 8080
USER app
HEALTHCHECK --interval=15s --timeout=5s --retries=5 CMD curl -fs http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "K2Marketing.Api.dll"]
