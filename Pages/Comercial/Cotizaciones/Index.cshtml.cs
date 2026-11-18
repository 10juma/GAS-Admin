using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Pages.Comercial.Oportunidades;
using GAS.Admin.Security;
using GAS.Admin.Services.Auditoria;
using GAS.Admin.Services.Catalogos;
using GAS.Admin.Services.Comercial;
using GAS.Admin.Services.Correo;
using GAS.Admin.Services.Navegacion;
using GAS.Admin.Services.Proyectos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Comercial.Cotizaciones;

[Authorize(Policy = Permisos.Comercial.Ver)]
public class IndexModel : PageModel
{
    public const string FiltroVencidas = "Vencida";
    private const int DiasPorVencer = 5;

    private readonly GasDbContext _db;
    private readonly ContadoresMenu _contadores;
    private readonly GeneradorPdfCotizacion _pdf;
    private readonly IServicioCorreo _correo;
    private readonly PlantillaCorreo _plantilla;
    private readonly IAuditoria _auditoria;
    private readonly ServicioTrabajos _trabajos;

    public IndexModel(GasDbContext db, ContadoresMenu contadores, GeneradorPdfCotizacion pdf, IServicioCorreo correo, IAuditoria auditoria, ServicioTrabajos trabajos, PlantillaCorreo plantilla)
    {
        _db = db;
        _contadores = contadores;
        _pdf = pdf;
        _correo = correo;
        _plantilla = plantilla;
        _auditoria = auditoria;
        _trabajos = trabajos;
    }

    [BindProperty(SupportsGet = true)] public string? Estado { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    public sealed record Fila(Cotizacion Cotizacion, string Cliente, string? Oportunidad, string? Responsable, bool Vencida, int? DiasRestantes);

    public List<Fila> Cotizaciones { get; private set; } = [];
    public Dictionary<string, int> PorEstado { get; private set; } = [];
    public int Total { get; private set; }
    public int Abiertas { get; private set; }
    public int PorVencer { get; private set; }
    public Dictionary<string, decimal> AprobadoMes { get; private set; } = [];
    public decimal? TasaAprobacion { get; private set; }
    public bool PuedeGestionar => User.TienePermiso(Permisos.Comercial.Gestionar);
    public CotizacionModalVm? Modal { get; private set; }

    private DateOnly Hoy => DateOnly.FromDateTime(Fechas.ALocal(DateTime.UtcNow));

    // ---------- Lista ----------

    /// <param name="nueva">Id de oportunidad para abrir una cotización nueva ligada a ella.</param>
    /// <param name="abrir">Id de cotización para abrirla al cargar.</param>
    public async Task OnGetAsync(int? nueva, int? abrir)
    {
        if (nueva is not null && PuedeGestionar)
        {
            TempData["GAS.AbrirModal"] = Url.Page("Index", "Nuevo", new { oportunidadId = nueva });
        }
        else if (abrir is not null)
        {
            TempData["GAS.AbrirModal"] = Url.Page("Index", "Editar", new { id = abrir });
        }
        await CargarAsync();
    }

    // ---------- Modales ----------

    public async Task<IActionResult> OnGetNuevoAsync(int? oportunidadId, int? clienteId)
    {
        if (!PuedeGestionar) return Forbid();
        var input = new CotizacionInput
        {
            FechaEmision = Hoy,
            ClienteId = clienteId,
            ResponsableId = User.UsuarioId(),
            Condiciones = TextosCotizacion.CondicionesPorDefecto
        };
        if (oportunidadId is not null)
        {
            var o = await _db.Oportunidades.AsNoTracking().FirstOrDefaultAsync(x => x.Id == oportunidadId);
            if (o is not null)
            {
                input.OportunidadId = o.Id;
                input.ClienteId = o.ClienteId;
                input.Titulo = o.Titulo;
                input.Moneda = o.Moneda;
            }
        }
        return Partial("_CotizacionModal", await ModalAsync(null, input));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id)
    {
        var c = await _db.Cotizaciones.AsNoTracking().Include(x => x.Lineas).Include(x => x.Envios)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        return Partial("_CotizacionModal", await ModalAsync(c, CotizacionInput.Desde(c)));
    }

    public async Task<IActionResult> OnGetEnviarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var c = await CargarCompletaAsync(id);
        if (c is null) return NotFound();
        return Partial("_EnviarModal", ModalEnvio(c, null));
    }

