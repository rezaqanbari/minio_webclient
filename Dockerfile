# Multi-stage build for ASP.NET Core 9 Web Manager

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["minio_csharpClient.csproj", "./"]
RUN dotnet restore "minio_csharpClient.csproj"

# Copy full source and publish
COPY . .
RUN dotnet publish "minio_csharpClient.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Expose default container port
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "minio_csharpClient.dll"]
