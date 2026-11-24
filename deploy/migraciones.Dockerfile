# Migraciones de EF Core de GAS-Admin empaquetadas (efbundle). El despliegue automático las ejecuta
# antes de reemplazar el contenedor, con el mismo archivo de variables (ConnectionStrings__DefaultConnection).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY GAS.Admin.csproj ./
RUN dotnet restore GAS.Admin.csproj
RUN dotnet tool install --tool-path /tools dotnet-ef --version 10.0.8
COPY . .
RUN /tools/dotnet-ef migrations bundle --project GAS.Admin.csproj --configuration Release --self-contained -o /out/efbundle

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
WORKDIR /bundle
COPY --from=build /out/efbundle .
ENTRYPOINT ["./efbundle"]
