using Microsoft.Xna.Framework;
using WorldRenderLib.Wrapping;

namespace WorldRenderLib.Wrappers
{
    public interface ITileDrawingWrapper
    {
        [TargetMember("_specialsCount")]
        int[] SpecialsCount { get; set; }
        
        [TargetMember("_specialPositions")]
        Point[][] SpecialPositions { get; set; }
    }
}
