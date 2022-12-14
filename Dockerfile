#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build
WORKDIR /src
COPY ["MyUpwayMQTTBridge.csproj", "."]
RUN dotnet restore "./MyUpwayMQTTBridge.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "MyUpwayMQTTBridge.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MyUpwayMQTTBridge.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyUpwayMQTTBridge.dll"]