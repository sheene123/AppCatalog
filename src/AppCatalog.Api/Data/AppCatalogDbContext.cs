using AppCatalog.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AppCatalog.Api.Data;

/// <summary>
/// Contexte EF Core : point d'entrée vers la base. Chaque DbSet correspond à une table.
/// </summary>
public class AppCatalogDbContext : DbContext
{
    public AppCatalogDbContext(DbContextOptions<AppCatalogDbContext> options)
        : base(options)
    {
    }

    public DbSet<Application> Applications => Set<Application>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var app = modelBuilder.Entity<Application>();

        app.Property(a => a.Name).IsRequired().HasMaxLength(120);
        app.Property(a => a.Owner).IsRequired().HasMaxLength(120);
        app.Property(a => a.Stack).HasMaxLength(400);

        // On stocke l'enum en texte plutôt qu'en entier : la base reste lisible
        // (« Vital » au lieu de « 3 ») et l'ordre des valeurs de l'enum peut changer
        // sans corrompre les données existantes.
        app.Property(a => a.Criticality)
            .HasConversion<string>()
            .HasMaxLength(20);

        app.HasIndex(a => a.Name);

        SeedData(modelBuilder);
    }

    /// <summary>
    /// Jeu de données de démonstration inséré via une migration (HasData).
    /// Les dates sont volontairement constantes : HasData exige des valeurs
    /// déterministes, sinon chaque « dotnet ef migrations add » détecterait un
    /// changement et générerait une migration inutile.
    /// </summary>
    private static void SeedData(ModelBuilder modelBuilder)
    {
        var seededAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        modelBuilder.Entity<Application>().HasData(
            new Application
            {
                Id = 1,
                Name = "Portail RH",
                Owner = "Équipe SIRH",
                Stack = "ASP.NET Core, SQL Server",
                Criticality = Criticality.High,
                LastDeployedAt = new DateTimeOffset(2026, 8, 20, 9, 30, 0, TimeSpan.Zero),
                CreatedAt = seededAt,
                UpdatedAt = seededAt
            },
            new Application
            {
                Id = 2,
                Name = "Référentiel Fournisseurs",
                Owner = "Équipe Achats",
                Stack = "ASP.NET Core, PostgreSQL",
                Criticality = Criticality.Medium,
                LastDeployedAt = new DateTimeOffset(2026, 7, 3, 14, 0, 0, TimeSpan.Zero),
                CreatedAt = seededAt,
                UpdatedAt = seededAt
            },
            new Application
            {
                Id = 3,
                Name = "Passerelle Paiement",
                Owner = "Équipe Finance",
                Stack = "ASP.NET Core, Redis, SQL Server",
                Criticality = Criticality.Vital,
                LastDeployedAt = new DateTimeOffset(2026, 8, 28, 6, 15, 0, TimeSpan.Zero),
                CreatedAt = seededAt,
                UpdatedAt = seededAt
            }
        );
    }
}
