FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Steward/SamorodinkaTech.Mnemonios.Steward.csproj", "src/Steward/"]
COPY ["src/Infrastructure/SamorodinkaTech.Mnemonios.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Domain/SamorodinkaTech.Mnemonios.Domain.csproj", "src/Domain/"]
RUN dotnet restore "src/Steward/SamorodinkaTech.Mnemonios.Steward.csproj"

COPY src/ src/
RUN dotnet publish "src/Steward/SamorodinkaTech.Mnemonios.Steward.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5080
ENTRYPOINT ["dotnet", "SamorodinkaTech.Mnemonios.Steward.dll"]
