using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using ReLogic.Content;
using ReLogic.Graphics;
using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Events;
using Terraria.GameContent.Liquid;
using Terraria.GameInput;
using Terraria.Graphics;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Light;
using Terraria.ID;
using Terraria.ModLoader;
using WorldRenderLib.Wrappers;
using WorldRenderLib.Wrapping;

// TODO: fix undeground bg
// TODO: fix water transparency
// TODO: fix water color
// TODO: fix issues with black

namespace WorldRenderLib
{
    public sealed class WorldRenderer : INeedRenderTargetContent, IDisposable
    {
        [MemberNotNullWhen(true, nameof(CurrentRenderer))]
        public static bool RenderingAny { get; private set; }
        public static WorldRenderer? CurrentRenderer { get; private set; }

        public bool Rendering { get; private set; }
        public bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled == value)
                    return;

                enabled = value;
                if (value)
                    Main.ContentThatNeedsRenderTargets.Add(this);
                else
                    Main.ContentThatNeedsRenderTargets.Remove(this);
                Ready = false;
                FirstDraw = true;
            }
        }
        public object? Owner { get; set; }

        public Func<bool>? CheckValid;

        public Rectangle RenderResultRectangle { get; private set; }

        public RenderTarget2D? RenderResult { get; private set; }
        private RenderTarget2D? RenderResultSwap;

        public Rectangle WorldRectangle { get; set; }

        public Vector2 RealScreenPosition { get; private set; }
        public Point RealScreenSize { get; private set; }

        [MemberNotNullWhen(true, nameof(RenderResult))]
        public bool IsReady => Ready;
        private bool Ready = false;

        private readonly RenderLayer SolidTileLayer;
        private readonly RenderLayer TileLayer;
        private readonly RenderLayer BackgroundLayer;
        private readonly RenderLayer BlackLayer;
        private readonly RenderLayer WallLayer;
        private readonly RenderLayer WaterLayer;
        private readonly RenderLayer BackWaterLayer;

        internal int OffScreenRender = 16;

        private bool enabled;

        private LiquidRenderer LiquidRenderer = new();
        private LightingEngine Lighting = new();
        private SceneMetrics SceneMetrics = new();
        private SpriteViewMatrix GameViewMatrix = new(Main.instance.GraphicsDevice);

        private int[] SpecialTileCount = new int[12];
        private Point[][] SpecialTilePositions = new Point[12][];
        private float[] BGAlphaFrontLayer = new float[14];
        private float[] BGAlphaFarBackLayer = new float[14];

        private int BackgroundDelay;
        private int BackgroundStyle;

        private int UndergroundBackground;
        private int OldUndergroundBackground;
        private float UndergroundBackgroundTransition;

        private int Timeout = 1;
        private int ValidationTimeout = 20;
        private bool FirstDraw = true;
        private int RenderCount = 0;

        private static readonly IMainWrapper MainWrapper = TypeWrapping.CreateWrapper<IMainWrapper>(Main.instance);
        private static readonly ILightingWrapper LightingWrapper = TypeWrapping.CreateWrapper<ILightingWrapper>(typeof(Lighting), null!);
        private static readonly ITileDrawingWrapper TilesRenderingWrapper = TypeWrapping.CreateWrapper<ITileDrawingWrapper>(Main.instance.TilesRenderer);

        private static readonly DepthStencilState DepthBufferStencilState = new DepthStencilState
        {
            DepthBufferEnable = true
        };

        public WorldRenderer()
        {
            for (int i = 0; i < SpecialTilePositions.Length; i++)
            {
                SpecialTilePositions[i] = new Point[9000];
            }
            LiquidRenderer._liquidTextures = LiquidRenderer.Instance._liquidTextures;

            SolidTileLayer = new(this)
            {
                Draw = delegate
                {
                    Main.instance.TilesRenderer.PreDrawTiles(true, false, false);
                    Main.instance.TilesRenderer.Draw(true, false, false, -1);
                }
            };
            TileLayer = new(this)
            {
                Draw = delegate
                {
                    for (int i = 0; i < SpecialTileCount.Length; i++)
                        SpecialTileCount[i] = 0;

                    Main.instance.TilesRenderer.PreDrawTiles(false, false, false);
                    Main.instance.TilesRenderer.Draw(false, false, false, -1);
                }
            };
            BackgroundLayer = new(this)
            {
                Draw = MainWrapper.DrawBackground
            };
            BlackLayer = new(this)
            {
                PreDraw = delegate
                {
                    Main.instance.GraphicsDevice.DepthStencilState = DepthBufferStencilState;
                },
                Draw = delegate
                {
                    MainWrapper.DrawBlack(false);
                }
            };
            WallLayer = new(this)
            {
                PreDraw = delegate
                {
                    Main.instance.GraphicsDevice.DepthStencilState = DepthBufferStencilState;
                },
                Draw = delegate
                {
                    Main.tileBatch.Begin();
                    Main.instance.WallsRenderer.DrawWalls();
                    Main.tileBatch.End();
                }
            };

            WaterLayer = new(this)
            {
                Draw = delegate
                {
                    MainWrapper.DrawWaters(false);
                }
            };
            BackWaterLayer = new(this)
            {
                Draw = delegate
                {

                    Main.tileBatch.Begin(SpriteSortMode.Texture, BlendState.AlphaBlend);
                    MainWrapper.DrawWaters(true);
                    Main.tileBatch.End();
                }
            };
        }

        public void PrepareRenderTarget(GraphicsDevice device, SpriteBatch spriteBatch)
        {
            if (Main.gameMenu)
            {
                Ready = false;
                return;
            }

            if (CheckValid is not null && Enabled)
            {
                ValidationTimeout--;
                if (ValidationTimeout <= 0)
                {
                    ValidationTimeout = 20;
                    if (!CheckValid())
                        Enabled = false;
                }
            }

            if (WorldRectangle.Width <= 0 || WorldRectangle.Height <= 0)
            {
                Ready = false;
                return;
            }

            if (Timeout > 0)
            {
                Timeout--;
                return;
            }

            RenderingAny = true;
            Rendering = true;
            CurrentRenderer = this;

            PrepareTarget(ref RenderResultSwap, device, false);

            IEnumerator saveRestoreRender = SaveRestoreMainRendering().GetEnumerator();
            saveRestoreRender.MoveNext();

            Main.LocalPlayer.Center = WorldRectangle.Center.ToVector2();

            int rc = Main.renderCount;
            Lighting.ProcessArea(new Rectangle(WorldRectangle.X / 16, WorldRectangle.Y / 16, WorldRectangle.Width / 16, WorldRectangle.Height / 16));
            Main.renderCount = rc;
            Main.LocalPlayer.UpdateBiomes();

            Main.screenWidth = WorldRectangle.Width + OffScreenRender * 2;
            Main.screenHeight = WorldRectangle.Height + OffScreenRender * 2;
            Main.screenPosition = WorldRectangle.Location.ToVector2() - new Vector2(OffScreenRender) + new Vector2(Main.offScreenRange);
            Main.offScreenRange = OffScreenRender;

            if (FirstDraw || RenderCount == 0)
            {
                BlackLayer.DrawLayer();
                WallLayer.DrawLayer();
            }

            if (FirstDraw || RenderCount == 1)
            {
                BackgroundLayer.DrawLayer();
                WaterLayer.DrawLayer();
            }

            if (FirstDraw || RenderCount == 2)
            {
                TileLayer.DrawLayer();
                BackWaterLayer.DrawLayer();
            }

            if (FirstDraw || RenderCount == 3)
                SolidTileLayer.DrawLayer();

            Main.screenWidth = WorldRectangle.Width;
            Main.screenHeight = WorldRectangle.Height;
            Main.screenPosition = WorldRectangle.Location.ToVector2();
            Main.offScreenRange = 0;

            device.SetRenderTarget(RenderResultSwap);
            device.Clear(Color.Black);
            RenderBiomeBackground();

            spriteBatch.Begin();

            Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.InWorldUI, false);
            BackWaterLayer.DrawResult(spriteBatch);
            Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.BackgroundWater, false);
            BackgroundLayer.DrawResult(spriteBatch);
            Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.Background, false);

            MainWrapper.CacheNPCDraws();
            MainWrapper.CacheProjDraws();
            MainWrapper.DrawCachedNPCs(Main.instance.DrawCacheNPCsMoonMoon, true);

            BlackLayer.DrawResult(spriteBatch);

            WallLayer.DrawResult(spriteBatch);

            MainWrapper.DrawWoF();
            MainWrapper.DrawBackGore();
            MoonlordDeathDrama.DrawPieces(Main.spriteBatch);
            MoonlordDeathDrama.DrawExplosions(Main.spriteBatch);
            MainWrapper.DrawCachedNPCs(Main.instance.DrawCacheNPCsBehindNonSolidTiles, true);

            TileLayer.DrawResult(spriteBatch);
            spriteBatch.End();
            Main.instance.TilesRenderer.PostDrawTiles(false, false, false);
            spriteBatch.Begin();

            Main.instance.waterfallManager.FindWaterfalls(false);
            Main.instance.waterfallManager.Draw(Main.spriteBatch);

            SolidTileLayer.DrawResult(spriteBatch);
            spriteBatch.End();
            Main.instance.TilesRenderer.PostDrawTiles(true, false, false);

            MainWrapper.DrawPlayers_BehindNPCs();
            MainWrapper.DoDraw_DrawNPCsOverTiles();

            spriteBatch.Begin();
            Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.TilesAndNPCs, false);
            spriteBatch.End();
            SystemLoader.PostDrawTiles();

            MainWrapper.SortDrawCacheWorms();
            //this.DrawSuperSpecialProjectiles(this.DrawCacheFirstFractals, true);
            MainWrapper.DrawCachedProjs(Main.instance.DrawCacheProjsBehindProjectiles, true);
            MainWrapper.DrawProjectiles();
            MainWrapper.DrawPlayers_AfterProjectiles();
            MainWrapper.DrawCachedProjs(Main.instance.DrawCacheProjsOverPlayers, true);
            Main.spriteBatch.Begin();

            MainWrapper.DrawCachedNPCs(Main.instance.DrawCacheNPCsOverPlayers, false);
            Main.instance.DrawItems();
            MainWrapper.DrawRain();
            MainWrapper.DrawGore();
            spriteBatch.End();
            MainWrapper.DrawDust();
            spriteBatch.Begin();
            Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.Entities, false);

            WaterLayer.DrawResult(spriteBatch);
            Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.ForegroundWater, false);

            MainWrapper.DrawCachedProjs(Main.instance.DrawCacheProjsOverWiresUI, false);
            Main.instance.DrawInfernoRings();

            spriteBatch.End();
            Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.All, false);

            device.SetRenderTarget(null);
            RenderResultRectangle = WorldRectangle;

            (RenderResultSwap, RenderResult) = (RenderResult, RenderResultSwap);
            saveRestoreRender.MoveNext();

            CurrentRenderer = null;
            Rendering = false;
            RenderingAny = false;
            Ready = true;
            FirstDraw = false;

            RenderCount++;
            RenderCount %= 4;
        }

        void RenderBiomeBackground()
        {
            double bgParallax = 0.1;
            int bgTopY = (int)((double)(0f - Main.screenPosition.Y) / (Main.worldSurface * 16.0 - 600.0) * 200.0);
            int bgStartX = (int)(0.0 - Math.IEEERemainder((double)Main.screenPosition.X * bgParallax, (double)Main.backgroundWidth[Main.background]) - (double)(Main.backgroundWidth[Main.background] / 2));
            int bgLoops = Main.screenWidth / Main.backgroundWidth[Main.background] + 2;

            MainWrapper.SetBackColor(new Main.InfoToSetBackColor
            {
                isInGameMenuOrIsServer = (Main.gameMenu || Main.netMode == NetmodeID.Server),
                CorruptionBiomeInfluence = Main.SceneMetrics.EvilTileCount / (float)SceneMetrics.CorruptionTileMax,
                CrimsonBiomeInfluence = Main.SceneMetrics.BloodTileCount / (float)SceneMetrics.CrimsonTileMax,
                JungleBiomeInfluence = Main.SceneMetrics.JungleTileCount / (float)SceneMetrics.JungleTileMax,
                MushroomBiomeInfluence = Main.SmoothedMushroomLightInfluence,
                GraveyardInfluence = Main.GraveyardVisualIntensity,
                BloodMoonActive = (Main.bloodMoon || Main.SceneMetrics.BloodMoonMonolith),
                LanternNightActive = LanternNight.LanternsUp
            }, out Color sunColor, out Color moonColor);

            Main.spriteBatch.Begin(0, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.BackgroundViewMatrix.TransformationMatrix);
            if (Main.screenPosition.Y < Main.worldSurface * 16.0 + 16.0)
            {
                Asset<Texture2D> asset = TextureAssets.Background[Main.background];
                Rectangle destinationRectangle = new(bgStartX, bgTopY, asset.Width(), Math.Max(Main.screenHeight, asset.Height()));
                if (destinationRectangle.Bottom < asset.Height())
                {
                    int num7 = asset.Height() - destinationRectangle.Bottom;
                    destinationRectangle.Height += num7;
                }
                for (int j = 0; j < bgLoops; j++)
                {
                    destinationRectangle.X = bgStartX + asset.Width() * j;
                    Main.spriteBatch.Draw(asset.Value, destinationRectangle, Main.ColorOfTheSkies);
                }
                TimeLogger.DetailedDrawTime(6);
            }
            Main.spriteBatch.End();

            Main.spriteBatch.Begin(0, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.BackgroundViewMatrix.EffectMatrix);
            Main.SceneArea sceneArea2 = new Main.SceneArea
            {
                bgTopY = bgTopY,
                totalHeight = Main.screenHeight,
                totalWidth  = Main.screenWidth,
                SceneLocalScreenPositionOffset = Vector2.Zero
            };
            MainWrapper.DrawStarsInBackground(sceneArea2);
            if ((double)(Main.screenPosition.Y / 16f) < Main.worldSurface + 2.0)
            {
                MainWrapper.DrawSunAndMoon(sceneArea2, moonColor, sunColor, Main.SmoothedMushroomLightInfluence);
            }
            TimeLogger.DetailedDrawTime(7);

            Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.Sky, false);
            Main.spriteBatch.End();

            Vector2 vector3 = Main.screenPosition;
            int num19 = Main.screenWidth;
            int num8 = Main.screenHeight;
            Main.screenWidth = (int)((float)Main.screenWidth / Main.BackgroundViewMatrix.Zoom.X);
            Main.screenHeight = (int)((float)Main.screenHeight / Main.BackgroundViewMatrix.Zoom.Y);
            Main.screenPosition += Main.BackgroundViewMatrix.Translation;
            Matrix transformationMatrix = Main.BackgroundViewMatrix.TransformationMatrix;
            transformationMatrix.Translation -= Main.BackgroundViewMatrix.ZoomMatrix.Translation * new Vector3(1f, Main.BackgroundViewMatrix.Effects.HasFlag(SpriteEffects.FlipVertically) ? -1f : 1f, 1f);
            Main.spriteBatch.Begin(0, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, transformationMatrix);
            Main.CurrentFrameFlags.Hacks.CurrentBackgroundMatrixForCreditsRoll = transformationMatrix;
            MainWrapper.DrawBG();
            Main.screenWidth = num19;
            Main.screenHeight = num8;
            Main.screenPosition = vector3;
            Main.spriteBatch.End();
            Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.Landscape, true);
        }

        void PrepareTarget(ref RenderTarget2D? target, GraphicsDevice device, bool withOffScreen)
        {
            Rectangle targetSize = WorldRectangle;
            if (withOffScreen)
            {
                targetSize.X -= OffScreenRender;
                targetSize.Y -= OffScreenRender;
                targetSize.Width += 2 * OffScreenRender;
                targetSize.Height += 2 * OffScreenRender;
            }

            if (target is null || target.Width != targetSize.Width || target.Height != targetSize.Height)
            {
                try
                {
                    target?.Dispose();
                    target = new(device, targetSize.Width, targetSize.Height);
                }
                catch (Exception e)
                {
                    Debugger.Break();
                }
            }
        }

        IEnumerable SaveRestoreMainRendering()
        {
            int offScreen = Main.offScreenRange;
            int screenWidth = Main.screenWidth;
            int screenHeight = Main.screenHeight;
            Vector2 screenPos = Main.screenPosition;
            ILightingEngine lightingEngine = LightingWrapper.ActiveEngine;
            SpriteViewMatrix gameView = Main.GameViewMatrix;
            int[] specialsCount = TilesRenderingWrapper.SpecialsCount;
            Point[][] specialPositions = TilesRenderingWrapper.SpecialPositions;
            SceneMetrics sceneMetrics = Main.SceneMetrics;
            int myPlayer = Main.myPlayer;
            float[] bgAlphaFrontLayer = Main.bgAlphaFrontLayer;
            float[] bgAlphaFarBackLayer = Main.bgAlphaFarBackLayer;
            int bgDelay = Main.bgDelay;
            int bgStyle = Main.bgStyle;
            int undergroundBackground = Main.undergroundBackground;
            int oldUndergroundBackground = Main.oldUndergroundBackground;
            float ugBackTransition = Main.ugBackTransition;
            LiquidRenderer liquidRenderer = LiquidRenderer.Instance;

            RealScreenPosition = screenPos;
            RealScreenSize = new(screenWidth, screenHeight);

            Main.offScreenRange = 0;
            LightingWrapper.ActiveEngine = Lighting;
            Main.GameViewMatrix = GameViewMatrix;
            TilesRenderingWrapper.SpecialsCount = SpecialTileCount;
            TilesRenderingWrapper.SpecialPositions = SpecialTilePositions;
            Main.SceneMetrics = SceneMetrics;
            Main.myPlayer = 255;
            Main.bgAlphaFrontLayer = BGAlphaFrontLayer;
            Main.bgAlphaFarBackLayer = BGAlphaFarBackLayer;
            Main.bgDelay = BackgroundDelay;
            Main.bgStyle = BackgroundStyle;
            Main.undergroundBackground = UndergroundBackground;
            Main.oldUndergroundBackground = OldUndergroundBackground;
            Main.ugBackTransition = UndergroundBackgroundTransition;
            LiquidRenderer.Instance = LiquidRenderer;

            yield return null;

            BackgroundDelay = Main.bgDelay;
            BackgroundStyle = Main.bgStyle;
            UndergroundBackground = Main.undergroundBackground;
            OldUndergroundBackground = Main.oldUndergroundBackground;
            UndergroundBackgroundTransition = Main.ugBackTransition;

            Main.offScreenRange = offScreen;
            Main.screenWidth = screenWidth;
            Main.screenHeight = screenHeight;
            Main.screenPosition = screenPos;
            LightingWrapper.ActiveEngine = lightingEngine;
            Main.GameViewMatrix = gameView;
            TilesRenderingWrapper.SpecialsCount = specialsCount;
            TilesRenderingWrapper.SpecialPositions = specialPositions;
            Main.SceneMetrics = sceneMetrics;
            Main.myPlayer = myPlayer;
            Main.bgAlphaFrontLayer = bgAlphaFrontLayer;
            Main.bgAlphaFarBackLayer = bgAlphaFarBackLayer;
            Main.bgDelay = bgDelay;
            Main.bgStyle = bgStyle;
            Main.undergroundBackground = undergroundBackground;
            Main.oldUndergroundBackground = oldUndergroundBackground;
            Main.ugBackTransition = ugBackTransition;
            LiquidRenderer.Instance = liquidRenderer;
        }

        public void Dispose()
        {
            RenderResult?.Dispose();
            RenderResultSwap?.Dispose();

            SolidTileLayer.Dispose();
            TileLayer.Dispose();
            BackgroundLayer.Dispose();
            BlackLayer.Dispose();
            WallLayer.Dispose();
            WaterLayer.Dispose();
            BackWaterLayer.Dispose();
        }
    }
}
