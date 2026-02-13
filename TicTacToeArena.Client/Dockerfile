# Use .NET 8 SDK for build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files
COPY TicTacToeArena.Client/*.csproj TicTacToeArena.Client/
COPY TicTacToeArena.Shared/*.csproj TicTacToeArena.Shared/

WORKDIR /src/TicTacToeArena.Client
RUN dotnet restore

# Copy all source files
COPY TicTacToeArena.Client/. ./ 
COPY TicTacToeArena.Shared/. ../TicTacToeArena.Shared/

# Publish
RUN dotnet publish -c Release -o /app/publish

# Use nginx for serving Blazor WASM
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html
RUN rm -rf ./*
COPY --from=build /app/publish/wwwroot ./
COPY TicTacToeArena.Client/nginx.conf /etc/nginx/templates/default.conf.template
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