    public async Task<IActionResult> OnGetPdfAsync(int id)
    {
        var c = await CargarCompletaAsync(id);
        if (c is null) return NotFound();
        var bytes = _pdf.Generar(c, await EmpresaAsync());
        Response.Headers.ContentDisposition = $"inline; filename=\"{GeneradorPdfCotizacion.NombreArchivo(c)}\"";
        return File(bytes, "application/pdf");
    }

    // ---------- Crear y editar ----------

    public async Task<IActionResult> OnPostCrearAsync([Bind(Prefix = "Input")] CotizacionInput input)
    {
        if (!PuedeGestionar) return Forbid();
        await ValidarAsync(input);
        if (ModelState.IsValid)
        {
            var c = new Cotizacion { Folio = await SiguienteFolioAsync(input.FechaEmision.Year), Version = 1 };
            input.AplicarA(c);
            _db.Cotizaciones.Add(c);
            await _db.GuardarConFolioAsync(async () => c.Folio = await SiguienteFolioAsync(input.FechaEmision.Year));
            _contadores.Invalidar();
            this.ToastSuccess($"Cotización {c.FolioCompleto} creada como borrador.");
            TempData["GAS.AbrirModal"] = Url.Page("Index", "Editar", new { id = c.Id });
            return Volver();
        }
        await CargarAsync();
        Modal = await ModalAsync(null, input);
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync(int id, [Bind(Prefix = "Input")] CotizacionInput input)
    {
        if (!PuedeGestionar) return Forbid();
        var c = await _db.Cotizaciones.Include(x => x.Lineas).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (!c.Editable)
        {
            this.ToastWarning("Solo se pueden editar borradores. Crea una nueva versión para cambiarla.");
            return Volver();
        }
        await ValidarAsync(input);
        if (ModelState.IsValid)
        {
            input.AplicarA(c);
            await _db.SaveChangesAsync();
            this.ToastSuccess($"Cotización {c.FolioCompleto} guardada.");
            return Volver();
        }
        await CargarAsync();
        Modal = await ModalAsync(c, input);
        return Page();
    }

    // ---------- Ciclo de vida ----------

    public async Task<IActionResult> OnPostNuevaVersionAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var origen = await _db.Cotizaciones.Include(x => x.Lineas).FirstOrDefaultAsync(x => x.Id == id);
        if (origen is null) return NotFound();
        if (origen.Estado != EstadoCotizacion.Aprobada
            && await _db.Cotizaciones.AnyAsync(x => x.Folio == origen.Folio && x.Estado == EstadoCotizacion.Aprobada))
        {
            this.ToastWarning($"El folio {origen.Folio} ya tiene una versión aprobada; no se pueden crear más versiones.");
            return Volver();
        }
        if (origen.Estado is EstadoCotizacion.Borrador or EstadoCotizacion.Aprobada)
        {
            this.ToastWarning(origen.Estado == EstadoCotizacion.Borrador
                ? "Un borrador se edita directamente; no necesita nueva versión."
                : "Una cotización aprobada no se modifica.");
            return Volver();
        }

        var version = await _db.Cotizaciones.Where(x => x.Folio == origen.Folio).MaxAsync(x => x.Version) + 1;
        var nueva = new Cotizacion
        {
            Folio = origen.Folio, Version = version, Titulo = origen.Titulo, ClienteId = origen.ClienteId,
            OportunidadId = origen.OportunidadId, Moneda = origen.Moneda, FechaEmision = Hoy,
            VigenciaDias = origen.VigenciaDias, TasaIva = origen.TasaIva, CondicionPago = origen.CondicionPago,
            AnticipoPorcentaje = origen.AnticipoPorcentaje, Condiciones = origen.Condiciones,
            NotasInternas = origen.NotasInternas, ResponsableId = User.UsuarioId() ?? origen.ResponsableId,
            Lineas = origen.Lineas.OrderBy(l => l.Orden).Select((l, i) => new CotizacionLinea
            {
                Orden = i, ServicioComercialId = l.ServicioComercialId, Concepto = l.Concepto, Descripcion = l.Descripcion,
                Cantidad = l.Cantidad, Unidad = l.Unidad, PrecioUnitario = l.PrecioUnitario,
                DescuentoPorcentaje = l.DescuentoPorcentaje, CostoUnitario = l.CostoUnitario
            }).ToList()
        };
        nueva.Recalcular();
        if (origen.Estado == EstadoCotizacion.Enviada) origen.Estado = EstadoCotizacion.Reemplazada;

        _db.Cotizaciones.Add(nueva);
        await _db.SaveChangesAsync();
        _contadores.Invalidar();
        this.ToastSuccess($"Versión {nueva.FolioCompleto} creada como borrador.");
        TempData["GAS.AbrirModal"] = Url.Page("Index", "Editar", new { id = nueva.Id });
        return Volver();
    }

