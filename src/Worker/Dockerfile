FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Worker/SamorodinkaTech.Mnemonios.Worker.csproj", "src/Worker/"]
COPY ["src/Infrastructure/SamorodinkaTech.Mnemonios.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Domain/SamorodinkaTech.Mnemonios.Domain.csproj", "src/Domain/"]
RUN dotnet restore "src/Worker/SamorodinkaTech.Mnemonios.Worker.csproj"

COPY src/ src/
RUN dotnet publish "src/Worker/SamorodinkaTech.Mnemonios.Worker.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "SamorodinkaTech.Mnemonios.Worker.dll"]
