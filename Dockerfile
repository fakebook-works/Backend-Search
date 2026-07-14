FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BackEndSearchFakebook.csproj", "."]
RUN dotnet restore "./BackEndSearchFakebook.csproj"
COPY . .
RUN dotnet publish "./BackEndSearchFakebook.csproj" \
    -c "$BUILD_CONFIGURATION" \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

FROM base AS final
WORKDIR /app
USER app
COPY --from=build --chown=app:app /app/publish .
ENTRYPOINT ["dotnet", "BackEndSearchFakebook.dll"]
