using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eqfal.API.Models
{
    public class SubscriptionPayment
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid(); // يُرسل لـ EzonePay كـ OrderReference

        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Required]
        public Guid PlanId { get; set; }

        [ForeignKey(nameof(PlanId))]
        public SubscriptionPlan Plan { get; set; } = null!;

        public Guid? PlanPriceId { get; set; }

        [ForeignKey(nameof(PlanPriceId))]
        public SubscriptionPlanPrice? PlanPrice { get; set; }

        public BillingCycle? BillingCycle { get; set; }

        public bool ReplacesActivePlan { get; set; } = false;

        [MaxLength(50)]
        public string? DiscountCode { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required, MaxLength(3)]
        public string Currency { get; set; } = "LYD";

        [Required, MaxLength(100)]
        public string GatewayPaymentId { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Processing, Settled, Cancelled, Rejected

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }
    }
}
