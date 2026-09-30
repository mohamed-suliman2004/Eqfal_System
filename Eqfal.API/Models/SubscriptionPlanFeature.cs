using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eqfal.API.Models
{
    public class SubscriptionPlanFeature
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid PlanId { get; set; }

        [ForeignKey(nameof(PlanId))]
        public SubscriptionPlan Plan { get; set; } = null!;

        [Required, MaxLength(250)]
        public string Text { get; set; } = string.Empty;

        public int SortOrder { get; set; } = 0;
    }
}
