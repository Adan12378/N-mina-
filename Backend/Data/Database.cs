using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Nomina.Backend.Models;

namespace Nomina.Backend.Data
{
    /// <summary>
    /// Contexto principal de la base de datos SQLite.
    /// Gestiona la conexión y el mapeo de las entidades Empleado y LiquidacionNomina.
    /// </summary>
    public class AppDbContext : DbContext
    {
        private const string DbFileName = "nomina_app.db";

        public DbSet<Empleado> Empleados { get; set; }
        public DbSet<LiquidacionNomina> Nominas { get; set; }

        public static string GetDatabasePath()
        {
            string carpetaData = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

            if (!Directory.Exists(carpetaData))
            {
                Directory.CreateDirectory(carpetaData);
            }

            return Path.Combine(carpetaData, DbFileName);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string rutaDb = GetDatabasePath();
                optionsBuilder.UseSqlite($"Data Source={rutaDb}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---------- Configuración de Empleado ----------
            modelBuilder.Entity<Empleado>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Documento)
                      .IsRequired()
                      .HasMaxLength(20);

                entity.HasIndex(e => e.Documento).IsUnique();

                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Apellido).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Cargo).HasMaxLength(100);

                entity.Property(e => e.SalarioBasico).HasColumnType("decimal(18,2)");
            });

            // ---------- Configuración de LiquidacionNomina ----------
            modelBuilder.Entity<LiquidacionNomina>(entity =>
            {
                entity.HasKey(n => n.Id);

                entity.HasOne(n => n.Empleado)
                      .WithMany()
                      .HasForeignKey(n => n.EmpleadoId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.Property(n => n.SalarioBasicoDevengado).HasColumnType("decimal(18,2)");
                entity.Property(n => n.HorasExtrasDiurnas).HasColumnType("decimal(18,2)");
                entity.Property(n => n.HorasExtrasNocturnas).HasColumnType("decimal(18,2)");
                entity.Property(n => n.RecargoNocturno).HasColumnType("decimal(18,2)");
                entity.Property(n => n.RecargoDominicalFestivo).HasColumnType("decimal(18,2)");
                entity.Property(n => n.AuxilioTransporte).HasColumnType("decimal(18,2)");
                entity.Property(n => n.TotalDevengado).HasColumnType("decimal(18,2)");

                entity.Property(n => n.DeduccionSalud).HasColumnType("decimal(18,2)");
                entity.Property(n => n.DeduccionPension).HasColumnType("decimal(18,2)");
                entity.Property(n => n.FondoSolidaridad).HasColumnType("decimal(18,2)");
                entity.Property(n => n.Retefuente).HasColumnType("decimal(18,2)");
                entity.Property(n => n.OtrasDeducciones).HasColumnType("decimal(18,2)");
                entity.Property(n => n.TotalDeducciones).HasColumnType("decimal(18,2)");

                entity.Property(n => n.NetoPagado).HasColumnType("decimal(18,2)");
            });
        }

        public static void Inicializar()
        {
            using var context = new AppDbContext();
            context.Database.EnsureCreated();
        }
    }
}