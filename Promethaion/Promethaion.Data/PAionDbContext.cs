using Microsoft.EntityFrameworkCore;
using Promethaion.Core.Entities;

namespace Promethaion.Data
{
    public class PAionDbContext : DbContext
    {
        public PAionDbContext(DbContextOptions<PAionDbContext> options) : base(options)
        {
        }

        public DbSet<PatternEvent> DrawResults => Set<PatternEvent>();
        public DbSet<PatternForecast> Predictions => Set<PatternForecast>();
        public DbSet<TrainingMetrics> ModelMetrics => Set<TrainingMetrics>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PatternEvent>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.DrawNumber, x.GameName }).IsUnique();
                e.HasIndex(x => x.DrawDate);
                e.Property(x => x.GameName).HasMaxLength(64);
            });

            modelBuilder.Entity<PatternForecast>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.TargetDrawNumber);
                e.HasOne(x => x.ActualResult)
                 .WithMany()
                 .HasForeignKey(x => x.ActualDrawResultId)
                 .IsRequired(false);
                e.Property(x => x.ModelVersion).HasMaxLength(32);
            });

            modelBuilder.Entity<TrainingMetrics>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.PipelineName, x.IsBestVersion });
                e.Property(x => x.ModelVersion).HasMaxLength(32);
                e.Property(x => x.PipelineName).HasMaxLength(64);
            });
        }
    }
}
