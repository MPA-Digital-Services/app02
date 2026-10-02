namespace NorthwindApi.Services;

using Microsoft.EntityFrameworkCore;
using NorthwindApi.Data;
using NorthwindApi.DTOs;
using NorthwindApi.Models;

/// <summary>
/// Implementación del servicio de órdenes.
/// SEGURIDAD:
/// - Usa exclusivamente LINQ/EF Core parametrizado (NO concatenación de strings SQL)
/// - AsNoTracking en consultas de lectura para evitar tracking innecesario
/// - Validación de entrada en capa de servicio (defensa en profundidad)
/// - Manejo de excepciones sin exponer detalles de BD
/// </summary>
public class OrderService : IOrderService
{
    private readonly NorthwindDbContext _context;
    private readonly ILogger<OrderService> _logger;

    public OrderService(NorthwindDbContext context, ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OrderResponseDto?> GetOrderByIdAsync(int orderId, CancellationToken ct = default)
    {
        // Validación de entrada (defensa en profundidad)
        if (orderId <= 0)
        {
            _logger.LogWarning("GetOrderByIdAsync llamado con OrderId inválido: {OrderId}", orderId);
            return null;
        }

        // CONSULTA SEGURA: EF Core genera SQL parametrizado automáticamente
        // AsNoTracking() evita overhead de tracking para consultas de solo lectura
        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == orderId, ct);

        return order is not null ? OrderResponseDto.FromEntity(order) : null;
    }

    public async Task<(IEnumerable<OrderResponseDto> Orders, int TotalCount)> GetOrdersAsync(
        int pageNumber = 1,
        int pageSize = 20,
        string? customerId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default)
    {
        // Validación y saneamiento de parámetros de paginación (previene DoS)
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100); // Límite máximo 100 registros por página

        var query = _context.Orders.AsNoTracking();

        // Filtros parametrizados (EF Core = SQL parametrizado = sin SQL Injection)
        if (!string.IsNullOrWhiteSpace(customerId))
        {
            // Validación adicional del formato CustomerId
            if (customerId.Length == 5 && customerId.All(char.IsLetterOrDigit))
            {
                query = query.Where(o => o.CustomerId == customerId);
            }
        }

        if (fromDate.HasValue)
        {
            query = query.Where(o => o.OrderDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(o => o.OrderDate <= toDate.Value);
        }

        // Total count antes de paginación (para metadata de respuesta)
        var totalCount = await query.CountAsync(ct);

        // Paginación segura con Skip/Take (traducido a OFFSET/FETCH en SQL)
        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(o => OrderResponseDto.FromEntity(o))
            .ToListAsync(ct);

        return (orders, totalCount);
    }

    public async Task<OrderResponseDto> CreateOrderAsync(OrderRequestDto requestDto, CancellationToken ct = default)
    {
        // Validación de reglas de negocio adicionales
        if (requestDto.RequiredDate.HasValue && requestDto.RequiredDate < requestDto.OrderDate)
        {
            throw new ArgumentException("La fecha requerida no puede ser anterior a la fecha de orden.");
        }

        if (requestDto.ShippedDate.HasValue && requestDto.ShippedDate < requestDto.OrderDate)
        {
            throw new ArgumentException("La fecha de envío no puede ser anterior a la fecha de orden.");
        }

        // Mapeo explícito: SOLO campos permitidos del DTO -> Entidad
        // Esto previene Mass Assignment (InternalStaffNotes nunca se asigna desde fuera)
        var order = new Order
        {
            CustomerId = requestDto.CustomerId,
            EmployeeId = requestDto.EmployeeId,
            OrderDate = requestDto.OrderDate,
            RequiredDate = requestDto.RequiredDate,
            ShippedDate = requestDto.ShippedDate,
            ShipVia = requestDto.ShipVia,
            Freight = requestDto.Freight,
            ShipName = requestDto.ShipName,
            ShipAddress = requestDto.ShipAddress,
            ShipCity = requestDto.ShipCity,
            ShipRegion = requestDto.ShipRegion,
            ShipPostalCode = requestDto.ShipPostalCode,
            ShipCountry = requestDto.ShipCountry,
            // InternalStaffNotes = null; // NUNCA se establece desde input externo
        };

        // INSERCIÓN SEGURA: EF Core usa parámetros SQL
        // INSERT INTO Orders (...) VALUES (@p0, @p1, ...)
        _context.Orders.Add(order);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Orden creada: OrderId={OrderId}, CustomerId={CustomerId}", order.OrderId, order.CustomerId);

        return OrderResponseDto.FromEntity(order);
    }

    public async Task<OrderResponseDto?> UpdateOrderAsync(int orderId, OrderRequestDto requestDto, CancellationToken ct = default)
    {
        if (orderId <= 0)
        {
            return null;
        }

        // Validación de reglas de negocio
        if (requestDto.RequiredDate.HasValue && requestDto.RequiredDate < requestDto.OrderDate)
        {
            throw new ArgumentException("La fecha requerida no puede ser anterior a la fecha de orden.");
        }

        // Búsqueda con tracking habilitado (necesario para actualización)
        var existingOrder = await _context.Orders
            .FirstOrDefaultAsync(o => o.OrderId == orderId, ct);

        if (existingOrder is null)
        {
            return null;
        }

        // Actualización explícita campo por campo (previene Mass Assignment)
        // InternalStaffNotes se conserva intacto - nunca se sobrescribe desde DTO
        existingOrder.CustomerId = requestDto.CustomerId;
        existingOrder.EmployeeId = requestDto.EmployeeId;
        existingOrder.OrderDate = requestDto.OrderDate;
        existingOrder.RequiredDate = requestDto.RequiredDate;
        existingOrder.ShippedDate = requestDto.ShippedDate;
        existingOrder.ShipVia = requestDto.ShipVia;
        existingOrder.Freight = requestDto.Freight;
        existingOrder.ShipName = requestDto.ShipName;
        existingOrder.ShipAddress = requestDto.ShipAddress;
        existingOrder.ShipCity = requestDto.ShipCity;
        existingOrder.ShipRegion = requestDto.ShipRegion;
        existingOrder.ShipPostalCode = requestDto.ShipPostalCode;
        existingOrder.ShipCountry = requestDto.ShipCountry;

        // ACTUALIZACIÓN SEGURA: EF Core genera UPDATE parametrizado con WHERE OrderId = @p0
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Orden actualizada: OrderId={OrderId}", orderId);

        return OrderResponseDto.FromEntity(existingOrder);
    }

    public async Task<bool> DeleteOrderAsync(int orderId, CancellationToken ct = default)
    {
        if (orderId <= 0)
        {
            return false;
        }

        // En producción: Soft Delete (IsDeleted = true) en lugar de Hard Delete
        var order = await _context.Orders.FindAsync(new object[] { orderId }, ct);

        if (order is null)
        {
            return false;
        }

        // ELIMINACIÓN SEGURA: DELETE parametrizado con WHERE OrderId = @p0
        _context.Orders.Remove(order);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Orden eliminada: OrderId={OrderId}", orderId);

        return true;
    }

    public async Task<bool> OrderExistsAsync(int orderId, CancellationToken ct = default)
    {
        if (orderId <= 0) return false;

        // EXISTS optimizado - devuelve solo boolean, no la entidad completa
        return await _context.Orders
            .AsNoTracking()
            .AnyAsync(o => o.OrderId == orderId, ct);
    }
}