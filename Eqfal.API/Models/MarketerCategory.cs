using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eqfal.API.Models
{
    public class MarketerCategory
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Description { get; set; }

        public DiscountType DefaultCommissionType { get; set; } = DiscountType.Percentage;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DefaultCommissionValue { get; set; } = 10;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Marketer> Marketers { get; set; } = new List<Marketer>();
    }
}
