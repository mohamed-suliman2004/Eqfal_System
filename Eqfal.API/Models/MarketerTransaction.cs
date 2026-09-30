using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eqfal.API.Models
{
    public enum MarketerTransactionType
    {
        Accrual = 1, // استحقاق عمولة (+)
        Payout = 2   // سداد مستحقات (-)
    }

    public class MarketerTransaction
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid MarketerId { get; set; }

        [ForeignKey(nameof(MarketerId))]
        public Marketer Marketer { get; set; } = null!;

        [Required]
        public MarketerTransactionType Type { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; } // دائماً موجب، والإشارة تفهم من Type

        // حقول الاستحقاق (Accrual snapshots)
        public int? UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [MaxLength(150)]
        public string? UserName { get; set; }

        public Guid? SubscriptionPaymentId { get; set; }

        public Guid? DiscountCodeId { get; set; }

        [ForeignKey(nameof(DiscountCodeId))]
        public DiscountCode? DiscountCode { get; set; }

        [MaxLength(50)]
        public string? DiscountCodeText { get; set; }

        [MaxLength(100)]
        public string? PlanName { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SubscriptionAmount { get; set; }

        // حقول السداد (Payout)
        [MaxLength(100)]
        public string? ReferenceNumber { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
