using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Negri.Wot.Models
{
    /// <summary>
    /// Modelo para a página que lista as bandeiras
    /// </summary>
    public class FlagsPage
    {
        /// <summary>
        /// Os códigos (2 letras) de todas as bandeiras
        /// </summary>
        public string[] Codes { get; set; } = new string[0];
    }
}