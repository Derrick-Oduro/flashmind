# Build context is the repository root: docker build -f Flashminds.Web/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Flashminds.Contracts/Flashminds.Contracts.csproj", "Flashminds.Contracts/"]
COPY ["Flashminds.Web/Flashminds.Web.csproj", "Flashminds.Web/"]
RUN dotnet restore "Flashminds.Web/Flashminds.Web.csproj"

COPY Flashminds.Contracts/ Flashminds.Contracts/
COPY Flashminds.Web/ Flashminds.Web/
RUN dotnet publish "Flashminds.Web/Flashminds.Web.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Flashminds.Web.dll"]
