namespace NorthwindApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NorthwindApi.DTOs;
using NorthwindApi.Services;

/// <summary>
/// Controlador para gestión de órdenes (Northwind).
/// SEGURIDAD IMPLEMENTADA:
/// - [Authorize] en toda la clase: Requiere autenticación JWT válida para TODOS los endpoints
/// - RBAC en Create: Solo roles "SalesAgent" y "Manager" pueden crear órdenes
/// - Validación ModelState.IsValid: Rechaza payloads malformados con 400
/// - DTOs tipados: Previene Mass Assignment y expone solo datos necesarios
/// - Rate Limiting: Configurado globalmente en Program.cs
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize] // REQUISITO 1: Protege TODOS los endpoints - requiere JWT válido
[Produces("application/json")]
[Consumes("application/json")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene una orden por ID.
    /// </summary>
    /// <param name="id">ID de la orden (debe ser > 0)</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Orden encontrada o 404</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponseDto>> GetOrder(int id, CancellationToken ct = default)
    {
        // Validación de parámetros de ruta
        if (id <= 0)
        {
            return BadRequest(new { error = "El ID de orden debe ser un valor positivo." });
        }

        var order = await _orderService.GetOrderByIdAsync(id, ct);

        if (order is null)
        {
            _logger.LogWarning("Orden no encontrada: OrderId={OrderId}", id);
            return NotFound(new { error = $"No se encontró la orden con ID {id}." });
        }

        return Ok(order);
    }

    /// <summary>
    /// Obtiene lista paginada de órdenes con filtros opcionales.
    /// </summary>
    /// <param name="pageNumber">Número de página (mín 1, default 1)</param>
    /// <param name="pageSize">Tamaño de página (1-100, default 20)</param>
    /// <param name="customerId">Filtrar por CustomerId (5 chars)</param>
    /// <param name="fromDate">Fecha desde (ISO 8601)</param>
    /// <param name="toDate">Fecha hasta (ISO 8601)</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Lista paginada de órdenes</returns>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> GetOrders(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? customerId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken ct = default)
    {
        // Validación de parámetros de consulta (defensa en profundidad)
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        if (!string.IsNullOrWhiteSpace(customerId) && customerId.Length != 5)
        {
            return BadRequest(new { error = "CustomerId debe tener exactamente 5 caracteres." });
        }

        var (orders, totalCount) = await _orderService.GetOrdersAsync(
            pageNumber, pageSize, customerId, fromDate, toDate, ct);

        // Respuesta con metadata de paginación (estándar REST)
        return Ok(new
        {
            data = orders,
            pagination = new
            {
                pageNumber,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            }
        });
    }

    /// <summary>
    /// Crea una nueva orden.
    /// REQUISITO 1 (RBAC): Solo roles "SalesAgent" y "Manager" pueden crear órdenes.
    /// REQUISITO 2: Usa OrderRequestDto (validado) - NUNCA expone InternalStaffNotes.
    /// REQUISITO 3: Valida ModelState.IsValid antes de procesar.
    /// </summary>
    /// <param name="requestDto">Datos de la orden a crear (validados via Data Annotations)</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Orden creada con ID asignado</returns>
    [HttpPost]
    [Authorize(Roles = "SalesAgent,Manager")] // REQUISITO 1: RBAC - Solo SalesAgent y Manager
    [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrderResponseDto>> CreateOrder(
        [FromBody] OrderRequestDto requestDto,
        CancellationToken ct = default)
    {
        // REQUISITO 3: Validación inmediata de ModelState
        // Rechaza payloads que no cumplan Data Annotations del DTO
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("CreateOrder: ModelState inválido. Errores: {Errors}",
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            return BadRequest(new
            {
                error = "Datos de entrada inválidos.",
                details = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray())
            });
        }

        try
        {
            var createdOrder = await _orderService.CreateOrderAsync(requestDto, ct);

            // Retorna 201 Created con Location header (RESTful)
            return CreatedAtAction(nameof(GetOrder), new { id = createdOrder.OrderId }, createdOrder);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Error de validación de negocio en CreateOrder");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error interno creando orden");
            // En producción: devolver ProblemDetails genérico (configurado en Program.cs)
            throw; // Deja que el middleware global maneje la excepción
        }
    }

    /// <summary>
    /// Actualiza una orden existente.
    /// Requiere autenticación (Authorize en clase) pero no rol específico adicional.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponseDto>> UpdateOrder(
        int id,
        [FromBody] OrderRequestDto requestDto,
        CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return BadRequest(new { error = "El ID de orden debe ser un valor positivo." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                error = "Datos de entrada inválidos.",
                details = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray())
            });
        }

        try
        {
            var updatedOrder = await _orderService.UpdateOrderAsync(id, requestDto, ct);

            if (updatedOrder is null)
            {
                return NotFound(new { error = $"No se encontró la orden con ID {id}." });
            }

            return Ok(updatedOrder);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Elimina una orden.
    /// En producción considerar soft delete y permisos adicionales.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOrder(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return BadRequest(new { error = "El ID de orden debe ser un valor positivo." });
        }

        var deleted = await _orderService.DeleteOrderAsync(id, ct);

        if (!deleted)
        {
            return NotFound(new { error = $"No se encontró la orden con ID {id}." });
        }

        return NoContent();
    }
}