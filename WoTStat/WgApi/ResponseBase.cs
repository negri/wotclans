namespace Negri.Wot.WgApi
{
    /// <summary>
    ///     Resposta de API da Wargaming
    /// </summary>
    public abstract class ResponseBase
    {
        /// <summary>
        ///     Status "ok" ou "error"
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        ///     Meta informação da resposta
        /// </summary>
        public Meta Meta { get; set; }

        /// <summary>
        ///     Erro ocorrido
        /// </summary>
        public Error Error { get; set; }

        /// <summary>
        ///     Se há algo errado
        /// </summary>
        public bool IsError => (Status ?? string.Empty) != "ok";
    }
}