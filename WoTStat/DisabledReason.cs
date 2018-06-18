namespace Negri.Wot
{
    /// <summary>
    /// Razões para um clã estar desabilidado no sistema
    /// </summary>
    public enum DisabledReason
    {
        NotDisabled = 0,
        NotActive = 1,
        TooSmall = 2,
        Banned = 3,
        Request = 4,
        Disbanded = 5,
        Unknow = 6
    }
}