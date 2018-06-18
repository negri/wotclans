namespace Negri.Wot.Tanks
{
    /// <summary>
    /// Origem do número WN8 esperado do tanque
    /// </summary>
    public enum Wn8TankExpectedValuesOrigin
    {
        /// <summary>
        /// A fonte original, sem alterações
        /// </summary>        
        Source = 0,

        /// <summary>
        /// Copiado de um tanque similar
        /// </summary>
        Surrogate = 1,

        /// <summary>
        /// Extrapolado a partir de médias para tier e tipo e de uma outra fonte (por exemplo médias do WoTClans)
        /// </summary>
        Extrapolated = 2,

        /// <summary>
        /// Média de dois tanques similares
        /// </summary>
        SurrogateTwo = 3,

        /// <summary>
        /// Average on the same tier and type
        /// </summary>
        TierType = 4
    }
}