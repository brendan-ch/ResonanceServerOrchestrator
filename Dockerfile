# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy backend and frontend folders
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .

ENV HOST=+
ENV PORT=9000

EXPOSE ${PORT}

ENTRYPOINT ["dotnet", "ResonanceServerOrchestrator.dll"]
