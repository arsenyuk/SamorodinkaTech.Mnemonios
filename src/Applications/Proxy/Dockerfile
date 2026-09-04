FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Applications/Proxy/SamorodinkaTech.Mnemonios.Proxy.csproj", "src/Applications/Proxy/"]
COPY ["src/Core/Infrastructure/SamorodinkaTech.Mnemonios.Infrastructure.csproj", "src/Core/Infrastructure/"]
COPY ["src/Core/Domain/SamorodinkaTech.Mnemonios.Domain.csproj", "src/Core/Domain/"]
RUN dotnet restore "src/Applications/Proxy/SamorodinkaTech.Mnemonios.Proxy.csproj"

COPY src/ src/
RUN dotnet publish "src/Applications/Proxy/SamorodinkaTech.Mnemonios.Proxy.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5080
ENTRYPOINT ["dotnet", "SamorodinkaTech.Mnemonios.Proxy.dll"]
