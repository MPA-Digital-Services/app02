namespace NorthwindApi.Data;

using Microsoft.EntityFrameworkCore;
using NorthwindApi.Models;

/// <summary>
/// DbContext para la base de datos Northwind.
/// Configuración segura:
/// - Usa parámetros parametrizados nativamente (EF Core previene SQL Injection)
/// - Configura precisiones para tipos monetarios
/// - Deshabilita tracking en consultas de solo lectura (AsNoTracking) para rendimiento
/// </summary>
public class NorthwindDbContext : DbContext
{
    public NorthwindDbContext(DbContextOptions<NorthwindDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(e => e.OrderId);

            // Configuración de precisión para campos monetarios (previene errores de redondeo)
            entity.Property(e => e.Freight)
                  .HasColumnType("money")
                  .HasPrecision(19, 4);

            // Índices para optimizar consultas frecuentes
            entity.HasIndex(e => e.CustomerId).HasDatabaseName("IX_Orders_CustomerID");
            entity.HasIndex(e => e.EmployeeId).HasDatabaseName("IX_Orders_EmployeeID");
            entity.HasIndex(e => e.OrderDate).HasDatabaseName("IX_Orders_OrderDate");
        });
    }

    /// <summary>
    /// Sobrescribe SaveChanges para agregar auditoría básica si se requiere.
    /// En producción, considerar interceptors de EF Core para logging de seguridad.
    /// </summary>
    public override int SaveChanges()
    {
        // Aquí se podría agregar lógica de auditoría (CreatedAt, UpdatedAt, UserId)
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }
}