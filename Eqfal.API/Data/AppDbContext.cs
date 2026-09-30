using Microsoft.EntityFrameworkCore;
using Eqfal.API.Models;

namespace Eqfal.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<WhatsAppSession> WhatsAppSessions { get; set; }
        public DbSet<MonitoredNumber> MonitoredNumbers { get; set; }
        public DbSet<DynamicKeyword> DynamicKeywords { get; set; }
        public DbSet<Operation> Operations { get; set; }
        public DbSet<WhatsAppAuthState> WhatsAppAuthStates { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<LidMapping> LidMappings { get; set; }
        public DbSet<SupportTicket> SupportTickets { get; set; }

        // Subscriptions, Payments & Marketers
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
        public DbSet<SubscriptionPlanPrice> SubscriptionPlanPrices { get; set; }
        public DbSet<SubscriptionPlanFeature> SubscriptionPlanFeatures { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<SubscriptionPayment> SubscriptionPayments { get; set; }
        public DbSet<DiscountCode> DiscountCodes { get; set; }
        public DbSet<MarketerCategory> MarketerCategories { get; set; }
        public DbSet<Marketer> Marketers { get; set; }
        public DbSet<MarketerTransaction> MarketerTransactions { get; set; }
        public DbSet<SubscriptionSettings> SubscriptionSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Index for fast Baileys auth state lookup by UserId and KeyId
            modelBuilder.Entity<WhatsAppAuthState>()
                .HasIndex(a => new { a.UserId, a.KeyId })
                .IsUnique();

            // Index for fast AuditLog queries by UserId and Timestamp
            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => new { a.UserId, a.Timestamp });

            // Index for fast LID mappings lookup
            modelBuilder.Entity<LidMapping>()
                .HasIndex(l => l.Lid)
                .IsUnique();
            
            // To ensure safe decimal precision for financial operations
            modelBuilder.Entity<Operation>()
                .Property(o => o.Amount)
                .HasColumnType("decimal(18,2)");

            // Configure 1-to-1 relationship for User <-> WhatsAppSession
            modelBuilder.Entity<User>()
                .HasOne(u => u.WhatsAppSession)
                .WithOne(w => w.User)
                .HasForeignKey<WhatsAppSession>(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure 1-to-Many relationship for User <-> MonitoredNumber
            modelBuilder.Entity<User>()
                .HasMany(u => u.MonitoredNumbers)
                .WithOne(m => m.User)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure 1-to-Many relationship for User <-> DynamicKeyword
            modelBuilder.Entity<User>()
                .HasMany(u => u.DynamicKeywords)
                .WithOne(d => d.User)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure 1-to-Many relationship for User <-> Operation
            modelBuilder.Entity<User>()
                .HasMany(u => u.Operations)
                .WithOne(o => o.User)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent multiple cascade paths

            // Configure 1-to-Many relationship for MonitoredNumber <-> Operation
            modelBuilder.Entity<MonitoredNumber>()
                .HasMany(m => m.Operations)
                .WithOne(o => o.MonitoredNumber)
                .HasForeignKey(o => o.MonitoredNumberId)
                .OnDelete(DeleteBehavior.SetNull); // Set to null if monitored number is deleted (to keep financial history)

            // ==================== Subscriptions Configurations ====================

            // SubscriptionPlanPrice: Unique Index on (PlanId, BillingCycle)
            modelBuilder.Entity<SubscriptionPlanPrice>()
                .HasIndex(p => new { p.PlanId, p.BillingCycle })
                .IsUnique();

            modelBuilder.Entity<SubscriptionPlan>()
                .HasMany(p => p.Prices)
                .WithOne(pr => pr.Plan)
                .HasForeignKey(pr => pr.PlanId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SubscriptionPlan>()
                .HasMany(p => p.Features)
                .WithOne(f => f.Plan)
                .HasForeignKey(f => f.PlanId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SubscriptionPlan>()
                .HasMany(p => p.Subscriptions)
                .WithOne(s => s.Plan)
                .HasForeignKey(s => s.PlanId)
                .OnDelete(DeleteBehavior.Restrict);

            // Subscription: Indexes & Relations
            modelBuilder.Entity<Subscription>()
                .HasIndex(s => new { s.UserId, s.ExpiresAt });

            modelBuilder.Entity<User>()
                .HasMany(u => u.Subscriptions)
                .WithOne(s => s.User)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // SubscriptionPayment: Unique Index on GatewayPaymentId & UserId Index
            modelBuilder.Entity<SubscriptionPayment>()
                .HasIndex(p => p.GatewayPaymentId)
                .IsUnique();

            modelBuilder.Entity<SubscriptionPayment>()
                .HasIndex(p => new { p.UserId, p.CreatedAt });

            modelBuilder.Entity<User>()
                .HasMany(u => u.SubscriptionPayments)
                .WithOne(sp => sp.User)
                .HasForeignKey(sp => sp.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // DiscountCode: Unique Index on Code
            modelBuilder.Entity<DiscountCode>()
                .HasIndex(d => d.Code)
                .IsUnique();

            modelBuilder.Entity<DiscountCode>()
                .HasOne(d => d.Marketer)
                .WithMany(m => m.DiscountCodes)
                .HasForeignKey(d => d.MarketerId)
                .OnDelete(DeleteBehavior.Restrict);

            // MarketerCategory: Unique Index on Name
            modelBuilder.Entity<MarketerCategory>()
                .HasIndex(c => c.Name)
                .IsUnique();

            modelBuilder.Entity<Marketer>()
                .HasOne(m => m.Category)
                .WithMany(c => c.Marketers)
                .HasForeignKey(m => m.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Marketer>()
                .HasIndex(m => m.Phone);

            // MarketerTransaction: Composite Index on (MarketerId, CreatedAt)
            modelBuilder.Entity<MarketerTransaction>()
                .HasIndex(t => new { t.MarketerId, t.CreatedAt });

            modelBuilder.Entity<MarketerTransaction>()
                .HasIndex(t => t.Type);

            modelBuilder.Entity<MarketerTransaction>()
                .HasOne(t => t.Marketer)
                .WithMany(m => m.Transactions)
                .HasForeignKey(t => t.MarketerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MarketerTransaction>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<MarketerTransaction>()
                .HasOne(t => t.DiscountCode)
                .WithMany()
                .HasForeignKey(t => t.DiscountCodeId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
