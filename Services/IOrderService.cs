namespace NorthwindApi.Services;

using NorthwindApi.DTOs;
using NorthwindApi.Models;

/// <summary>
/// Contrato del servicio de órdenes.
/// Abstrae la lógica de negocio y acceso a datos del controlador.
/// Permite testing fácil (mock) y separación de responsabilidades.
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Obtiene una orden por ID de forma segura.
    /// Usa AsNoTracking para consultas de solo lectura (rendimiento + seguridad).
    /// </summary>
    Task<OrderResponseDto?> GetOrderByIdAsync(int orderId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene órdenes paginadas con filtrado opcional.
    /// Implementa paginación obligatoria para prevenir DoS por result sets masivos.
    /// </summary>
    Task<(IEnumerable<OrderResponseDto> Orders, int TotalCount)> GetOrdersAsync(
        int pageNumber = 1,
        int pageSize = 20,
        string? customerId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default);

    /// <summary>
    /// Crea una nueva orden.
    /// Valida reglas de negocio y usa EF Core parametrizado (previene SQL Injection).
    /// </summary>
    Task<OrderResponseDto> CreateOrderAsync(OrderRequestDto requestDto, CancellationToken ct = default);

    /// <summary>
    /// Actualiza una orden existente.
    /// Validación de concurrencia optimista recomendada en producción.
    /// </summary>
    Task<OrderResponseDto?> UpdateOrderAsync(int orderId, OrderRequestDto requestDto, CancellationToken ct = default);

    /// <summary>
    /// Elimina una orden (soft delete recomendado en producción).
    /// </summary>
    Task<bool> DeleteOrderAsync(int orderId, CancellationToken ct = default);

    /// <summary>
    /// Verifica si una orden existe.
    /// </summary>
    Task<bool> OrderExistsAsync(int orderId, CancellationToken ct = default);
}