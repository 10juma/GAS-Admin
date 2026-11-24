using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GAS.Admin.Data.Context;

/// <summary>
/// Cómo crean el contexto las herramientas de EF (migrations add, migrations bundle, database update) sin
/// arrancar la aplicación. Así el paquete de migraciones (efbundle) se puede compilar en Docker sin cadena
/// de conexión, y al ejecutarlo la toma de ConnectionStrings__DefaultConnection (la variable del contenedor)
/// o, en la Mac, de appsettings.Development.json.
/// </summary>
public class GasDbContextFactory : IDesignTimeDbContextFactory<GasDbContext>
{
    /// <summary>Solo para compilar las migraciones: nunca se usa para conectarse.</summary>
    private const string SinConexion = "Server=localhost;Database=GAS;Integrated Security=true;TrustServerCertificate=True";

    public GasDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var cadena = config.GetConnectionString("DefaultConnection") is { Length: > 0 } c ? c : SinConexion;
        return new GasDbContext(new DbContextOptionsBuilder<GasDbContext>().UseSqlServer(cadena).Options);
    }
}
