using System.ComponentModel.DataAnnotations;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Catalogos;

namespace GAS.Admin.Pages.Organizacion.Departamentos;

public class DepartamentoInput
{
    public static readonly string[] Colores = ["c1", "c2", "c3", "c4", "c5", "c6"];

    [Required(ErrorMessage = "Ingresa el nombre del departamento.")]
    [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres.")]
    public string? Descripcion { get; set; }

    public string Color { get; set; } = "c1";
    public int? ResponsableId { get; set; }

    [Range(0, 999)]
    public int Orden { get; set; }

    public static DepartamentoInput Desde(Departamento d) => new()
    {
        Nombre = d.Nombre, Descripcion = d.Descripcion, Color = d.Color, ResponsableId = d.ResponsableId, Orden = d.Orden
    };

    public void AplicarA(Departamento d)
    {
        d.Nombre = Nombre.Trim();
        d.Descripcion = Opciones.Limpio(Descripcion);
        d.Color = Colores.Contains(Color) ? Color : "c1";
        d.ResponsableId = ResponsableId;
        d.Orden = Orden;
    }
}

public class PuestoInput
{
    [Required(ErrorMessage = "Ingresa el nombre del puesto.")]
    [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    public int? DepartamentoId { get; set; }

    [StringLength(1000, ErrorMessage = "Las responsabilidades no pueden exceder 1000 caracteres.")]
    public string? Descripcion { get; set; }
}

public sealed record OpcionPersona(int Id, string Nombre, int? DepartamentoId);

public class DepartamentoModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public DepartamentoInput Input { get; init; } = new();
    public List<OpcionPersona> Personas { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public int Integrantes { get; init; }
    public int Puestos { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
}

public class PuestoModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public PuestoInput Input { get; init; } = new();
    public List<GAS.Admin.Pages.Organizacion.OpcionDepartamento> Departamentos { get; init; } = [];
    public List<string> Ocupantes { get; init; } = [];
    public bool PuedeGestionar { get; init; }
}
