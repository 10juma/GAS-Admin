using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Auditoria;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Dashboard;

public class IndexModel : PageModel
{
    private readonly GasDbContext _db;

    public IndexModel(GasDbContext db) => _db = db;

    public int ProductosOperacion { get; private set; }
    public int ProductosTotal { get; private set; }
    public int ClientesActivos { get; private set; }
    public int ClientesTotal { get; private set; }
    public Dictionary<string, decimal> CostoMensual { get; private set; } = [];
    public int Renovaciones30 { get; private set; }
    public bool PuedeVerServicios { get; private set; }
    public sealed record Renovacion(string Servicio, DateOnly Fecha, int Dias, decimal? Costo, string Moneda);
    public List<Renovacion> ProximasRenovaciones { get; private set; } = [];

    public int UsuariosActivos { get; private set; }
    public int UsuariosTotales { get; private set; }
    public int Roles { get; private set; }
    public int EventosHoy { get; private set; }
    public int AlertasHoy { get; private set; }
    public bool PuedeVerAuditoria { get; private set; }
    public bool PuedeVerUsuarios { get; private set; }
    public List<AuditLog> Actividad { get; private set; } = [];

    public sealed record Integrante(string Id, string Nombre, string Email, bool Activo, DateTime? UltimoAccesoUtc, IReadOnlyList<string> Roles);
    public List<Integrante> Equipo { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var desde = DateTime.UtcNow.AddHours(-24);
        var alertas = AccionesAuditoria.DeCategoria(CategoriaAccion.Alertas);

        ProductosTotal = await _db.Productos.CountAsync();
        ProductosOperacion = await _db.Productos.CountAsync(p => p.Estado == EstadoProducto.EnOperacion);
        ClientesTotal = await _db.Clientes.CountAsync();
        ClientesActivos = await _db.Clientes.CountAsync(c => c.Estado == EstadoCliente.Activo);

        var servicios = await _db.ServiciosExternos.AsNoTracking()
            .Where(s => s.Estado != EstadoServicio.Cancelado)
            .ToListAsync();
        CostoMensual = servicios.Where(s => s.CostoMensual is not null)
            .GroupBy(s => s.Moneda).ToDictionary(g => g.Key, g => g.Sum(s => s.CostoMensual!.Value));
        var hoy = DateOnly.FromDateTime(Fechas.ALocal(DateTime.UtcNow));
        var renovaciones = servicios.Where(s => s.ProximaRenovacion is not null)
            .Select(s => new Renovacion(s.Nombre, s.ProximaRenovacion!.Value, s.ProximaRenovacion.Value.DayNumber - hoy.DayNumber, s.Costo, s.Moneda))
            .OrderBy(r => r.Fecha)
            .ToList();
        Renovaciones30 = renovaciones.Count(r => r.Dias <= 30);
        ProximasRenovaciones = renovaciones.Take(6).ToList();
        PuedeVerServicios = User.TienePermiso(Permisos.Infraestructura.Ver);

        UsuariosTotales = await _db.Users.CountAsync();
        UsuariosActivos = await _db.Users.CountAsync(u => u.Activo);
        Roles = await _db.Roles.CountAsync();
        EventosHoy = await _db.AuditLogs.CountAsync(a => a.FechaUtc >= desde);
        AlertasHoy = await _db.AuditLogs.CountAsync(a => a.FechaUtc >= desde && alertas.Contains(a.Accion));

        PuedeVerAuditoria = User.TienePermiso(Permisos.Auditoria.Ver);
        if (PuedeVerAuditoria)
        {
            Actividad = await _db.AuditLogs.AsNoTracking()
                .OrderByDescending(a => a.FechaUtc)
                .Take(10)
                .ToListAsync();
        }

        PuedeVerUsuarios = User.TienePermiso(Permisos.Usuarios.Ver);
        if (PuedeVerUsuarios)
        {
            var usuarios = await _db.Users.AsNoTracking()
                .OrderByDescending(u => u.Activo)
                .ThenByDescending(u => u.UltimoAccesoUtc)
                .Take(8)
                .ToListAsync();

            var ids = usuarios.Select(u => u.Id).ToList();
            var roles = await (
                    from ur in _db.UserRoles
                    join r in _db.Roles on ur.RoleId equals r.Id
                    where ids.Contains(ur.UserId)
                    select new { ur.UserId, r.Name })
                .ToListAsync();

            Equipo = usuarios.Select(u => new Integrante(
                    u.Id, u.NombreVisible, u.Email ?? "", u.Activo, u.UltimoAccesoUtc,
                    roles.Where(r => r.UserId == u.Id).Select(r => r.Name!).OrderBy(n => n == RolesBase.Admin ? 0 : 1).ThenBy(n => n).ToList()))
                .ToList();
        }
    }
}
