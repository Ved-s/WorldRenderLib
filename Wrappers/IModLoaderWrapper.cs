using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorldRenderLib.Wrapping;

namespace WorldRenderLib.Wrappers
{
    public interface IModLoaderWrapper
    {
        [TargetMember("isLoading")]
        bool IsLoading { get; }
    }
}
