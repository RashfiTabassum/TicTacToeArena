# -------- Build Stage --------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first (API + Shared)
COPY TicTacToeArena.Api/*.csproj TicTacToeArena.Api/
COPY TicTacToeArena.Shared/*.csproj TicTacToeArena.Shared/

# Restore
WORKDIR /src/TicTacToeArena.Api
RUN dotnet restore

# Copy everything
COPY TicTacToeArena.Api/. ./
COPY TicTacToeArena.Shared/. ../TicTacToeArena.Shared/

# Publish
RUN dotnet publish -c Release -o /app/publish

# -------- Runtime Stage --------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Render uses PORT environment variable
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "TicTacToeArena.Api.dll"]
