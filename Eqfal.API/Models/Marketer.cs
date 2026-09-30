using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eqfal.API.Models
{
    public class Marketer
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? Email { get; set; }

        [Required]
        public Guid CategoryId { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public MarketerCategory Category { get; set; } = null!;

        public DiscountType CommissionType { get; set; } = DiscountType.Percentage;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CommissionValue { get; set; } = 10;

        public bool IsActive { get; set; } = true;

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<DiscountCode> DiscountCodes { get; set; } = new List<DiscountCode>();
        public ICollection<MarketerTransaction> Transactions { get; set; } = new List<MarketerTransaction>();
    }
}
