using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eqfal.API.Models
{
    public class Operation
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        
        [ForeignKey("UserId")]
        public User? User { get; set; }

        public int? MonitoredNumberId { get; set; }
        
        [ForeignKey("MonitoredNumberId")]
        public MonitoredNumber? MonitoredNumber { get; set; }

        [MaxLength(50)]
        public string? SenderNumber { get; set; }

        [MaxLength(50)]
        public string? ReceiverNumber { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Amount { get; set; }

        [MaxLength(50)]
        public string? Currency { get; set; } = "";

        [MaxLength(200)]
        public string? Party { get; set; }

        [NotMapped]
        public string? Source { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "مسودة";

        public string? RawMessage { get; set; }

        public bool IsReviewed { get; set; } = false;

        public bool IsOutgoing { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}