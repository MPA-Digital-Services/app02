namespace NorthwindApi.DTOs;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// DTO de entrada (Request) para la creación/actualización de órdenes.
/// Implementa validaciones estrictas via Data Annotations para prevenir:
/// - Mass Assignment (solo propiedades permitidas aquí son vinculables)
/// - Inyección de datos maliciosos (validación de longitud, rango, formato)
/// - Payloads corruptos o excesivamente grandes
/// </summary>
public class OrderRequestDto
{
    /// <summary>
    /// ID del cliente (formato Northwind: 5 caracteres, ej. "ALFKI").
    /// Obligatorio para asociar la orden a un cliente existente.
    /// </summary>
    [Required(ErrorMessage = "El CustomerId es obligatorio.")]
    [StringLength(5, MinimumLength = 5, ErrorMessage = "El CustomerId debe tener exactamente 5 caracteres.")]
    [RegularExpression(@"^[A-Z0-9]{5}$", ErrorMessage = "El CustomerId debe ser alfanumérico de 5 caracteres en mayúsculas.")]
    public required string CustomerId { get; set; }

    /// <summary>
    /// ID del empleado que gestiona la orden.
    /// Debe ser un valor positivo y referenciar un empleado existente.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "El EmployeeId debe ser un valor positivo.")]
    public int? EmployeeId { get; set; }

    /// <summary>
    /// Fecha de la orden. No puede ser futura.
    /// </summary>
    [Required(ErrorMessage = "La fecha de orden es obligatoria.")]
    [DataType(DataType.Date)]
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha requerida de entrega. Debe ser posterior a OrderDate.
    /// </summary>
    [DataType(DataType.Date)]
    public DateTime? RequiredDate { get; set; }

    /// <summary>
    /// Fecha de envío real. No puede ser anterior a OrderDate.
    /// </summary>
    [DataType(DataType.Date)]
    public DateTime? ShippedDate { get; set; }

    /// <summary>
    /// Transportista (ShipVia). Referencia a tabla Shippers.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "El ShipVia debe referenciar un transportista válido.")]
    public int? ShipVia { get; set; }

    /// <summary>
    /// Costo de flete. Valor monetario no negativo con precisión de 4 decimales.
    /// </summary>
    [Range(0, 999999.9999, ErrorMessage = "El flete debe ser un valor no negativo.")]
    [DataType(DataType.Currency)]
    public decimal? Freight { get; set; }

    /// <summary>
    /// Nombre del destinatario. Máximo 40 caracteres.
    /// </summary>
    [StringLength(40, ErrorMessage = "El nombre de envío no puede exceder 40 caracteres.")]
    public string? ShipName { get; set; }

    /// <summary>
    /// Dirección de envío. Máximo 60 caracteres.
    /// </summary>
    [StringLength(60, ErrorMessage = "La dirección de envío no puede exceder 60 caracteres.")]
    public string? ShipAddress { get; set; }

    /// <summary>
    /// Ciudad de envío. Máximo 15 caracteres.
    /// </summary>
    [StringLength(15, ErrorMessage = "La ciudad de envío no puede exceder 15 caracteres.")]
    public string? ShipCity { get; set; }

    /// <summary>
    /// Región/Estado de envío. Máximo 15 caracteres.
    /// </summary>
    [StringLength(15, ErrorMessage = "La región de envío no puede exceder 15 caracteres.")]
    public string? ShipRegion { get; set; }

    /// <summary>
    /// Código postal de envío. Máximo 10 caracteres.
    /// </summary>
    [StringLength(10, ErrorMessage = "El código postal no puede exceder 10 caracteres.")]
    public string? ShipPostalCode { get; set; }

    /// <summary>
    /// País de envío. Máximo 15 caracteres.
    /// </summary>
    [StringLength(15, ErrorMessage = "El país de envío no puede exceder 15 caracteres.")]
    public string? ShipCountry { get; set; }
}