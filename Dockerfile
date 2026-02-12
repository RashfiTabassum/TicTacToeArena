# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first (for better caching)
COPY TicTacToeArena.Api/*.csproj TicTacToeArena.Api/
COPY TicTacToeArena.Shared/*.csproj TicTacToeArena.Shared/

RUN dotnet restore TicTacToeArena.Api/TicTacToeArena.Api.csproj

# Copy everything
COPY . .

WORKDIR /src/TicTacToeArena.Api
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TicTacToeArena.Api.dll"]
