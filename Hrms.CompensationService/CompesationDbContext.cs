namespace Hrms.CompensationService;

using Models;
using Microsoft.EntityFrameworkCore;

public class CompensationDbContext : DbContext
{
    private readonly IConfiguration _configuration; 
    
    public CompensationDbContext(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public DbSet<Compensation> Compensations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Compensation>()
            .Property(e => e.EffectiveFrom)
            .HasColumnType("date");
        
        modelBuilder.Entity<Compensation>()
            .HasIndex(c => c.EmployeeId)
            .IsUnique()
            .HasFilter("\"EffectiveTo\" IS NULL");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql(_configuration.GetConnectionString("CompensationDb"));
        }
    }
}