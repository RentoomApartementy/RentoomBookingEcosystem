using Microsoft.EntityFrameworkCore;
using RentoomBooking.LiveChat.Entities;

namespace RentoomBooking.LiveChat.Data;

public sealed class LiveChatDbContext : DbContext
{
    public LiveChatDbContext(DbContextOptions<LiveChatDbContext> options) : base(options)
    {
    }

    public DbSet<LiveChatSessionEntity> LiveChatSessions => Set<LiveChatSessionEntity>();
    public DbSet<LiveChatMessageEntity> LiveChatMessages => Set<LiveChatMessageEntity>();
    public DbSet<BitrixLiveChatPortalEntity> BitrixLiveChatPortals => Set<BitrixLiveChatPortalEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LiveChatSessionEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReservationToken).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");
            entity.HasIndex(x => new { x.ReservationToken, x.Status })
                .HasDatabaseName("idx_livechat_sessions_token_status");
            entity.HasIndex(x => x.BitrixChatId)
                .HasDatabaseName("idx_livechat_sessions_bitrix_chat_id");
        });

        modelBuilder.Entity<LiveChatMessageEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Sender).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Content).IsRequired().HasMaxLength(4000);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
            entity.HasIndex(x => new { x.SessionId, x.CreatedAt })
                .HasDatabaseName("idx_livechat_messages_session_created_at");
            entity.HasIndex(x => x.BitrixMessageId)
                .HasDatabaseName("idx_livechat_messages_bitrix_message_id");

            entity.HasOne(x => x.Session)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BitrixLiveChatPortalEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MemberId).IsRequired();
            entity.Property(x => x.Domain).IsRequired();
            entity.Property(x => x.ClientEndpoint).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");
            entity.HasIndex(x => x.MemberId)
                .IsUnique()
                .HasDatabaseName("idx_bitrix_livechat_portals_member_id");
            entity.HasIndex(x => x.Domain)
                .IsUnique()
                .HasDatabaseName("idx_bitrix_livechat_portals_domain");
        });
    }
}