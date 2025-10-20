# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["TaskManagement.sln", "./"]
COPY ["src/TaskManagement.API/TaskManagement.API.csproj", "src/TaskManagement.API/"]
COPY ["src/TaskManagement.Application/TaskManagement.Application.csproj", "src/TaskManagement.Application/"]
COPY ["src/TaskManagement.Domain/TaskManagement.Domain.csproj", "src/TaskManagement.Domain/"]
COPY ["src/TaskManagement.Infrastructure/TaskManagement.Infrastructure.csproj", "src/TaskManagement.Infrastructure/"]
COPY ["src/TaskManagement.Contracts/TaskManagement.Contracts.csproj", "src/TaskManagement.Contracts/"]

# Restore dependencies
RUN dotnet restore "src/TaskManagement.API/TaskManagement.API.csproj"

# Copy all source files
COPY . .

# Build the application
WORKDIR "/src/src/TaskManagement.API"
RUN dotnet build "TaskManagement.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "TaskManagement.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "TaskManagement.API.dll"]
