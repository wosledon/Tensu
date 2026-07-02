FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Tensu.Core/Tensu.Core.csproj src/Tensu.Core/
COPY src/Tensu.Api/Tensu.Api.csproj src/Tensu.Api/
RUN dotnet restore src/Tensu.Api/Tensu.Api.csproj
COPY src/ src/
RUN dotnet publish src/Tensu.Api/Tensu.Api.csproj -c Release -o /app/publish

FROM node:24-alpine AS frontend
WORKDIR /frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ .
RUN npm run build

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=frontend /frontend/dist ./wwwroot
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "Tensu.Api.dll"]
