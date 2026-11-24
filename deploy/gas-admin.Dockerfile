# GAS-Admin compilado dentro de Docker. Se despliega con el despliegue automático de GAS-Admin
# (desde la instancia local; una instancia no puede reemplazar su propio contenedor).
# A mano, desde la raíz del repositorio: docker build -f deploy/gas-admin.Dockerfile -t gas-admin .
#
# Variables mínimas: ASPNETCORE_ENVIRONMENT=Production, ConnectionStrings__DefaultConnection,
# Seguridad__LlaveMaestra (la misma que cifró las llaves de la base), AllowedHosts.
# Volúmenes: /keys (llaves de sesión) y /app/App_Data/archivos (archivos de proyectos).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Primero solo el proyecto, para que la restauración de paquetes quede en caché entre despliegues.
COPY GAS.Admin.csproj ./
RUN dotnet restore GAS.Admin.csproj
COPY . .
RUN dotnet publish GAS.Admin.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "GAS.Admin.dll"]
