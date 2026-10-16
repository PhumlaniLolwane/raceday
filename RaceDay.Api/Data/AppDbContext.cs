using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<RaceRoute> Routes => Set<RaceRoute>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Organiser','Participant')"));
            e.HasKey(x => x.UserID);
            e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(x => x.Role).HasMaxLength(20).HasDefaultValue(Roles.Participant);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        });

        b.Entity<Event>(e =>
        {
            e.ToTable("Events", t =>
            {
                t.HasCheckConstraint("CK_Events_EventType", "[EventType] IN ('Run','Walk','Cycle')");
                t.HasCheckConstraint("CK_Events_Distance", "[DistanceKM] > 0");
            });
            e.Property(x => x.EventName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Location).HasMaxLength(150).IsRequired();
            e.Property(x => x.DistanceKM).HasColumnType("decimal(6,2)");
            e.Property(x => x.EventType).HasMaxLength(20).HasDefaultValue("Run");
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.Organiser).WithMany(u => u.Events)
             .HasForeignKey(x => x.OrganiserID).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Category>(e =>
        {
            e.ToTable("Categories", t =>
            {
                t.HasCheckConstraint("CK_Categories_Distance", "[DistanceKM] > 0");
                t.HasCheckConstraint("CK_Categories_MaxParticipants", "[MaxParticipants] > 0");
            });
            e.Property(x => x.CategoryName).HasMaxLength(100).IsRequired();
            e.Property(x => x.DistanceKM).HasColumnType("decimal(6,2)");
            e.Property(x => x.MaxParticipants).HasDefaultValue(100);
            e.Property(x => x.EntryFee).HasColumnType("decimal(8,2)").HasDefaultValue(0m);
            e.HasOne(x => x.Event).WithMany(ev => ev.Categories)
             .HasForeignKey(x => x.EventID).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<RaceRoute>(e =>
        {
            e.ToTable("Routes");
            e.HasKey(x => x.RouteID);
            e.Property(x => x.StartPoint).HasMaxLength(150).IsRequired();
            e.Property(x => x.EndPoint).HasMaxLength(150).IsRequired();
            e.Property(x => x.MapURL).HasMaxLength(255);
            e.HasIndex(x => x.CategoryID).IsUnique();
            e.HasOne(x => x.Category).WithOne(c => c.Route)
             .HasForeignKey<RaceRoute>(x => x.CategoryID).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolments", t => t.HasCheckConstraint("CK_Enrolments_Status", "[Status] IN ('Registered','Cancelled','Completed')"));
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Registered");
            e.Property(x => x.EnrolmentDate).HasDefaultValueSql("CAST(GETDATE() AS DATE)");
            e.HasIndex(x => new { x.ParticipantID, x.CategoryID }).IsUnique();
            e.HasOne(x => x.Participant).WithMany(u => u.Enrolments)
             .HasForeignKey(x => x.ParticipantID).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments)
             .HasForeignKey(x => x.CategoryID).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Result>(e =>
        {
            e.ToTable("Results", t => t.HasCheckConstraint("CK_Results_Status", "[Status] IN ('Pending','Finished','DNF','DSQ')"));
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Pending");
            e.HasIndex(x => x.EnrolmentID).IsUnique();
            e.HasOne(x => x.Enrolment).WithOne(en => en.Result)
             .HasForeignKey<Result>(x => x.EnrolmentID).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
