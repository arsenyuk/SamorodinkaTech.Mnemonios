FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Applications/Steward/SamorodinkaTech.Mnemonios.Steward.csproj", "src/Applications/Steward/"]
COPY ["src/Core/Infrastructure/SamorodinkaTech.Mnemonios.Infrastructure.csproj", "src/Core/Infrastructure/"]
COPY ["src/Core/Domain/SamorodinkaTech.Mnemonios.Domain.csproj", "src/Core/Domain/"]
COPY ["src/Core/Common/SamorodinkaTech.Mnemonios.Common.csproj", "src/Core/Common/"]
RUN dotnet restore "src/Applications/Steward/SamorodinkaTech.Mnemonios.Steward.csproj"

COPY src/ src/
RUN dotnet publish "src/Applications/Steward/SamorodinkaTech.Mnemonios.Steward.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5080
ENTRYPOINT ["dotnet", "SamorodinkaTech.Mnemonios.Steward.dll"]
