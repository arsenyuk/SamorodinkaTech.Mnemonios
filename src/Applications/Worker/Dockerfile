FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Applications/Worker/SamorodinkaTech.Mnemonios.Worker.csproj", "src/Applications/Worker/"]
COPY ["src/Core/Infrastructure/SamorodinkaTech.Mnemonios.Infrastructure.csproj", "src/Core/Infrastructure/"]
COPY ["src/Core/Domain/SamorodinkaTech.Mnemonios.Domain.csproj", "src/Core/Domain/"]
RUN dotnet restore "src/Applications/Worker/SamorodinkaTech.Mnemonios.Worker.csproj"

COPY src/ src/
RUN dotnet publish "src/Applications/Worker/SamorodinkaTech.Mnemonios.Worker.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "SamorodinkaTech.Mnemonios.Worker.dll"]
