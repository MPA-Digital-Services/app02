namespace NorthwindApi.DTOs;

using NorthwindApi.Models;

/// <summary>
/// DTO de salida (Response) para exponer datos de órdenes de forma segura.
/// DISEÑO DE SEGURIDAD:
/// - NUNCA expone 'InternalStaffNotes' (campo confidencial del modelo)
/// - NUNCA expone entidades crudas de EF Core (previene serialización circular y data leakage)
/// - Controla explícitamente qué campos llegan al cliente (Principio de Menor Privilegio)
/// - Inmutable (propiedades solo lectura via init) para prevenir modificaciones accidentales
/// </summary>
public class OrderResponseDto
{
    public int OrderId { get; init; }
    public string? CustomerId { get; init; }
    public int? EmployeeId { get; init; }
    public DateTime? OrderDate { get; init; }
    public DateTime? RequiredDate { get; init; }
    public DateTime? ShippedDate { get; init; }
    public int? ShipVia { get; init; }
    public decimal? Freight { get; init; }
    public string? ShipName { get; init; }
    public string? ShipAddress { get; init; }
    public string? ShipCity { get; init; }
    public string? ShipRegion { get; init; }
    public string? ShipPostalCode { get; init; }
    public string? ShipCountry { get; init; }

    /// <summary>
    /// Factoría para mapear de forma segura desde la entidad de dominio (Model) al DTO.
    /// Este es el ÚNICO punto donde se realiza el mapeo, garantizando que InternalStaffNotes
    /// nunca se filtre accidentalmente en la respuesta HTTP.
    /// </summary>
    /// <param name="order">Entidad de dominio Order (EF Core)</param>
    /// <returns>DTO seguro para serialización</returns>
    public static OrderResponseDto FromEntity(Order order)
    {
        return new OrderResponseDto
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            EmployeeId = order.EmployeeId,
            OrderDate = order.OrderDate,
            RequiredDate = order.RequiredDate,
            ShippedDate = order.ShippedDate,
            ShipVia = order.ShipVia,
            Freight = order.Freight,
            ShipName = order.ShipName,
            ShipAddress = order.ShipAddress,
            ShipCity = order.ShipCity,
            ShipRegion = order.ShipRegion,
            ShipPostalCode = order.ShipPostalCode,
            ShipCountry = order.ShipCountry
            // NOTA: InternalStaffNotes se omite INTENCIONALMENTE aquí
        };
    }
}