using System.Collections.Generic;
using Negri.Wot.WgApi;

namespace Negri.Wot.Tanks
{
    public static class TankTypeExtensions
    {
        public static IEnumerable<TankType> GetGameTankTypes()
        {
            yield return TankType.Light;
            yield return TankType.Medium;
            yield return TankType.Heavy;
            yield return TankType.TankDestroyer;
            yield return TankType.Artillery;
        }
    }

}
