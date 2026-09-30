using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eqfal.API.Models
{
    public class DiscountCode
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty; // يُخزن بحروف كبيرة بعد Trim

        public DiscountType DiscountType { get; set; } = DiscountType.Percentage;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Value { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public int? MaxUses { get; set; }

        public int TimesUsed { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        [Required]
        public Guid MarketerId { get; set; }

        [ForeignKey(nameof(MarketerId))]
        public Marketer Marketer { get; set; } = null!;

        public DiscountType CommissionType { get; set; } = DiscountType.Percentage;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CommissionValue { get; set; } = 10;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public bool IsUsable => IsActive &&
                                (ExpiresAt == null || ExpiresAt >= DateTime.UtcNow) &&
                                (MaxUses == null || TimesUsed < MaxUses);
    }
}
