
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SistemaGestaoFinanceira.Models;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Cliente> Clientes { get; set; }

    public DbSet<ContaFinanceira> ContasFinanceiras { get; set; }

    public DbSet<CategoriaFinanceira> CategoriasFinanceiras { get; set; }

    public DbSet<Receita> Receitas { get; set; }

    public DbSet<Despesa> Despesas { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ContaFinanceira>()
            .Property(c => c.SaldoInicial)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Receita>()
            .Property(r => r.Valor)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Despesa>()
            .Property(d => d.Valor)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Cliente>()
            .Property(c => c.Nome)
            .HasMaxLength(150)
            .IsRequired();

        modelBuilder.Entity<Cliente>()
            .Property(c => c.TipoPessoa)
            .HasMaxLength(2);

        modelBuilder.Entity<Cliente>()
            .Property(c => c.Documento)
            .HasMaxLength(14);

        modelBuilder.Entity<Cliente>()
            .Property(c => c.EmailContato)
            .HasMaxLength(256);

        modelBuilder.Entity<Cliente>()
            .Property(c => c.NomeResponsavel)
            .HasMaxLength(150);

        modelBuilder.Entity<Cliente>()
            .HasIndex(c => c.Documento)
            .IsUnique()
            .HasFilter("[Documento] IS NOT NULL");

        modelBuilder.Entity<ApplicationUser>()
            .HasOne(u => u.Cliente)
            .WithMany(c => c.Usuarios)
            .HasForeignKey(u => u.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ContaFinanceira>()
            .HasOne(c => c.Cliente)
            .WithMany()
            .HasForeignKey(c => c.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CategoriaFinanceira>()
            .HasOne(c => c.Cliente)
            .WithMany()
            .HasForeignKey(c => c.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Receita>()
            .HasOne(r => r.Cliente)
            .WithMany()
            .HasForeignKey(r => r.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Despesa>()
            .HasOne(d => d.Cliente)
            .WithMany()
            .HasForeignKey(d => d.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

