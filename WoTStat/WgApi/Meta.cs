using Newtonsoft.Json;

namespace Negri.Wot.WgApi
{
    /// <summary>
    /// Metadados das respostas
    /// </summary>
    public class Meta
    {
        /// <summary>
        /// Dados retornados
        /// </summary>
        public long Count { get; set; }

        /// <summary>
        /// De um total disponível
        /// </summary>
        public long Total { get; set; }

        /// <summary>
        /// Limit per page
        /// </summary>
        public int Limit { get; set; }

        /// <summary>
        /// Current page
        /// </summary>
        public int? Page { get; set; }

        /// <summary>
        /// Total pages
        /// </summary>
        [JsonProperty("page_total")]
        public int PageTotal { get; set; }
    }
}