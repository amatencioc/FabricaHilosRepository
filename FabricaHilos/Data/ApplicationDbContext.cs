using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FabricaHilos.Models;
using FabricaHilos.Models.Inventario;
using FabricaHilos.Models.Produccion;
using FabricaHilos.Models.Ventas;
using FabricaHilos.Models.RecursosHumanos;

namespace FabricaHilos.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<MateriaPrima> MateriasPrimas { get; set; }
        public DbSet<ProductoTerminado> ProductosTerminados { get; set; }
        public DbSet<OrdenProduccion> OrdenesProduccion { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<Empleado> Empleados { get; set; }
        public DbSet<Asistencia> Asistencias { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configuración de MateriaPrima
            builder.Entity<MateriaPrima>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CantidadDisponible).HasColumnType("decimal(18,2)");
                entity.Property(e => e.StockMinimo).HasColumnType("decimal(18,2)");
            });

            // Configuración de ProductoTerminado
            builder.Entity<ProductoTerminado>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Cantidad).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PrecioUnitario).HasColumnType("decimal(18,2)");
            });

            // Configuración de OrdenProduccion
            builder.Entity<OrdenProduccion>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            // Configuración de Cliente
            builder.Entity<Cliente>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(150);
            });

            // Configuración de Pedido
            builder.Entity<Pedido>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.NumeroPedido).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Total).HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.Cliente)
                      .WithMany(c => c.Pedidos)
                      .HasForeignKey(e => e.ClienteId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuración de Empleado
            builder.Entity<Empleado>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.NombreCompleto).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Salario).HasColumnType("decimal(18,2)");
            });

            // Configuración de Asistencia
            builder.Entity<Asistencia>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Empleado)
                      .WithMany(emp => emp.Asistencias)
                      .HasForeignKey(e => e.EmpleadoId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