    public async Task<IActionResult> OnPostAprobarAsync(int id, TipoTrabajo tipo)
    {
        if (!PuedeGestionar) return Forbid();
        var c = await _db.Cotizaciones.Include(x => x.Cliente).Include(x => x.Oportunidad).Include(x => x.Lineas).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (c.Estado is not (EstadoCotizacion.Borrador or EstadoCotizacion.Enviada))
        {
            this.ToastWarning("Solo se puede aprobar un borrador o una cotización enviada.");
            return Volver();
        }
        if (c.Vencida(Hoy))
        {
            this.ToastWarning("La cotización está vencida. Crea una nueva versión con fecha actual antes de aprobarla.");
            return Volver();
        }
        if (await _db.Cotizaciones.AnyAsync(x => x.Folio == c.Folio && x.Id != c.Id && x.Estado == EstadoCotizacion.Aprobada))
        {
            this.ToastWarning($"El folio {c.Folio} ya tiene otra versión aprobada.");
            return Volver();
        }

        c.Estado = EstadoCotizacion.Aprobada;
        c.FechaAprobacionUtc = DateTime.UtcNow;
        c.TipoTrabajo = tipo;

        // Otras versiones abiertas del mismo folio quedan reemplazadas.
        var otras = await _db.Cotizaciones
            .Where(x => x.Folio == c.Folio && x.Id != c.Id && (x.Estado == EstadoCotizacion.Borrador || x.Estado == EstadoCotizacion.Enviada))
            .ToListAsync();
        otras.ForEach(x => x.Estado = EstadoCotizacion.Reemplazada);

        if (c.Cliente.Estado == EstadoCliente.Prospecto) c.Cliente.Estado = EstadoCliente.Activo;
        if (c.Oportunidad is { Abierta: true } op)
        {
            op.Etapa = EtapaOportunidad.Ganada;
            op.Probabilidad = 100;
            op.FechaCierreUtc = DateTime.UtcNow;
        }

        // Todo trabajo aprobado se registra en Proyectos.
        var (trabajo, cobros) = await _trabajos.CrearDesdeCotizacionAsync(c);
        try
        {
            await _db.GuardarConFolioAsync(async () => trabajo.Folio = await _trabajos.SiguienteFolioAsync(ServicioTrabajos.Hoy.Year));
        }
        catch (DbUpdateException)
        {
            // Doble clic o dos personas aprobando al mismo tiempo: el índice único lo impide.
            this.ToastWarning("La cotización ya se estaba aprobando. Revisa el trabajo creado.");
            return Volver();
        }
        _contadores.Invalidar();
        this.ToastSuccess($"Cotización {c.FolioCompleto} aprobada. Se creó el trabajo {trabajo.Folio}.");
        if (cobros > 0) this.ToastInfo(cobros == 1 ? "Se registró 1 cobro esperado en Finanzas." : $"Se registraron {cobros} cobros esperados en Finanzas.");
        return RedirectToPage("/Proyectos/Detalle/Index", new { id = trabajo.Id });
    }

