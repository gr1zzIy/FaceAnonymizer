# Multi-stage Docker build for FaceAnonymizer API (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first to leverage Docker layer caching for restore
COPY FaceAnonymizer.Api/FaceAnonymizer.Api.csproj FaceAnonymizer.Api/
COPY FaceAnonymizer.Application/FaceAnonymizer.Application.csproj FaceAnonymizer.Application/
COPY FaceAnonymizer.Core/FaceAnonymizer.Core.csproj FaceAnonymizer.Core/
COPY FaceAnonymizer.Infrastructure/FaceAnonymizer.Infrastructure.csproj FaceAnonymizer.Infrastructure/

RUN dotnet restore FaceAnonymizer.Api/FaceAnonymizer.Api.csproj

# Copy everything else and publish
COPY . .
RUN dotnet publish FaceAnonymizer.Api/FaceAnonymizer.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

# Ensure storage folders exist for batch processing and outputs
RUN mkdir -p /app/storage/batch-input /app/storage/batch-output /app/storage/results

ENTRYPOINT ["dotnet", "FaceAnonymizer.Api.dll"]

