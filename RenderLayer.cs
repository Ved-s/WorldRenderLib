using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace WorldRenderLib
{
    internal class RenderLayer : IDisposable
    {
        public RenderTarget2D? RenderTarget;
        public Vector2 LastRenderScreenPos;

        private WorldRenderer Renderer;
        public Action? Draw { get; init; }
        public Action? PreDraw { get; init; }

        public RenderLayer(WorldRenderer renderer)
        {
            Renderer = renderer;
        }

        public void PrepareAndSetTarget()
        {
            const int AddSize = 32;

            int rtWidth = Renderer.WorldRectangle.Width + Renderer.OffScreenRender * 2;
            int rtHeight = Renderer.WorldRectangle.Height + Renderer.OffScreenRender * 2;

            if (RenderTarget is null || RenderTarget.Width < rtWidth || RenderTarget.Height < rtHeight)
            {
                RenderTarget?.Dispose();
                RenderTarget = new(Main.instance.GraphicsDevice, rtWidth + AddSize, rtHeight + AddSize);
            }

            Main.instance.GraphicsDevice.SetRenderTarget(RenderTarget);
            LastRenderScreenPos = Main.screenPosition - new Vector2(Renderer.OffScreenRender);
        }

        public void DrawLayer()
        {
            const int AddSize = 32;

            int rtWidth = Renderer.WorldRectangle.Width + Renderer.OffScreenRender * 2;
            int rtHeight = Renderer.WorldRectangle.Height + Renderer.OffScreenRender * 2;

            if (RenderTarget is null || RenderTarget.Width < rtWidth || RenderTarget.Height < rtHeight)
            {
                RenderTarget?.Dispose();
                RenderTarget = new(Main.instance.GraphicsDevice, rtWidth + AddSize, rtHeight + AddSize);
            }

            Main.instance.GraphicsDevice.SetRenderTarget(RenderTarget);
            PreDraw?.Invoke();

            Main.instance.GraphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin();

            try
            {
                Draw?.Invoke();
            }
            catch (Exception e)
            {
                if (!Main.ignoreErrors)
                {
                    throw;
                }
                TimeLogger.DrawException(e);
            }

            Main.spriteBatch.End();
            Main.instance.GraphicsDevice.SetRenderTarget(null);
            LastRenderScreenPos = Main.screenPosition - new Vector2(Renderer.OffScreenRender);
        }

        public void DrawResult(SpriteBatch spriteBatch)
        {
            if (RenderTarget is null)
                return;

            spriteBatch.Draw(RenderTarget, LastRenderScreenPos - Main.screenPosition, Color.White);
        }

        public void Dispose()
        {
            RenderTarget?.Dispose();
        }
    }
}
