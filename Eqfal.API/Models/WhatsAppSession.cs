using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eqfal.API.Models
{
    public class WhatsAppSession
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        
        [ForeignKey("UserId")]
        public User? User { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "غير متصل";

        [MaxLength(20)]
        public string? PairingCode { get; set; }

        public DateTime? LastConnected { get; set; }
    }
}