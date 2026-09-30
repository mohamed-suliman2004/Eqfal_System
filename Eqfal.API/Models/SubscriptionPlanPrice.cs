using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eqfal.API.Models
{
    public class SubscriptionPlanPrice
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid PlanId { get; set; }

        [ForeignKey(nameof(PlanId))]
        public SubscriptionPlan Plan { get; set; } = null!;

        [Required]
        public BillingCycle BillingCycle { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public int DurationDays { get; set; } = 30;

        public bool IsActive { get; set; } = true;
    }
}
