namespace Negri.Wot
{
    /// <summary>
    /// Reasons for a clan being disabled
    /// </summary>
    public enum DisabledReason
    {
        NotDisabled = 0,
        NotActive = 1,
        TooSmall = 2,
        Banned = 3,
        Request = 4,
        Disbanded = 5,
        Unknown = 6
    }
}