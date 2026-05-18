using Microsoft.EntityFrameworkCore;
using NameSelect.Server.Data.Entities;

namespace NameSelect.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<NameList> Lists => Set<NameList>();
    public DbSet<VotingSession> Sessions => Set<VotingSession>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<ItemState> ItemStates => Set<ItemState>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<NameList>(e =>
        {
            e.ToTable("lists");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").IsRequired();
            e.Property(x => x.FilePath).HasColumnName("file_path").IsRequired();
            e.Property(x => x.ItemCount).HasColumnName("item_count");
            e.Property(x => x.LoadedAt).HasColumnName("loaded_at");
        });

        b.Entity<VotingSession>(e =>
        {
            e.ToTable("voting_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.ListId).HasColumnName("list_id").IsRequired();
            e.Property(x => x.ConfidenceThreshold).HasColumnName("confidence_threshold").HasDefaultValue(3);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            e.Property(x => x.ShareToken).HasColumnName("share_token");
            e.HasIndex(x => x.ShareToken).IsUnique();
            e.HasIndex(x => new { x.UserId, x.ListId });
            e.HasOne<NameList>().WithMany().HasForeignKey(x => x.ListId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Vote>(e =>
        {
            e.ToTable("votes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.SessionId).HasColumnName("session_id");
            e.Property(x => x.ItemA).HasColumnName("item_a").IsRequired();
            e.Property(x => x.ItemB).HasColumnName("item_b").IsRequired();
            e.Property(x => x.Result).HasColumnName("result").HasConversion<string>();
            e.Property(x => x.PresentedAt).HasColumnName("presented_at").HasDefaultValueSql("now()");
            e.HasOne<VotingSession>().WithMany().HasForeignKey(x => x.SessionId);
            e.HasIndex(x => x.SessionId);
        });

        b.Entity<ItemState>(e =>
        {
            e.ToTable("item_states");
            e.HasKey(x => new { x.SessionId, x.Item });
            e.Property(x => x.SessionId).HasColumnName("session_id");
            e.Property(x => x.Item).HasColumnName("item");
            e.Property(x => x.EloScore).HasColumnName("elo_score").HasDefaultValue(1000.0);
            e.Property(x => x.TimesShown).HasColumnName("times_shown").HasDefaultValue(0);
            e.Property(x => x.IsBanned).HasColumnName("is_banned").HasDefaultValue(false);
            e.HasOne<VotingSession>().WithMany().HasForeignKey(x => x.SessionId);
        });
    }
}