    /// <summary>Para cotizaciones aprobadas antes de existir Proyectos.</summary>
    public async Task<IActionResult> OnPostCrearTrabajoAsync(int id)
    {
        if (!User.TienePermiso(Permisos.Proyectos.Gestionar)) return Forbid();
        var c = await _db.Cotizaciones.Include(x => x.Lineas).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (c.Estado != EstadoCotizacion.Aprobada)
        {
            this.ToastWarning("Solo las cotizaciones aprobadas se convierten en trabajo.");
            return Volver();
        }
        var existente = await _db.Trabajos.FirstOrDefaultAsync(t => t.CotizacionId == id);
        if (existente is not null) return RedirectToPage("/Proyectos/Detalle/Index", new { id = existente.Id });

        var (trabajo, cobros) = await _trabajos.CrearDesdeCotizacionAsync(c);
        await _db.GuardarConFolioAsync(async () => trabajo.Folio = await _trabajos.SiguienteFolioAsync(ServicioTrabajos.Hoy.Year));
        _contadores.Invalidar();
        this.ToastSuccess($"Se creó el trabajo {trabajo.Folio} desde {c.FolioCompleto}.");
        if (cobros > 0) this.ToastInfo(cobros == 1 ? "Se registró 1 cobro esperado en Finanzas." : $"Se registraron {cobros} cobros esperados en Finanzas.");
        return RedirectToPage("/Proyectos/Detalle/Index", new { id = trabajo.Id });
    }

    public async Task<IActionResult> OnPostRechazarAsync(int id) =>
        await CambiarEstadoAsync(id, EstadoCotizacion.Rechazada, [EstadoCotizacion.Enviada], "rechazada");

