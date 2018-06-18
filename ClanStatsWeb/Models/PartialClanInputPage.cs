using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Negri.Wot.Models
{
    /// <summary>
    /// O input no formulário
    /// </summary>
    public class PartialClanInputPage
    {
        private static readonly Random Random = new Random();

        public PartialClanInputPage()
        {
            Tag = "ABC123";
        }

        /// <summary>
        /// Tag desejado
        /// </summary>
        [DisplayName(@"Setup Name")]
        [Required(AllowEmptyStrings = false, ErrorMessage = @"A tag for this setup is required!")]
        [StringLength(20, MinimumLength = 3)]
        public string Tag { get; set; }
        

        /// <summary>
        /// Texto bruto
        /// </summary>
        [DisplayName(@"Clans Information")]
        [Required(AllowEmptyStrings = false, ErrorMessage = @"The clans informations are required!")]
        [StringLength(1024*1024, MinimumLength = 50)]
        public string RawText { get; set; }

        
    }
}