using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Eqfal.API.Models
{
    public class SubscriptionPlan
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string NameAr { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? DescriptionAr { get; set; }

        public bool IsTrialPlan { get; set; } = false;

        public int? TrialDurationDays { get; set; }

        public bool IsPopular { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;

        [MaxLength(20)]
        public string ThemeKey { get; set; } = "blue"; // green, blue, purple

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public ICollection<SubscriptionPlanFeature> Features { get; set; } = new List<SubscriptionPlanFeature>();
        public ICollection<SubscriptionPlanPrice> Prices { get; set; } = new List<SubscriptionPlanPrice>();
        public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    }
}
