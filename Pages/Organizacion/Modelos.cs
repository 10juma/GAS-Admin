using System.ComponentModel.DataAnnotations;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Catalogos;

namespace GAS.Admin.Pages.Organizacion;

public class PersonaInput
{
    public static readonly string[] Monedas = ["MXN", "USD"];

    [Required(ErrorMessage = "Ingresa el nombre de la persona.")]
    [StringLength(150, ErrorMessage = "El nombre no puede exceder 150 caracteres.")]
    public string NombreCompleto { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    [StringLength(256)]
    public string? Correo { get; set; }

    [RegularExpression(@"^[\d\s+()\-.]{7,30}$", ErrorMessage = "El teléfono solo puede tener números, espacios y + ( ) - .")]
    public string? Telefono { get; set; }

    public TipoPersona Tipo { get; set; } = TipoPersona.Empleado;
    public int? DepartamentoId { get; set; }
    public int? PuestoId { get; set; }
    public DateOnly? FechaIngreso { get; set; }
    public bool Activa { get; set; } = true;
    public string? UsuarioId { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "El costo por hora debe ser un número positivo.")]
    public decimal? CostoHora { get; set; }

    public string MonedaCosto { get; set; } = "MXN";

    [Range(1, 80, ErrorMessage = "Las horas por semana deben estar entre 1 y 80.")]
    public int? HorasSemana { get; set; }

    [StringLength(2000, ErrorMessage = "Las notas no pueden exceder 2000 caracteres.")]
    public string? Notas { get; set; }

    public static PersonaInput Desde(Persona p, bool conCosto) => new()
    {
        NombreCompleto = p.NombreCompleto, Correo = p.Correo, Telefono = p.Telefono, Tipo = p.Tipo,
        DepartamentoId = p.DepartamentoId, PuestoId = p.PuestoId, FechaIngreso = p.FechaIngreso, Activa = p.Activa,
        UsuarioId = p.UsuarioId, HorasSemana = p.HorasSemana, Notas = p.Notas,
        CostoHora = conCosto ? p.CostoHora : null, MonedaCosto = conCosto ? p.MonedaCosto : "MXN"
    };

    /// <param name="editarCosto">Si es falso, el costo actual se conserva aunque no venga en el formulario.</param>
    public void AplicarA(Persona p, bool editarCosto)
    {
        p.NombreCompleto = NombreCompleto.Trim();
        p.Correo = Opciones.Limpio(Correo);
        p.Telefono = Opciones.Limpio(Telefono);
        p.Tipo = Tipo;
        p.DepartamentoId = DepartamentoId;
        p.PuestoId = PuestoId;
        p.FechaIngreso = FechaIngreso;
        p.Activa = Activa;
        p.UsuarioId = Opciones.Limpio(UsuarioId);
        p.HorasSemana = HorasSemana;
        p.Notas = Opciones.Limpio(Notas);
        if (editarCosto)
        {
            p.CostoHora = CostoHora;
            p.MonedaCosto = MonedaCosto;
        }
    }
}

public sealed record OpcionDepartamento(int Id, string Nombre, string Color);
public sealed record OpcionPuesto(int Id, int DepartamentoId, string Nombre);
public sealed record OpcionUsuarioLibre(string Id, string Nombre, string Email);

public class PersonaModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public PersonaInput Input { get; init; } = new();
    public List<OpcionDepartamento> Departamentos { get; init; } = [];
    public List<OpcionPuesto> Puestos { get; init; } = [];
    public List<OpcionUsuarioLibre> Usuarios { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public bool VerCosto { get; init; }
    public bool EditarCosto { get; init; }
    public List<string> Responsabilidades { get; init; } = [];
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
    public string? Vista { get; init; }
}
