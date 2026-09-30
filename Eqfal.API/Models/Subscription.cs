using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eqfal.API.Models
{
    public class Subscription
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        public Guid? PlanId { get; set; }

        [ForeignKey(nameof(PlanId))]
        public SubscriptionPlan? Plan { get; set; }

        public Guid? PlanPriceId { get; set; }

        [ForeignKey(nameof(PlanPriceId))]
        public SubscriptionPlanPrice? PlanPrice { get; set; }

        public BillingCycle? BillingCycle { get; set; }

        [Required, MaxLength(100)]
        public string PlanType { get; set; } = "تجربة";

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PriceSnapshot { get; set; } = 0;

        public bool IsTrial { get; set; } = true;

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

        public DateTime? ReminderSentAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
