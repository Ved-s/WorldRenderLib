using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameContent;

namespace WorldRenderLib
{
    public static class Graphics
    {
        public static void DrawLineWorld(Vector2 a, Vector2 b, Color color)
        {
            DrawLine(a - Main.screenPosition, b - Main.screenPosition, color);
        }
        public static void DrawLine(Vector2 a, Vector2 b, Color color)
        {
            Vector2 diff = b - a;
            float angle = MathF.Atan2(diff.Y, diff.X);
            float length = diff.Length();

            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, a, new(0, 0, 1, 1), color, angle, Vector2.Zero, new Vector2(length, 1), SpriteEffects.None, 0f);
        }

        public static void DrawRectWorld(Rectangle rect, Color color, int thickness = 1)
        {
            rect.Location = (rect.Location.ToVector2() - Main.screenPosition).ToPoint();
            DrawRect(rect, color, thickness);
        }
        public static void DrawRect(Rectangle rect, Color color, int thickness = 1)
        {
            if (color.A == 0)
                return;

            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X + thickness, rect.Y, rect.Width - thickness, thickness), color);
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Y, thickness, rect.Height - thickness), color);

            if (rect.Height > thickness)
                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Bottom - thickness, Math.Max(thickness, rect.Width - thickness), thickness), color);

            if (rect.Width > thickness)
                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.Right - thickness, rect.Y + thickness, thickness, Math.Max(thickness, rect.Height - thickness)), color);
        }

        public static void DrawDotWorld(Vector2 pos, Color color, float size = 10)
        {
            DrawDot(pos - Main.screenPosition, color, size);
        }
        public static void DrawDot(Vector2 pos, Color color, float size = 10)
        {
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, pos, new Rectangle(0, 0, 1, 1), color, 0f, new(.5f), size, SpriteEffects.None, 0);
        }
    }
}
