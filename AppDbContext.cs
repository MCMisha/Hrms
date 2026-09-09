using Hrms.Models;
using Microsoft.EntityFrameworkCore;

namespace Hrms;

public class AppDbContext : DbContext
{
    private readonly IConfiguration _configuration; 
    
    public AppDbContext(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public DbSet<Employee> Employees { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>()
            .Property(e => e.DateOfBirth)
            .HasColumnType("date");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql(_configuration.GetConnectionString("AppDB"));
        }
    }
}