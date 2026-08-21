# ---- Derleme aşaması ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Önce csproj + restore (katman önbelleği için)
COPY CaptivePortalSms.csproj ./
RUN dotnet restore

# Kaynakları kopyala ve yayınla
COPY . ./
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ---- Çalışma aşaması (küçük runtime imajı) ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Healthcheck için curl (aspnet imajında yok)
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

# Kalıcı uyumluluk veritabanı /app/data volume'ünde tutulur.
ENV ConnectionStrings__ComplianceDb="Data Source=/app/data/compliance.db"
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
VOLUME /app/data

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "CaptivePortalSms.dll"]
