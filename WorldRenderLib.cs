using Terraria.Graphics.Effects;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace WorldRenderLib
{
	public class WorldRenderLib : Mod
	{
		public static WorldRenderLib Instance => ModContent.GetInstance<WorldRenderLib>();

		public static Effect GetEffect(string path) 
		{
			return new(Main.graphics.GraphicsDevice, Instance.GetFileBytes(path));
		}
    }
}