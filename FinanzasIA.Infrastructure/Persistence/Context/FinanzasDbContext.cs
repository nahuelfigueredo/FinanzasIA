using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanzasIA.Infrastructure.Persistence.Context;

public class FinanzasDbContext : DbContext
{
    public FinanzasDbContext(DbContextOptions<FinanzasDbContext> options)
        : base(options)
    {
    }

    public DbSet<Movimiento> Movimientos => Set<Movimiento>();

    public DbSet<Categoria> Categorias => Set<Categoria>();

    public DbSet<Cuenta> Cuentas => Set<Cuenta>();

    public DbSet<MensajeProcesado> MensajesProcesados => Set<MensajeProcesado>();

    public DbSet<UsuarioWhatsapp> UsuariosWhatsapp => Set<UsuarioWhatsapp>();

    public DbSet<TicketPendiente> TicketsPendientes => Set<TicketPendiente>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.Property(x => x.Nombre)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(x => x.UsuarioId)
                  .HasMaxLength(450);

            entity.HasIndex(x => x.UsuarioId);

            entity.HasData(
                new Categoria
                {
                    Id = 1,
                    Nombre = "Salario",
                    TipoMovimiento = TipoMovimiento.Ingreso,
                    FechaCreacion = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Categoria
                {
                    Id = 2,
                    Nombre = "Alimentación",
                    TipoMovimiento = TipoMovimiento.Egreso,
                    FechaCreacion = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                });
        });

        modelBuilder.Entity<Movimiento>(entity =>
        {
            entity.Property(x => x.Descripcion)
                  .HasMaxLength(300);

            entity.Property(x => x.Monto)
                  .HasPrecision(18, 2);

            entity.Property(x => x.UsuarioId)
                  .HasMaxLength(450);

            entity.HasIndex(x => x.UsuarioId);

            entity.HasOne(x => x.Categoria)
                  .WithMany(x => x.Movimientos)
                  .HasForeignKey(x => x.CategoriaId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Cuenta)
                  .WithMany(x => x.Movimientos)
                  .HasForeignKey(x => x.CuentaId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Cuenta>(entity =>
        {
            entity.Property(x => x.Nombre)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(x => x.UsuarioId)
                  .HasMaxLength(450);

            entity.HasIndex(x => x.UsuarioId);
        });

        modelBuilder.Entity<MensajeProcesado>(entity =>
        {
            entity.Property(x => x.Texto)
                  .HasMaxLength(1000)
                  .IsRequired();

            entity.Property(x => x.Respuesta)
                  .HasMaxLength(2000);

            entity.Property(x => x.UsuarioId)
                  .HasMaxLength(450);

            entity.HasIndex(x => x.UsuarioId);
            entity.HasIndex(x => x.FechaCreacion);
        });

        modelBuilder.Entity<UsuarioWhatsapp>(entity =>
        {
            entity.Property(x => x.UsuarioId)
                  .HasMaxLength(450)
                  .IsRequired();

            entity.Property(x => x.NumeroTelefono)
                  .HasMaxLength(30)
                  .IsRequired();

            entity.Property(x => x.Nombre)
                  .HasMaxLength(100);

            entity.HasIndex(x => new { x.NumeroTelefono, x.Canal }).IsUnique();
            entity.HasIndex(x => x.UsuarioId);
        });

        modelBuilder.Entity<TicketPendiente>(entity =>
        {
            entity.Property(x => x.UsuarioId)
                  .HasMaxLength(450)
                  .IsRequired();

            entity.Property(x => x.TextoOcr)
                  .HasMaxLength(4000);

            entity.Property(x => x.Monto)
                  .HasPrecision(18, 2);

            entity.Property(x => x.Comercio)
                  .HasMaxLength(200);

            entity.Property(x => x.CategoriaSugerida)
                  .HasMaxLength(100);

            entity.Property(x => x.DatoSolicitado)
                  .HasMaxLength(20);

            entity.HasIndex(x => new { x.UsuarioId, x.Activo });
        });
    }
}