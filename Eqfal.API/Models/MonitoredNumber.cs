using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace Eqfal.API.Models
{
    public class MonitoredNumber
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        
        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required, MaxLength(30)]
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// الرقم بصيغة قياسية موحّدة (أرقام فقط، بادئة دولية).
        /// يُستخدَم للبحث المباشر في SQL بدلاً من تحميل كل الأرقام للـ RAM.
        /// مثال: "218920012345"
        /// </summary>
        [MaxLength(30)]
        public string NormalizedPhone { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ContactName { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<Operation> Operations { get; set; } = new List<Operation>();
    }
}

