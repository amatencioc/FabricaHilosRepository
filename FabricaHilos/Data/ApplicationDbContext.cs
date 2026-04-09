using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FabricaHilos.Models;
using FabricaHilos.Models.Facturacion;
using FabricaHilos.Models.Produccion;
using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<OrdenProduccion> OrdenesProduccion { get; set; }
        public DbSet<RegistroAutoconer> RegistrosAutoconer { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<FhLcDocumento> LcDocumentos { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configuración de OrdenProduccion
            builder.Entity<OrdenProduccion>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            // Configuración de RegistroAutoconer
            builder.Entity<RegistroAutoconer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.VelocidadMMin).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PesoBruto).HasColumnType("decimal(18,2)");
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
                entity.HasIndex(e => e.NumeroPedido).IsUnique();
                entity.Property(e => e.Total).HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.Cliente)
                      .WithMany(c => c.Pedidos)
                      .HasForeignKey(e => e.ClienteId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

                    }
                }
            }
