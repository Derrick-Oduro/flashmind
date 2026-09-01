using Microsoft.EntityFrameworkCore;
using Flashminds.Models;

namespace Flashminds.Data;

/// <summary>
/// Entity Framework Core DbContext for FlashMind application.
/// Manages all database operations for Decks, Cards, StudySessions, and CardReviews.
/// </summary>
public class FlashmindsContext : DbContext
{
    public FlashmindsContext(DbContextOptions<FlashmindsContext> options)
        : base(options)
    {
    }

    public DbSet<Deck> Decks { get; set; }
    public DbSet<Card> Cards { get; set; }
    public DbSet<StudySession> StudySessions { get; set; }
    public DbSet<CardReview> CardReviews { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Deck configuration
        modelBuilder.Entity<Deck>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.OwnerId).HasMaxLength(450);
            entity.HasIndex(e => e.OwnerId);
            entity.HasMany(e => e.Cards)
                .WithOne(c => c.Deck)
                .HasForeignKey(c => c.DeckId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.StudySessions)
                .WithOne(s => s.Deck)
                .HasForeignKey(s => s.DeckId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Card configuration
        modelBuilder.Entity<Card>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Question).IsRequired();
            entity.Property(e => e.Answer).IsRequired();
            entity.Property(e => e.CardType).IsRequired().HasMaxLength(20).HasDefaultValue("Basic");
            entity.Property(e => e.Hint).HasMaxLength(1000);
            entity.Property(e => e.Explanation).HasMaxLength(2000);
            entity.HasIndex(e => e.DeckId);
            entity.HasIndex(e => e.NextReviewDate);
        });

        // StudySession configuration
        modelBuilder.Entity<StudySession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DeckId);
            entity.HasIndex(e => e.StartedAt);
        });

        // CardReview configuration
        modelBuilder.Entity<CardReview>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Card)
                .WithMany()
                .HasForeignKey(e => e.CardId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.StudySession)
                .WithMany()
                .HasForeignKey(e => e.StudySessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.CardId);
            entity.HasIndex(e => e.StudySessionId);
        });
    }
}
