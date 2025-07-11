# Multi-stage Dockerfile for BizConnect ASP.NET Core 8 Application
# Optimized for production deployment on Ubuntu 24.04

# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file and project files
COPY BizConnect.sln ./
COPY BizConnect/BizConnect.csproj ./BizConnect/
COPY BizConnect.Services/BizConnect.Services.csproj ./BizConnect.Services/
COPY BizConnect.Dal/BizConnect.Dal.csproj ./BizConnect.Dal/
COPY BizConnect.Tests/BizConnect.Tests.csproj ./BizConnect.Tests/

# Restore NuGet packages
RUN dotnet restore BizConnect.sln

# Copy source code
COPY . .

# Build the application
WORKDIR /src/BizConnect
RUN dotnet build BizConnect.csproj -c Release -o /app/build

# Publish Stage
FROM build AS publish
RUN dotnet publish BizConnect.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Install required packages for Ubuntu 24.04
RUN apt-get update && apt-get install -y \
    curl \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r bizconnect && useradd -r -g bizconnect bizconnect

# Set working directory
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Create directories and set permissions
RUN mkdir -p /app/logs /app/uploads && \
    chown -R bizconnect:bizconnect /app

# Switch to non-root user
USER bizconnect

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "BizConnect.dll"]
