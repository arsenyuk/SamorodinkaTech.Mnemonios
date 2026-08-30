FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Api/SamorodinkaTech.Mnemonios.Api.csproj", "src/Api/"]
COPY ["src/Infrastructure/SamorodinkaTech.Mnemonios.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Domain/SamorodinkaTech.Mnemonios.Domain.csproj", "src/Domain/"]
RUN dotnet restore "src/Api/SamorodinkaTech.Mnemonios.Api.csproj"

COPY src/ src/
RUN dotnet publish "src/Api/SamorodinkaTech.Mnemonios.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5080
ENTRYPOINT ["dotnet", "SamorodinkaTech.Mnemonios.Api.dll"]
