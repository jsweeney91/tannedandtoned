# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY tanningTribe/tanningTribe.csproj tanningTribe/
RUN dotnet restore tanningTribe/tanningTribe.csproj
COPY tanningTribe/ tanningTribe/
RUN dotnet publish tanningTribe/tanningTribe.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Railway injects PORT; Kestrel listens on it.
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} dotnet tanningTribe.dll"]
