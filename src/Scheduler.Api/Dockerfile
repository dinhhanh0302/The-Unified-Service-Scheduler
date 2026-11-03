# Build context is the repository root (Directory.*.props and global.json live there):
#   docker build -f src/Scheduler.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Scheduler.Domain/Scheduler.Domain.csproj src/Scheduler.Domain/
COPY src/Scheduler.Application/Scheduler.Application.csproj src/Scheduler.Application/
COPY src/Scheduler.Infrastructure/Scheduler.Infrastructure.csproj src/Scheduler.Infrastructure/
COPY src/Scheduler.Api/Scheduler.Api.csproj src/Scheduler.Api/
RUN dotnet restore src/Scheduler.Api/Scheduler.Api.csproj

COPY src/ src/
RUN dotnet publish src/Scheduler.Api/Scheduler.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Scheduler.Api.dll"]