    public async Task<IActionResult> OnPostCancelarAsync(int id) =>
        await CambiarEstadoAsync(id, EstadoCotizacion.Cancelada, [EstadoCotizacion.Borrador, EstadoCotizacion.Enviada], "cancelada");

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var c = await _db.Cotizaciones.Include(x => x.Envios).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (c.Estado != EstadoCotizacion.Borrador || c.Envios.Count > 0)
        {
            this.ToastWarning("Solo se eliminan borradores que nunca se enviaron. Para las demás, usa Cancelar.");
            return Volver();
        }
        _db.Cotizaciones.Remove(c);
        await _db.SaveChangesAsync();
        _contadores.Invalidar();
        this.ToastSuccess($"Borrador {c.FolioCompleto} eliminado.");
        return Volver();
    }

    // ---------- Envío por correo ----------

    public async Task<IActionResult> OnPostEnviarAsync(int id, [Bind(Prefix = "Envio")] EnvioInput envio)
    {
        if (!PuedeGestionar) return Forbid();
        var c = await CargarCompletaAsync(id, seguimiento: true);
        if (c is null) return NotFound();

        if (c.Estado is not (EstadoCotizacion.Borrador or EstadoCotizacion.Enviada))
        {
            this.ToastWarning("Solo se envían borradores o cotizaciones ya enviadas.");
            return Volver();
        }
        if (c.Lineas.Count == 0) ModelState.AddModelError(string.Empty, "La cotización no tiene conceptos.");
        var para = Correos(envio.Para, "Para");
        var copia = Correos(envio.Copia, "Copia");
        if (!_correo.Configurado) ModelState.AddModelError(string.Empty, "El envío de correo no está configurado todavía.");

        if (!ModelState.IsValid)
        {
            await CargarAsync();
            ViewData["ModalEnvio"] = ModalEnvio(c, envio);
            return Page();
        }

        var pdf = _pdf.Generar(c, await EmpresaAsync());
        var resultado = await _correo.EnviarAsync(new MensajeCorreo(
            para, copia, envio.Asunto.Trim(),
            await _plantilla.HtmlAsync(CorreoCotizacion.Contenido(c, envio.Mensaje)), CorreoCotizacion.Texto(c, envio.Mensaje),
            [new AdjuntoCorreo(GeneradorPdfCotizacion.NombreArchivo(c), pdf)],
            User.Identity?.Name));

        c.Envios.Add(new CotizacionEnvio
        {
            Para = string.Join(", ", para),
            Copia = copia.Count == 0 ? null : string.Join(", ", copia),
            Asunto = envio.Asunto.Trim(),
            EnviadoPor = User.Identity?.Name,
            Exito = resultado.Exito,
            IdProveedor = resultado.Id,
            Error = resultado.Error
        });

        if (resultado.Exito)
        {
            c.FechaEnvioUtc = DateTime.UtcNow;
            if (c.Estado == EstadoCotizacion.Borrador) c.Estado = EstadoCotizacion.Enviada;
            if (c.Oportunidad is { Abierta: true } op && op.Etapa < EtapaOportunidad.PropuestaEnviada)
            {
                op.Etapa = EtapaOportunidad.PropuestaEnviada;
                op.Probabilidad = Math.Max(op.Probabilidad, Oportunidad.ProbabilidadSugerida(EtapaOportunidad.PropuestaEnviada));
            }
        }
        await _db.SaveChangesAsync();

        if (resultado.Exito)
        {
            await _auditoria.RegistrarAsync(AccionesAuditoria.CotizacionEnviada,
                detalle: $"{c.FolioCompleto} a {string.Join(", ", para)}", entidad: "Cotización", entidadId: c.Id.ToString());
            _contadores.Invalidar();
            this.ToastSuccess($"Cotización {c.FolioCompleto} enviada a {string.Join(", ", para)}.");
        }
        else
        {
            this.ToastError($"No se pudo enviar: {resultado.Error}");
        }
        return Volver();
    }

    // ---------- Apoyo ----------

    private Task<Empresa?> EmpresaAsync() => _db.Empresas.AsNoTracking().OrderBy(e => e.Id).FirstOrDefaultAsync();

    private IActionResult Volver() => RedirectToPage("Index", new { estado = Opciones.Limpio(Estado), q = Opciones.Limpio(Q) });

    private async Task<IActionResult> CambiarEstadoAsync(int id, EstadoCotizacion nuevo, EstadoCotizacion[] permitidos, string texto)
    {
        if (!PuedeGestionar) return Forbid();
        var c = await _db.Cotizaciones.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (!permitidos.Contains(c.Estado))
        {
            this.ToastWarning($"Esta cotización no se puede marcar como {texto}.");
            return Volver();
        }
        c.Estado = nuevo;
        await _db.SaveChangesAsync();
        _contadores.Invalidar();
        this.ToastSuccess($"Cotización {c.FolioCompleto} {texto}.");
        return Volver();
    }

    private List<string> Correos(string? texto, string campo)
    {
        var lista = (texto ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var validador = new EmailAddressAttribute();
        foreach (var correo in lista.Where(c => !validador.IsValid(c) || !MailAddress.TryCreate(c, out _)))
        {
            ModelState.AddModelError(string.Empty, $"{campo}: \"{correo}\" no es un correo válido.");
        }
        if (lista.Count > 5) ModelState.AddModelError(string.Empty, $"{campo}: máximo 5 correos.");
        return lista;
    }

    private async Task ValidarAsync(CotizacionInput input)
    {
        if (input.ClienteId is null || !await _db.Clientes.AnyAsync(c => c.Id == input.ClienteId))
        {
            ModelState.AddModelError(string.Empty, "Elige el cliente de la cotización.");
        }
        if (input.OportunidadId is not null
            && !await _db.Oportunidades.AnyAsync(o => o.Id == input.OportunidadId && o.ClienteId == input.ClienteId))
        {
            ModelState.AddModelError(string.Empty, "La oportunidad elegida no pertenece a ese cliente.");
        }
        if (input.Lineas.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Agrega al menos un concepto.");
        }
        if (!CotizacionInput.Monedas.Contains(input.Moneda))
        {
            ModelState.AddModelError(string.Empty, "Elige una moneda válida.");
        }
        if (input.FechaEmision == default) input.FechaEmision = Hoy;
    }

    private async Task<string> SiguienteFolioAsync(int anio)
    {
        var prefijo = $"COT-{anio}-";
        var folios = await _db.Cotizaciones.Where(c => c.Folio.StartsWith(prefijo)).Select(c => c.Folio).Distinct().ToListAsync();
        var max = folios.Select(f => int.TryParse(f[prefijo.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{prefijo}{max + 1:0000}";
    }

    private Task<Cotizacion?> CargarCompletaAsync(int id, bool seguimiento = false)
    {
        var q = _db.Cotizaciones.Include(x => x.Cliente).Include(x => x.Responsable).Include(x => x.Lineas)
            .Include(x => x.Envios).Include(x => x.Oportunidad).AsQueryable();
        if (!seguimiento) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(x => x.Id == id);
    }

    private EnvioModalVm ModalEnvio(Cotizacion c, EnvioInput? envio) => new()
    {
        Id = c.Id,
        Folio = c.FolioCompleto,
        Titulo = c.Titulo,
        Cliente = c.Cliente.NombreVisible,
        Total = c.Total,
        Moneda = c.Moneda,
        Input = envio ?? new EnvioInput
        {
            Para = c.Cliente.ContactoEmail ?? "",
            Asunto = CorreoCotizacion.AsuntoPorDefecto(c),
            Mensaje = CorreoCotizacion.MensajePorDefecto(c, User.NombreVisible())
        },
        Configurado = _correo.Configurado,
        Remitente = _correo.Remitente,
        Envios = c.Envios.OrderByDescending(e => e.FechaUtc).ToList(),
        Filtro = Estado,
        Q = Q
    };

    private async Task<CotizacionModalVm> ModalAsync(Cotizacion? c, CotizacionInput input) => new()
    {
        Id = c?.Id,
        Folio = c?.FolioCompleto,
        Estado = c?.Estado ?? EstadoCotizacion.Borrador,
        Vencida = c?.Vencida(Hoy) ?? false,
        Input = input,
        Clientes = await _db.Clientes.AsNoTracking().OrderBy(x => x.NombreComercial ?? x.Nombre)
            .Select(x => new OpcionCliente(x.Id, x.NombreComercial ?? x.Nombre, x.Estado)).ToListAsync(),
        Oportunidades = await _db.Oportunidades.AsNoTracking()
            .Where(o => (o.Etapa != EtapaOportunidad.Ganada && o.Etapa != EtapaOportunidad.Perdida) || o.Id == input.OportunidadId)
            .OrderBy(o => o.Titulo)
            .Select(o => new OpcionOportunidad(o.Id, o.ClienteId, o.Titulo, o.Cliente.NombreComercial ?? o.Cliente.Nombre)).ToListAsync(),
        Responsables = await Opciones.ResponsablesAsync(_db, input.ResponsableId),
        Catalogo = await _db.ServiciosComerciales.AsNoTracking().Where(s => s.Estado != EstadoCatalogo.Inactivo)
            .OrderBy(s => s.Categoria).ThenBy(s => s.Nombre)
            .Select(s => new ItemCatalogo(s.Id, s.Nombre, s.Descripcion, s.PrecioBase, s.Moneda, s.Unidad, s.CostoEstimado, s.Categoria.ToString()))
            .ToListAsync(),
        Envios = c?.Envios.OrderByDescending(e => e.FechaUtc).ToList() ?? [],
        PuedeGestionar = PuedeGestionar,
        PuedeEliminar = c is not null && c.Estado == EstadoCotizacion.Borrador && c.Envios.Count == 0,
        Trabajo = c is null ? null : await _db.Trabajos.AsNoTracking().Where(t => t.CotizacionId == c.Id)
            .Select(t => new OpcionOportunidad(t.Id, 0, t.Folio, t.Nombre)).FirstOrDefaultAsync(),
        PuedeCrearTrabajo = User.TienePermiso(Permisos.Proyectos.Gestionar),
        TipoTrabajo = c?.TipoTrabajo,
        FechaAprobacionUtc = c?.FechaAprobacionUtc,
        CreadoUtc = c?.CreadoUtc,
        CreadoPor = c?.CreadoPor,
        ModificadoUtc = c?.ModificadoUtc,
        ModificadoPor = c?.ModificadoPor,
        Filtro = Estado,
        Q = Q
    };

    public bool HayMas { get; private set; }

    private async Task CargarAsync()
    {
        const int limite = 300;
        var hoy = Hoy;

        // Indicadores con una proyección ligera (sin traer las cotizaciones completas).
        var ligeras = await _db.Cotizaciones.AsNoTracking()
            .Select(c => new { c.Estado, c.FechaEmision, c.VigenciaDias, c.FechaAprobacionUtc, c.Moneda, c.Total }).ToListAsync();
        bool EsVencida(EstadoCotizacion e, DateOnly emision, int vigencia) => e == EstadoCotizacion.Enviada && emision.AddDays(vigencia) < hoy;
        string ClaveLigera(EstadoCotizacion e, DateOnly emision, int vigencia) => EsVencida(e, emision, vigencia) ? FiltroVencidas : e.ToString();
        Total = ligeras.Count;
        PorEstado = ligeras.GroupBy(x => ClaveLigera(x.Estado, x.FechaEmision, x.VigenciaDias)).ToDictionary(g => g.Key, g => g.Count());
        Abiertas = ligeras.Count(x => x.Estado == EstadoCotizacion.Enviada && !EsVencida(x.Estado, x.FechaEmision, x.VigenciaDias));
        PorVencer = ligeras.Count(x => x.Estado == EstadoCotizacion.Enviada && x.FechaEmision.AddDays(x.VigenciaDias).DayNumber - hoy.DayNumber is >= 0 and <= DiasPorVencer);
        var inicioMes = Fechas.ALocalUtc(new DateTime(hoy.Year, hoy.Month, 1));
        AprobadoMes = ligeras.Where(x => x.Estado == EstadoCotizacion.Aprobada && x.FechaAprobacionUtc >= inicioMes)
            .GroupBy(x => x.Moneda).ToDictionary(g => g.Key, g => g.Sum(x => x.Total));
        var aprobadas = ligeras.Count(x => x.Estado == EstadoCotizacion.Aprobada);
        var rechazadas = ligeras.Count(x => x.Estado == EstadoCotizacion.Rechazada);
        TasaAprobacion = aprobadas + rechazadas == 0 ? null : Math.Round((decimal)aprobadas / (aprobadas + rechazadas), 4);

        // Lista: filtrada en la base y con tope de filas.
        var q = _db.Cotizaciones.AsNoTracking().AsQueryable();
        if (Estado == FiltroVencidas)
            q = q.Where(c => c.Estado == EstadoCotizacion.Enviada && c.FechaEmision.AddDays(c.VigenciaDias) < hoy);
        else if (Enum.TryParse<EstadoCotizacion>(Estado, out var e))
            q = e == EstadoCotizacion.Enviada
                ? q.Where(c => c.Estado == e && c.FechaEmision.AddDays(c.VigenciaDias) >= hoy)
                : q.Where(c => c.Estado == e);
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var b = Q.Trim();
            q = q.Where(c => c.Folio.Contains(b) || c.Titulo.Contains(b) || c.Cliente.Nombre.Contains(b)
                             || (c.Cliente.NombreComercial != null && c.Cliente.NombreComercial.Contains(b)));
        }
        var datos = await q.OrderByDescending(c => c.Folio).ThenByDescending(c => c.Version).Take(limite + 1)
            .Select(c => new
            {
                Cotizacion = c,
                Cliente = c.Cliente.NombreComercial ?? c.Cliente.Nombre,
                Oportunidad = c.Oportunidad == null ? null : c.Oportunidad.Titulo,
                Responsable = c.Responsable == null ? null : (c.Responsable.NombreCompleto == "" ? c.Responsable.Email : c.Responsable.NombreCompleto)
            })
            .ToListAsync();
        HayMas = datos.Count > limite;
        Cotizaciones = datos.Take(limite).Select(d =>
        {
            var vencida = d.Cotizacion.Vencida(hoy);
            int? dias = d.Cotizacion.Estado is EstadoCotizacion.Enviada or EstadoCotizacion.Borrador
                ? d.Cotizacion.FechaVencimiento.DayNumber - hoy.DayNumber : null;
            return new Fila(d.Cotizacion, d.Cliente, d.Oportunidad, d.Responsable, vencida, dias);
        }).ToList();
    }
}
