using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Data
{
    public class TouristContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<Tourist> Tourists { get; set; }
        public DbSet<TripPlan> TripPlans { get; set; }
        public DbSet<Destination> Destinations { get; set; }
        public DbSet<TripDestination> TripDestinations { get; set; }
        public DbSet<Sponsor> Sponsors { get; set; }
        public DbSet<Branch> Branches { get; set; }
        public DbSet<RewardBranch> RewardBranches { get; set; }
        public DbSet<Reward> Rewards { get; set; }
        public DbSet<Redemption> Redemptions { get; set; }
        public DbSet<RewardView> RewardViews { get; set; }
        public DbSet<Mission> Missions { get; set; }
        public DbSet<UserMission> UserMissions { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<SponsorApprovalRequest> SponsorApprovalRequests { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }

        public TouristContext(DbContextOptions<TouristContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Destination>()
                .Property(d => d.TicketPrice)
                .HasColumnType("decimal(10, 2)");

            modelBuilder.Entity<Destination>()
                .Property(d => d.Rating)
                .HasColumnType("decimal(18, 2)");

            modelBuilder.Entity<TripPlan>()
                .Property(t => t.Budget)
                .HasColumnType("decimal(10, 2)");

            modelBuilder.Entity<MenuItem>()
                .Property(m => m.Price)
                .HasColumnType("decimal(10, 2)");

            // Link Tourist -> ApplicationUser (Identity login). Nullable FK:
            // Tourists created directly by an Admin may not have a login account.
            modelBuilder.Entity<Tourist>()
                .Property(t => t.ApplicationUserId)
                .HasMaxLength(450);

            modelBuilder.Entity<Tourist>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.ApplicationUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Link Sponsor -> ApplicationUser (Identity login). Nullable FK:
            // Sponsors created directly by an Admin may not have a login account.
            // Mirrors the Tourist -> ApplicationUser link above.
            modelBuilder.Entity<Sponsor>()
                .Property(s => s.ApplicationUserId)
                .HasMaxLength(450);

            modelBuilder.Entity<Sponsor>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(s => s.ApplicationUserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Sponsor>()
                .Property(s => s.Email)
                .HasMaxLength(450);

            // Branch -> Sponsor (one-to-many).
            modelBuilder.Entity<Branch>()
                .HasOne(b => b.Sponsor)
                .WithMany(s => s.Branches)
                .HasForeignKey(b => b.SponsorId)
                .OnDelete(DeleteBehavior.Cascade);

            // Reward <-> Branch many-to-many join (RewardBranch).
            modelBuilder.Entity<RewardBranch>()
                .HasKey(rb => new { rb.RewardId, rb.BranchId });

            modelBuilder.Entity<RewardBranch>()
                .HasOne(rb => rb.Reward)
                .WithMany(r => r.RewardBranches)
                .HasForeignKey(rb => rb.RewardId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RewardBranch>()
                .HasOne(rb => rb.Branch)
                .WithMany(b => b.RewardBranches)
                .HasForeignKey(rb => rb.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            // Reward -> Sponsor is NoAction (not Cascade) to avoid a second
            // cascade path into RewardBranch (Sponsor->Reward->RewardBranch and
            // Sponsor->Branch->RewardBranch would otherwise create a cycle that
            // SQL Server rejects). Deleting a Sponsor that still has Rewards is
            // therefore blocked until its Rewards are removed.
            modelBuilder.Entity<Reward>()
                .HasOne(r => r.Sponsor)
                .WithMany(s => s.Rewards)
                .HasForeignKey(r => r.SponsorId)
                .OnDelete(DeleteBehavior.NoAction);

            // SponsorApprovalRequest -> ApplicationUser (one-to-many).
            modelBuilder.Entity<SponsorApprovalRequest>()
                .Property(r => r.ApplicationUserId)
                .HasMaxLength(450);

            modelBuilder.Entity<SponsorApprovalRequest>()
                .Property(r => r.ReviewedByAdminId)
                .HasMaxLength(450);

            modelBuilder.Entity<SponsorApprovalRequest>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(r => r.ApplicationUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // App-user shared profile columns (sized to match the
            // Identity column limits and keep the table compact).
            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.FirstName)
                .HasMaxLength(100);

            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.LastName)
                .HasMaxLength(100);

            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.Nationality)
                .HasMaxLength(100);

            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.ProfilePicturePath)
                .HasMaxLength(500);

            // Notification -> Sponsor (one-to-many).
            modelBuilder.Entity<Notification>()
                .HasOne<Sponsor>()
                .WithMany()
                .HasForeignKey(n => n.SponsorId)
                .OnDelete(DeleteBehavior.Cascade);

            // SupportTicket -> Sponsor (one-to-many).
            modelBuilder.Entity<SupportTicket>()
                .HasOne<Sponsor>()
                .WithMany()
                .HasForeignKey(st => st.SponsorId)
                .OnDelete(DeleteBehavior.Cascade);

            // SupportTicket -> Tourist (one-to-many, optional).
            modelBuilder.Entity<SupportTicket>()
                .HasOne<Tourist>()
                .WithMany()
                .HasForeignKey(st => st.TouristId)
                .OnDelete(DeleteBehavior.NoAction);

            // NOTE: All sample data is now seeded from JSON via
            // Services/DbInitializer.cs (see Program.cs). The ad-hoc
            // modelBuilder.HasData(...) calls that previously lived here have
            // been removed in favour of that idempotent, maintainable routine.
        }
    }
}
