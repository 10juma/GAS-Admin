using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GAS.Admin.Models.Entities;

/// <summary>Datos de Global App Suite (un solo registro, Id = 1).</summary>
[DisplayName("Datos de la empresa")]
public class Empresa : EntidadBase
{
    [Display(Name = "Razón social")] public string? RazonSocial { get; set; }
    [Display(Name = "Nombre comercial")] public string NombreComercial { get; set; } = "Global App Suite";
    [Display(Name = "RFC")] public string? Rfc { get; set; }
    [Display(Name = "Régimen fiscal")] public string? RegimenFiscal { get; set; }
    [Display(Name = "Domicilio fiscal")] public string? Domicilio { get; set; }
    [Display(Name = "Código postal")] public string? CodigoPostal { get; set; }
    [Display(Name = "Teléfono")] public string? Telefono { get; set; }
    [Display(Name = "Correo")] public string? Correo { get; set; }
    [Display(Name = "Sitio web")] public string? SitioWeb { get; set; }
    [Display(Name = "Lema")] public string? Lema { get; set; } = "Software que resuelve problemas reales";

    public override string DescripcionAuditoria => NombreComercial;
}

[DisplayName("Departamento")]
public class Departamento : EntidadBase
{
    [Display(Name = "Nombre")] public string Nombre { get; set; } = string.Empty;
    [Display(Name = "Descripción")] public string? Descripcion { get; set; }
    /// <summary>Color del departamento (c1…c6, mismos tonos que los avatares).</summary>
    [Display(Name = "Color")] public string Color { get; set; } = "c1";
    [Display(Name = "Responsable")] public int? ResponsableId { get; set; }
    public Persona? Responsable { get; set; }
    [Display(Name = "Orden")] public int Orden { get; set; }

    public List<Puesto> Puestos { get; set; } = [];
    public List<Persona> Personas { get; set; } = [];

    public override string DescripcionAuditoria => Nombre;
}

[DisplayName("Puesto")]
public class Puesto : EntidadBase
{
    [Display(Name = "Nombre")] public string Nombre { get; set; } = string.Empty;
    [Display(Name = "Departamento")] public int DepartamentoId { get; set; }
    public Departamento Departamento { get; set; } = null!;
    [Display(Name = "Responsabilidades")] public string? Descripcion { get; set; }

    public override string DescripcionAuditoria => Nombre;
}

public enum TipoPersona { Socio, Empleado, Externo }

/// <summary>Persona del equipo. Puede o no tener usuario de GAS-Admin.</summary>
[DisplayName("Persona")]
public class Persona : EntidadBase
{
    [Display(Name = "Nombre")] public string NombreCompleto { get; set; } = string.Empty;
    [Display(Name = "Correo")] public string? Correo { get; set; }
    [Display(Name = "Teléfono")] public string? Telefono { get; set; }
    [Display(Name = "Tipo")] public TipoPersona Tipo { get; set; } = TipoPersona.Empleado;
    [Display(Name = "Departamento")] public int? DepartamentoId { get; set; }
    public Departamento? Departamento { get; set; }
    [Display(Name = "Puesto")] public int? PuestoId { get; set; }
    public Puesto? Puesto { get; set; }
    [Display(Name = "Fecha de ingreso")] public DateOnly? FechaIngreso { get; set; }
    [Display(Name = "Activa")] public bool Activa { get; set; } = true;
    [Display(Name = "Usuario de GAS-Admin")] public string? UsuarioId { get; set; }
    public ApplicationUser? Usuario { get; set; }
    /// <summary>Costo por hora de referencia. Solo visible con permiso de Finanzas.</summary>
    [Display(Name = "Costo por hora")] public decimal? CostoHora { get; set; }
    [Display(Name = "Moneda del costo")] public string MonedaCosto { get; set; } = "MXN";
    [Display(Name = "Horas por semana")] public int? HorasSemana { get; set; }
    [Display(Name = "Notas")] public string? Notas { get; set; }

    /// <summary>Costo mensual estimado (4.33 semanas por mes).</summary>
    [NotMapped]
    public decimal? CostoMensual => CostoHora is null || HorasSemana is null ? null : Math.Round(CostoHora.Value * HorasSemana.Value * 4.33m, 2);

    public override string DescripcionAuditoria => NombreCompleto;
}
