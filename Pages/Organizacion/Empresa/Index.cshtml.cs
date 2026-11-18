using System.ComponentModel.DataAnnotations;
using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using EmpresaEntidad = GAS.Admin.Models.Entities.Empresa;

namespace GAS.Admin.Pages.Organizacion.Empresa;

public class EmpresaInput
{
    [StringLength(200)] public string? RazonSocial { get; set; }

    [Required(ErrorMessage = "Ingresa el nombre comercial.")]
    [StringLength(200)] public string NombreComercial { get; set; } = string.Empty;

    [RegularExpression(@"^\s*[A-Za-zÑñ&]{3,4}\d{6}[A-Za-z0-9]{3}\s*$", ErrorMessage = "El RFC no tiene un formato válido (12 o 13 caracteres).")]
    public string? Rfc { get; set; }

    [StringLength(150)] public string? RegimenFiscal { get; set; }
    [StringLength(500)] public string? Domicilio { get; set; }

    [RegularExpression(@"^\d{5}$", ErrorMessage = "El código postal debe tener 5 dígitos.")]
    public string? CodigoPostal { get; set; }

    [RegularExpression(@"^[\d\s+()\-.]{7,30}$", ErrorMessage = "El teléfono solo puede tener números, espacios y + ( ) - .")]
    public string? Telefono { get; set; }

    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    [StringLength(256)] public string? Correo { get; set; }

    [Url(ErrorMessage = "El sitio web debe ser una URL completa (https://…).")]
    [StringLength(300)] public string? SitioWeb { get; set; }

    [StringLength(200)] public string? Lema { get; set; }
}

[Authorize(Policy = Permisos.Organizacion.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;

    public IndexModel(GasDbContext db) => _db = db;

    public EmpresaEntidad Empresa { get; private set; } = new();
    public bool PuedeGestionar => User.TienePermiso(Permisos.Organizacion.Gestionar);
    public EmpresaInput? Modal { get; private set; }

    public async Task OnGetAsync() => Empresa = await ObtenerAsync();

    public async Task<IActionResult> OnGetEditarAsync()
    {
        if (!PuedeGestionar) return Forbid();
        var e = await ObtenerAsync();
        return Partial("_EmpresaModal", Desde(e));
    }

    public async Task<IActionResult> OnPostGuardarAsync([Bind(Prefix = "Input")] EmpresaInput input)
    {
        if (!PuedeGestionar) return Forbid();
        var e = await ObtenerAsync(seguimiento: true);
        if (!ModelState.IsValid)
        {
            Empresa = e;
            Modal = input;
            return Page();
        }

        e.RazonSocial = Opciones.Limpio(input.RazonSocial);
        e.NombreComercial = input.NombreComercial.Trim();
        e.Rfc = Opciones.Limpio(input.Rfc)?.ToUpperInvariant();
        e.RegimenFiscal = Opciones.Limpio(input.RegimenFiscal);
        e.Domicilio = Opciones.Limpio(input.Domicilio);
        e.CodigoPostal = Opciones.Limpio(input.CodigoPostal);
        e.Telefono = Opciones.Limpio(input.Telefono);
        e.Correo = Opciones.Limpio(input.Correo);
        e.SitioWeb = Opciones.Limpio(input.SitioWeb);
        e.Lema = Opciones.Limpio(input.Lema);
        if (_db.Entry(e).State == EntityState.Detached) _db.Empresas.Add(e);
        await _db.SaveChangesAsync();
        this.ToastSuccess("Datos de la empresa guardados. Se usarán en los PDF de cotización.");
        return RedirectToPage("Index");
    }

    private static EmpresaInput Desde(EmpresaEntidad e) => new()
    {
        RazonSocial = e.RazonSocial, NombreComercial = e.NombreComercial, Rfc = e.Rfc, RegimenFiscal = e.RegimenFiscal,
        Domicilio = e.Domicilio, CodigoPostal = e.CodigoPostal, Telefono = e.Telefono, Correo = e.Correo,
        SitioWeb = e.SitioWeb, Lema = e.Lema
    };

    private async Task<EmpresaEntidad> ObtenerAsync(bool seguimiento = false)
    {
        var q = seguimiento ? _db.Empresas : _db.Empresas.AsNoTracking();
        return await q.OrderBy(x => x.Id).FirstOrDefaultAsync() ?? new EmpresaEntidad();
    }
}
