using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.Graphics.Light;
using WorldRenderLib.Wrapping;

namespace WorldRenderLib.Wrappers
{
    public interface ILightingWrapper
    {
        [TargetMember("_activeEngine")]
        ILightingEngine ActiveEngine { get; set; }
    }
}
