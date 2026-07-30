FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
# Health probes need an HTTP client. Npgsql loads GSSAPI dynamically even when
# password authentication is used.
RUN apk add --no-cache curl krb5-libs
EXPOSE 1004
ENV ASPNETCORE_HTTP_PORTS=1004

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
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
