# syntax=docker/dockerfile:1

# 1. Build the web app
FROM node:26-alpine AS web
WORKDIR /web
COPY src/ManagementApp.Web/package.json src/ManagementApp.Web/package-lock.json ./
RUN npm ci
COPY src/ManagementApp.Web/ ./
RUN npm run build

# 2. Build and publish the API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ManagementApp.Domain/ManagementApp.Domain.csproj src/ManagementApp.Domain/
COPY src/ManagementApp.Application/ManagementApp.Application.csproj src/ManagementApp.Application/
COPY src/ManagementApp.Infrastructure/ManagementApp.Infrastructure.csproj src/ManagementApp.Infrastructure/
COPY src/ManagementApp.Api/ManagementApp.Api.csproj src/ManagementApp.Api/
RUN dotnet restore src/ManagementApp.Api/ManagementApp.Api.csproj
COPY src/ManagementApp.Domain/ src/ManagementApp.Domain/
COPY src/ManagementApp.Application/ src/ManagementApp.Application/
COPY src/ManagementApp.Infrastructure/ src/ManagementApp.Infrastructure/
COPY src/ManagementApp.Api/ src/ManagementApp.Api/
RUN dotnet publish src/ManagementApp.Api/ManagementApp.Api.csproj -c Release -o /app --no-restore

# 3. Runtime image: the API serves the web app from wwwroot
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# Data protection keys (auth cookies) live here; mount a volume to keep sessions across restarts.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ManagementApp.Api.dll"]
