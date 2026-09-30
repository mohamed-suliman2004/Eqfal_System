using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Eqfal.API.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string UsernameEmail { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Phone { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public SuspensionReason? SuspensionReason { get; set; }
        public string? SuspensionNote { get; set; }
        public DateTime? SuspendedAt { get; set; }

        public string? ResetOtp { get; set; }
        public DateTime? ResetOtpExpiry { get; set; }

        [MaxLength(500)]
        public string? FcmToken { get; set; }

        // Navigation Properties
        public WhatsAppSession? WhatsAppSession { get; set; }
        public ICollection<MonitoredNumber> MonitoredNumbers { get; set; } = new List<MonitoredNumber>();
        public ICollection<DynamicKeyword> DynamicKeywords { get; set; } = new List<DynamicKeyword>();
        public ICollection<Operation> Operations { get; set; } = new List<Operation>();
        public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public ICollection<SubscriptionPayment> SubscriptionPayments { get; set; } = new List<SubscriptionPayment>();
    }

    public enum SuspensionReason
    {
        Manual = 1,
        SubscriptionExpired = 2
    }
}
