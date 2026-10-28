# syntax=docker/dockerfile:1

# ---- 1. Build the Angular app --------------------------------------------
FROM node:22-alpine AS web
WORKDIR /web
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build -- --configuration production

# ---- 2. Build the ASP.NET Core API ----------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS api
WORKDIR /src
# Copy project files first so the restore layer is cached until dependencies change.
COPY backend/GameHub.sln ./
COPY backend/src/GameHub.Api/GameHub.Api.csproj src/GameHub.Api/
COPY backend/src/GameHub.Auth/GameHub.Auth.csproj src/GameHub.Auth/
COPY backend/src/GameHub.Favorites/GameHub.Favorites.csproj src/GameHub.Favorites/
COPY backend/src/GameHub.Games/GameHub.Games.csproj src/GameHub.Games/
COPY backend/src/GameHub.Shared/GameHub.Shared.csproj src/GameHub.Shared/
RUN dotnet restore src/GameHub.Api/GameHub.Api.csproj
COPY backend/src/ src/
RUN dotnet publish src/GameHub.Api/GameHub.Api.csproj -c Release -o /app --no-restore

# ---- 3. Runtime image: API + the compiled Angular files in wwwroot --------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist/gamehub/browser ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "GameHub.Api.dll"]
