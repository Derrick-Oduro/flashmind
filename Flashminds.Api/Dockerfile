# Build context is the repository root: docker build -f Flashminds.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Flashminds.Contracts/Flashminds.Contracts.csproj", "Flashminds.Contracts/"]
COPY ["Flashminds.Api/Flashminds.Api.csproj", "Flashminds.Api/"]
RUN dotnet restore "Flashminds.Api/Flashminds.Api.csproj"

COPY Flashminds.Contracts/ Flashminds.Contracts/
COPY Flashminds.Api/ Flashminds.Api/
RUN dotnet publish "Flashminds.Api/Flashminds.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Flashminds.Api.dll"]
