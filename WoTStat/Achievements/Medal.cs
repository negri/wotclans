
namespace Negri.Wot.Achievements
{
    /// <summary>
    /// An Achievement (medal, ribbon, award etc) on the game
    /// </summary>
    public class Medal
    {
        public Medal() { }

        /// <summary>
        /// The WG Code for the Medal
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// The Medal Name
        /// </summary>
        public string Name { get; set; }

        public Category Category { get; set; }

        public Type Type { get; set; }

        public Section Section { get; set; }

        public string Description { get; set; }

        public string Condition { get; set; }

        public string HeroInformation { get; set; }

    }
}
