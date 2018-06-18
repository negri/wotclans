namespace Negri.Wot.WgApi
{
    /// <summary>
    /// Um erro de chamada na API
    /// </summary>
    public class Error
    {
        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        public override string ToString()
        {
            return $"WG API Error: {Code} - {Message} - at {Field} - with value '{Value}'";
        }

        /// <summary>
        /// Codigo do Erro
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// A mensagem do erro
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// O campo com o erro
        /// </summary>
        public string Field { get; set; }

        /// <summary>
        /// O valor errado
        /// </summary>
        public string Value { get; set; }

    }
}