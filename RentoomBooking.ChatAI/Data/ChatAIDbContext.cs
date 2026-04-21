using Microsoft.EntityFrameworkCore;
using RentoomBooking.ChatAI.Entities;

namespace RentoomBooking.ChatAI.Data;

public sealed class ChatAIDbContext : DbContext
{
    public ChatAIDbContext(DbContextOptions<ChatAIDbContext> options) : base(options)
    {
    }

    public DbSet<ChatConversationEntity> ChatConversations => Set<ChatConversationEntity>();
    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatConversationEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReservationToken).IsRequired();
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");
            entity.HasIndex(x => new { x.ReservationToken, x.UpdatedAt })
                .HasDatabaseName("idx_chat_conversations_reservation_updated_at");
        });

        modelBuilder.Entity<ChatMessageEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Content).IsRequired();
            entity.Property(x => x.TokenCount).HasDefaultValue(0);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
            entity.HasIndex(x => new { x.ConversationId, x.CreatedAt })
                .HasDatabaseName("idx_chat_messages_conversation_created_at");

            entity.HasOne(x => x.Conversation)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
