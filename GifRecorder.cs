using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using System;
using System.IO;
using System.Threading;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Graphics.Capture;
using Terraria.ID;
using Terraria.UI.Chat;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace WorldRenderLib
{
    public static class GifRecorder
    {
        public static RecordingSession? CurrentSession;

        public static Texture2D GetButtonIcon()
        {
            short itemId = CurrentSession is null ? 
                    CaptureInterface.EdgeAPinned && CaptureInterface.EdgeBPinned ? ItemID.EmeraldGemsparkBlock 
                    : ItemID.AmberGemsparkBlock
                : CurrentSession.Exporting ? ItemID.AmberGemsparkBlock 
                : ItemID.RubyGemsparkBlock;
            Asset<Texture2D> asset = TextureAssets.Item[itemId];
            if (!asset.IsLoaded)
                asset = Main.Assets.Request<Texture2D>("Images/Item_" + itemId, AssetRequestMode.ImmediateLoad);

            return asset.Value;
        }

        public static string GetButtonText()
        {
            if (CurrentSession is null)
            {
                if (!CaptureInterface.EdgeAPinned || !CaptureInterface.EdgeBPinned)
                    return "Set frame to record";

                return "Start GIF recording";
            }
            else
            {
                return CurrentSession.Exporting ? "Exporting GIF..." : "Stop GIF recording";
            }
        }

        public static void HandleButtonClick()
        {
            if (CurrentSession is null)
            {
                if (!CaptureInterface.EdgeAPinned || !CaptureInterface.EdgeBPinned)
                    return;

                SoundEngine.PlaySound(SoundID.MenuTick);

                CurrentSession = new();
                CurrentSession.Start();
                return;
            }
            if (CurrentSession.Exporting)
                return;

            SoundEngine.PlaySound(SoundID.MenuTick);
            CurrentSession.Finish();
        }

        public static void Draw()
        {
            CurrentSession?.Draw();
        }

        public static void Update()
        {
            if (CurrentSession is not null && CurrentSession.Finished)
                CurrentSession = null;

            CurrentSession?.Update();
        }
    }

    public class RecordingSession
    {
        public WorldRenderer Renderer;
        public Rectangle Area;
        public Rgba32[] RenderBuffer = Array.Empty<Rgba32>();
        public Image<Rgba32> Image = null!;

        public DateTime StartTime;
        public float GifDelay = 0;
        public float GifFps = 40;
        public uint Timeout;
        public bool Active;
        public bool Exporting;
        public bool Finished;
        public float TotalDuration;

        public RecordingSession()
        {
            Renderer = new();
        }

        public void Start()
        {
            Area = CaptureInterface.GetArea();
            Area = new(Area.X * 16, Area.Y * 16, Area.Width * 16, Area.Height * 16);
            Image = new(Area.Width, Area.Height);
            Image.Metadata.GetGifMetadata().RepeatCount = 0;
            Renderer.WorldRectangle = Area;
            Renderer.Enabled = true;
            Timeout = 20;
            Active = true;
            StartTime = DateTime.Now;
        }

        public void Finish()
        {
            if (Exporting)
                return;

            Renderer.Enabled = false;
            Image.Frames.RemoveFrame(0);
            Exporting = true;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                DateTime dateTime = DateTime.Now.ToLocalTime();
                string outputName = string.Concat(new string[]
                {
                    "Capture ",
                    dateTime.Year.ToString("D4"),
                    "-",
                    dateTime.Month.ToString("D2"),
                    "-",
                    dateTime.Day.ToString("D2"),
                    " ",
                    dateTime.Hour.ToString("D2"),
                    "_",
                    dateTime.Minute.ToString("D2"),
                    "_",
                    dateTime.Second.ToString("D2")
                });

                string filename = string.Concat(new string[]
                {
                    Main.SavePath,
                    Path.DirectorySeparatorChar.ToString(),
                    "Captures",
                    Path.DirectorySeparatorChar.ToString(),
                    outputName,
                    ".gif"
                });

                Image.SaveAsGif(filename, new() { ColorTableMode = GifColorTableMode.Global });
                Finished = true;
                Image.Dispose();
            });
        }

        public void Update()
        {
            if (!Renderer.IsReady || Exporting)
                return;

            GifDelay += (float)Main.gameTimeCache.ElapsedGameTime.TotalMilliseconds;

            if (GifDelay < 1000 / GifFps)
                return;

            float fps = Math.Min(GifFps, (float)Main.frameRate - 1);
            float delayMs = (int)(100 / fps) * 10;
            GifDelay -= delayMs;

            delayMs = Math.Max(delayMs, (float)Main.gameTimeCache.ElapsedGameTime.TotalMilliseconds);

            if (Timeout > 0)
            {
                Timeout--;
                return;
            }

            RenderTarget2D render = Renderer.RenderResult;
            int renderSize = render.Width * render.Height;
            if (RenderBuffer.Length != renderSize)
                RenderBuffer = new Rgba32[renderSize];

            render.GetData(RenderBuffer);

            ImageFrame frame = Image.Frames.AddFrame(RenderBuffer);
            GifFrameMetadata gifData = frame.Metadata.GetGifMetadata();
            gifData.FrameDelay = (int)delayMs / 10;
            gifData.DisposalMethod = GifDisposalMethod.NotDispose;
            TotalDuration += delayMs;
        }

        public void Draw()
        {
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.CurrentWantedZoomMatrix);
            PlayerInput.SetZoom_Context();

            Rectangle result;
            if (!Main.mapFullscreen)
            {
                result = Main.ReverseGravitySupport(Area);
                result.Offset(-(int)Main.screenPosition.X, -(int)Main.screenPosition.Y);
            }
            else
            {
                Vector2 tl = MapToScreen(Area.Location.ToVector2() / 16);
                Vector2 br = MapToScreen((Area.Location.ToVector2() + Area.Size()) / 16);
                result = new((int)tl.X, (int)tl.Y, (int)(br.X - tl.X), (int)(br.Y - tl.Y));
            }

            Graphics.DrawRect(result, Color.Yellow, 2);
            TimeSpan time = DateTime.Now - StartTime;

            string text = Timeout > 0 ? "Starting..." : Exporting ? "Exporting GIF..." : $"Recording. {(int)time.TotalMinutes}:{time.Seconds:00}";

            ChatManager.DrawColorCodedStringWithShadow(
                Main.spriteBatch,
                FontAssets.MouseText.Value,
                text, 
                result.Location.ToVector2() + new Vector2(0, result.Height),
                Color.Yellow,
                0f,
                Vector2.Zero,
                Vector2.One
                );

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
            PlayerInput.SetZoom_UI();
        }

        static Vector2 MapToScreen(Vector2 mapTilePos)
        {
            Vector2 screenCenter = new Vector2(PlayerInput.RealScreenWidth, PlayerInput.RealScreenHeight) / 2;
            Vector2 mapScreenPos = screenCenter - Main.mapFullscreenPos * Main.mapFullscreenScale;

            mapTilePos *= Main.mapFullscreenScale;
            mapTilePos += mapScreenPos;

            return mapTilePos;
        }
    }
}
