using System.ComponentModel.DataAnnotations;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Catalogos;

namespace GAS.Admin.Pages.Clientes;

public class ClienteInput
{
    [Required(ErrorMessage = "Ingresa el nombre o razón social.")]
    [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "El nombre comercial no puede exceder 200 caracteres.")]
    public string? NombreComercial { get; set; }

    [RegularExpression(@"^\s*[A-Za-zÑñ&]{3,4}\d{6}[A-Za-z0-9]{3}\s*$", ErrorMessage = "El RFC no tiene un formato válido (12 o 13 caracteres).")]
    public string? Rfc { get; set; }

    public EstadoCliente Estado { get; set; } = EstadoCliente.Prospecto;

    [StringLength(100)]
    public string? Ciudad { get; set; }

    [Url(ErrorMessage = "El sitio web debe ser una URL completa (https://…).")]
    [StringLength(300)]
    public string? SitioWeb { get; set; }

    [StringLength(150)]
    public string? ContactoNombre { get; set; }

    [EmailAddress(ErrorMessage = "El correo del contacto no es válido.")]
    [StringLength(256)]
    public string? ContactoEmail { get; set; }

    [RegularExpression(@"^[\d\s+()\-.]{7,30}$", ErrorMessage = "El teléfono solo puede tener números, espacios y + ( ) - .")]
    public string? ContactoTelefono { get; set; }

    public string? ResponsableId { get; set; }

    [StringLength(2000, ErrorMessage = "Las notas no pueden exceder 2000 caracteres.")]
    public string? Notas { get; set; }

    public List<ProductoDelCliente> Productos { get; set; } = [];

    public static ClienteInput Desde(Cliente c, IEnumerable<Producto> productos) => new()
    {
        Nombre = c.Nombre, NombreComercial = c.NombreComercial, Rfc = c.Rfc, Estado = c.Estado,
        Ciudad = c.Ciudad, SitioWeb = c.SitioWeb, ContactoNombre = c.ContactoNombre,
        ContactoEmail = c.ContactoEmail, ContactoTelefono = c.ContactoTelefono,
        ResponsableId = c.ResponsableId, Notas = c.Notas,
        Productos = productos.Select(p =>
        {
            var r = c.Productos.FirstOrDefault(x => x.ProductoId == p.Id);
            return new ProductoDelCliente { ProductoId = p.Id, Estado = r?.Estado, Desde = r?.Desde };
        }).ToList()
    };

    public void AplicarA(Cliente c)
    {
        c.Nombre = Nombre.Trim();
        c.NombreComercial = Opciones.Limpio(NombreComercial);
        c.Rfc = Opciones.Limpio(Rfc)?.ToUpperInvariant();
        c.Estado = Estado;
        c.Ciudad = Opciones.Limpio(Ciudad);
        c.SitioWeb = Opciones.Limpio(SitioWeb);
        c.ContactoNombre = Opciones.Limpio(ContactoNombre);
        c.ContactoEmail = Opciones.Limpio(ContactoEmail);
        c.ContactoTelefono = Opciones.Limpio(ContactoTelefono);
        c.ResponsableId = Opciones.Limpio(ResponsableId);
        c.Notas = Opciones.Limpio(Notas);
    }
}

public class ProductoDelCliente
{
    public int ProductoId { get; set; }
    /// <summary>Nulo = no usa el producto.</summary>
    public EstadoClienteProducto? Estado { get; set; }
    public DateOnly? Desde { get; set; }
}

public class ClienteModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public ClienteInput Input { get; init; } = new();
    public List<Producto> ProductosDisponibles { get; init; } = [];
    public List<OpcionUsuario> Responsables { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
    public string? Filtro { get; init; }
    public int? FiltroProducto { get; init; }
    public string? Q { get; init; }
}
