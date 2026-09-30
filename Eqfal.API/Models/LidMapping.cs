using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eqfal.API.Models
{
    [Table("LidMappings")]
    public class LidMapping
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Lid { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string RealPhone { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? ContactName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
