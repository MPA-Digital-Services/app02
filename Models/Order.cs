namespace NorthwindApi.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Entidad de dominio que representa una orden en la base de datos Northwind.
/// Esta clase mapea directamente a la tabla 'Orders' y contiene campos sensibles
/// que NO deben exponerse a través de la API (ej. InternalStaffNotes).
/// </summary>
[Table("Orders")]
public class Order
{
    [Key]
    [Column("OrderID")]
    public int OrderId { get; set; }

    [Column("CustomerID")]
    [StringLength(5)]
    public string? CustomerId { get; set; }

    [Column("EmployeeID")]
    public int? EmployeeId { get; set; }

    [Column("OrderDate")]
    public DateTime? OrderDate { get; set; }

    [Column("RequiredDate")]
    public DateTime? RequiredDate { get; set; }

    [Column("ShippedDate")]
    public DateTime? ShippedDate { get; set; }

    [Column("ShipVia")]
    public int? ShipVia { get; set; }

    [Column("Freight")]
    public decimal? Freight { get; set; }

    [Column("ShipName")]
    [StringLength(40)]
    public string? ShipName { get; set; }

    [Column("ShipAddress")]
    [StringLength(60)]
    public string? ShipAddress { get; set; }

    [Column("ShipCity")]
    [StringLength(15)]
    public string? ShipCity { get; set; }

    [Column("ShipRegion")]
    [StringLength(15)]
    public string? ShipRegion { get; set; }

    [Column("ShipPostalCode")]
    [StringLength(10)]
    public string? ShipPostalCode { get; set; }

    [Column("ShipCountry")]
    [StringLength(15)]
    public string? ShipCountry { get; set; }

    /// <summary>
    /// CAMPO CONFIDENCIAL: Notas internas del personal.
    /// NUNCA debe exponerse en DTOs de respuesta ni serializarse en APIs públicas.
    /// Su inclusión aquí es solo para persistencia en base de datos.
    /// </summary>
    [Column("InternalStaffNotes")]
    [StringLength(1000)]
    public string? InternalStaffNotes { get; set; }

    // Propiedades de navegación (opcional, para EF Core)
    // public virtual Customer? Customer { get; set; }
    // public virtual ICollection<OrderDetail>? OrderDetails { get; set; }
}