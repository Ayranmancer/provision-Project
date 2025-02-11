using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext
{
    // DbSet for the ExchangeRate entity
    public DbSet<ExchangeRate> ExchangeRates { get; set; }

    // Constructor that accepts DbContextOptions
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    // Configure the model and relationships
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Ensure the combination of CurrencyCode and Date is unique
        modelBuilder.Entity<ExchangeRate>()
            .HasIndex(e => new { e.CurrencyCode, e.Date })
            .IsUnique();

        // Optional: Configure additional entity properties or relationships here
        modelBuilder.Entity<ExchangeRate>()
            .Property(e => e.CurrencyCode)
            .IsRequired()
            .HasMaxLength(3); // Assuming CurrencyCode is a 3-letter code (e.g., USD, EUR)

        modelBuilder.Entity<ExchangeRate>()
            .Property(e => e.CurrencyName)
            .IsRequired()
            .HasMaxLength(100);

        modelBuilder.Entity<ExchangeRate>()
            .Property(e => e.ForexBuying)
            .HasColumnType("decimal(18, 4)"); // Configure precision and scale for ForexBuying

        modelBuilder.Entity<ExchangeRate>()
            .Property(e => e.ForexSelling)
            .HasColumnType("decimal(18, 4)"); // Configure precision and scale for ForexSelling

        modelBuilder.Entity<ExchangeRate>()
            .Property(e => e.Date)
            .IsRequired();
    }
}