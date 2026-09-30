using System;
using System.ComponentModel.DataAnnotations;

namespace Eqfal.API.Models
{
    public class SubscriptionSettings
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public int GracePeriodDays { get; set; } = 3; // افتراضياً 3 أيام

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
