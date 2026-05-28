# Stage 1: Build and publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy solution-level files for restore
COPY LDK.RideClub.Bot.slnx .
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .

# Copy all project files for restore
COPY src/LDK.RideClub.Bot/RideClub.Bot.csproj src/LDK.RideClub.Bot/
COPY src/LDK.RideClub.Bot.Abstractions/LDK.RideClub.Bot.Abstractions.csproj src/LDK.RideClub.Bot.Abstractions/
COPY src/LDK.RideClub.Bot.Domain/LDK.RideClub.Bot.Domain.csproj src/LDK.RideClub.Bot.Domain/
COPY src/LDK.RideClub.Bot.Adapters.WhatsApp/LDK.RideClub.Bot.Adapters.WhatsApp.csproj src/LDK.RideClub.Bot.Adapters.WhatsApp/
COPY src/LDK.RideClub.Bot.Persistence/LDK.RideClub.Bot.Persistence.csproj src/LDK.RideClub.Bot.Persistence/
COPY src/LDK.RideClub.Bot.Observability/LDK.RideClub.Bot.Observability.csproj src/LDK.RideClub.Bot.Observability/

# Restore dependencies
RUN dotnet restore src/LDK.RideClub.Bot/RideClub.Bot.csproj

# Copy all source code
COPY src/ src/

# Publish the application
RUN dotnet publish src/LDK.RideClub.Bot/RideClub.Bot.csproj -c "${BUILD_CONFIGURATION}" -o /app/publish --no-restore

# Stage 2: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV DEPLOYMENT_MODE=kestrel

EXPOSE 8080

ENTRYPOINT ["dotnet", "RideClub.Bot.dll"]
