//using Microsoft.Xna.Framework;
//using Microsoft.Xna.Framework.Graphics;
//using Mono.Cecil.Cil;
//using MonoMod.Cil;
//using ReLogic.Graphics;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using Terraria;
//using Terraria.GameContent;
//using Terraria.Graphics.Effects;
//using Terraria.ID;
//using Terraria.ModLoader;
//using WorldRenderLib.Wrappers;
//using WorldRenderLib.Wrapping;

//namespace WorldRenderLib
//{
//    [Autoload(false)]
//    internal class PortalGateHooks : GlobalProjectile
//    {
//        private static List<Projectile> Portals = new();

//        public override bool InstancePerEntity => true;

//        WorldRenderer OtherSide = null!;

//        static IPortalHelperWrapper PortalHelperWrapper = TypeWrapping.CreateWrapper<IPortalHelperWrapper>(typeof(PortalHelper), null!);
//        static Effect? PortalView;

//        Point OldRectSize;
//        Vector2 LastRenderRelativePlayer;
//        int LocalPLayerIndex = Main.myPlayer;

//        public PortalGateHooks() { }

//        public override void Load()
//        {
//            On.Terraria.Main.DrawInfernoRings += Main_DrawInfernoRings;
//        }

//        public override void Unload()
//        {
//            On.Terraria.Main.DrawInfernoRings -= Main_DrawInfernoRings;
//        }

//        public override GlobalProjectile NewInstance(Projectile target)
//        {
//            PortalGateHooks hooks = (PortalGateHooks)base.NewInstance(target);

//            hooks.OtherSide = new()
//            {
//                Enabled = true,
//                Owner = hooks
//            };

//            Portals.Add(target);

//            return hooks;
//        }

//        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
//            => entity.type == ProjectileID.PortalGunGate;

//        public void DrawPortal(Projectile projectile)
//        {
//            int portalId = (int)projectile.ai[1];
//            int otherPortalProjectile = PortalHelperWrapper.FoundPortals[projectile.owner, 1 - portalId];

//            if (otherPortalProjectile == -1)
//                return;

//            Projectile otherPortal = Main.projectile[otherPortalProjectile];

//            Player LocalPlayer = Main.player[LocalPLayerIndex];

//            Vector2 viewPoint = LocalPlayer.Center;

//            if (WorldRenderer.RenderingAny)
//            {
//                if (WorldRenderer.CurrentRenderer.Owner is not PortalGateHooks otherHooks)
//                    return;

//                viewPoint = Main.screenPosition + otherHooks.LastRenderRelativePlayer;
//                if (!VisibleFromThisPoint(viewPoint, projectile.Center, projectile))
//                    return;
//            }

//            if (!new Rectangle(0, 0, Main.screenWidth, Main.screenHeight).Contains((projectile.Center - Main.screenPosition).ToPoint()))
//            {
//                if (!WorldRenderer.RenderingAny)
//                    OtherSide.Enabled = false;
//                return;
//            }

//            if (!VisibleFromThisPoint(viewPoint, projectile.Center, projectile))
//            {
//                if (!WorldRenderer.RenderingAny)
//                    OtherSide.Enabled = false;
//                return;
//            }
//            if (!WorldRenderer.RenderingAny)
//                OtherSide.Enabled = true;

//            int rot = ((int)((projectile.rotation + 0.1 * Math.Sign(projectile.rotation)) / (MathF.PI / 4)) + 8) % 8;
//            bool rightAngles = rot % 2 == 0;

//            if (!WorldRenderer.RenderingAny)
//            {
//                Point rectSize = new(200, 100);
//                switch (rot)
//                {

//                    case 0:
//                        rectSize.Y = Main.screenWidth - (int)(projectile.Center.X - Main.screenPosition.X);
//                        rectSize.X = Main.screenHeight;
//                        break;

//                    case 1:
//                        rectSize.X = (int)Math.Sqrt(2 * Math.Pow(Math.Min(Main.screenWidth, Main.screenHeight), 2)) / 2;
//                        rectSize.Y = (int)Math.Sqrt(Math.Pow(Main.screenHeight - (projectile.Center.Y - Main.screenPosition.Y), 2) + Math.Pow(Main.screenWidth - (projectile.Center.X - Main.screenPosition.X), 2));
//                        break;

//                    case 2:
//                        rectSize.Y = Main.screenHeight - (int)(projectile.Center.Y - Main.screenPosition.Y);
//                        rectSize.X = Main.screenWidth;
//                        break;

//                    case 3:
//                        rectSize.X = (int)Math.Sqrt(2 * Math.Pow(Math.Min(Main.screenWidth, Main.screenHeight), 2)) / 2;
//                        rectSize.Y = (int)Math.Sqrt(Math.Pow(Main.screenHeight - (projectile.Center.Y - Main.screenPosition.Y), 2) + Math.Pow(projectile.Center.X - Main.screenPosition.X, 2));
//                        break;

//                    case 4:
//                        rectSize.Y = (int)(projectile.Center.X - Main.screenPosition.X);
//                        rectSize.X = Main.screenHeight;
//                        break;

//                    case 5:
//                        rectSize.X = (int)Math.Sqrt(2 * Math.Pow(Math.Min(Main.screenWidth, Main.screenHeight), 2)) / 2;
//                        rectSize.Y = (int)Math.Sqrt(Math.Pow(projectile.Center.Y - Main.screenPosition.Y, 2) + Math.Pow(projectile.Center.X - Main.screenPosition.X, 2));
//                        break;

//                    case 6:
//                        rectSize.Y = (int)(projectile.Center.Y - Main.screenPosition.Y);
//                        rectSize.X = Main.screenWidth;
//                        break;

//                    case 7:
//                        rectSize.X = (int)Math.Sqrt(2 * Math.Pow(Math.Min(Main.screenWidth, Main.screenHeight), 2)) / 2;
//                        rectSize.Y = (int)Math.Sqrt(Math.Pow(projectile.Center.Y - Main.screenPosition.Y, 2) + Math.Pow(Main.screenWidth - (projectile.Center.X - Main.screenPosition.X), 2));
//                        break;
//                }

//                //Main.spriteBatch.DrawString(FontAssets.MouseText.Value, rectSize.ToString(), projectile.Center - Main.screenPosition + new Vector2(20, 0), Color.Yellow);
//                OldRectSize = rectSize;
//            }

//            OtherSide.WorldRectangle = GetPortalWorldViewRect(otherPortal, OldRectSize, out Vector2 rectCenter);
//            if (OtherSide.IsReady)
//            {
//                Main.spriteBatch.End();
//                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

//                float angle = WrapAngle(projectile.rotation - otherPortal.rotation + MathF.PI);
//                SpriteEffects effects = SpriteEffects.None;
//                if (otherPortal.rotation == projectile.rotation) // -pi -> pi
//                {
//                    if (projectile.rotation <= -MathF.PI / 2)
//                    {
//                        effects = SpriteEffects.FlipHorizontally;
//                    }
//                    else if (projectile.rotation <= 0)
//                    {
//                        effects = SpriteEffects.FlipVertically;
//                    }
//                    else if (projectile.rotation <= MathF.PI / 2)
//                    {
//                        effects = SpriteEffects.FlipHorizontally;
//                    }
//                    else
//                    {
//                        effects = SpriteEffects.FlipVertically;
//                    }
//                }

//                float sideMod = (MathF.Cos(otherPortal.rotation * 2) + 1) / 2;
//                float rectSide = OtherSide.RenderResult.Width * sideMod + OtherSide.RenderResult.Height * (1 - sideMod);
//                float rectOtherSide = OtherSide.RenderResult.Height * sideMod + OtherSide.RenderResult.Width * (1 - sideMod);

//                float wtf = (0.5f - MathF.Abs(sideMod - 0.5f)) * 2;

//                // 0.2 -> 2.58, 0.5 -> 3.6, 1 -> 6.66, 2 -> 32
//                float wtf2 = 1.27559f + 0.950169f * MathF.Exp(1.73807f * ((float)OldRectSize.Y / OldRectSize.X));

//                Vector2 renderPosWorld = projectile.Center + new Vector2(rectSide / 2 - (rightAngles ? 0 : wtf * rectOtherSide / wtf2), 0).RotatedBy(projectile.rotation);
//                Vector2 renderPos = renderPosWorld - Main.screenPosition;

//                Vector2 rplayerPos = (viewPoint.RotatedBy(-angle, renderPosWorld) - renderPosWorld + OtherSide.RenderResult.Size() / 2);

//                if (effects.HasFlag(SpriteEffects.FlipVertically))
//                    rplayerPos.Y = OtherSide.RenderResult.Height - rplayerPos.Y;
//                if (effects.HasFlag(SpriteEffects.FlipHorizontally))
//                    rplayerPos.X = OtherSide.RenderResult.Width - rplayerPos.X;

//                LastRenderRelativePlayer = rplayerPos;

//                rplayerPos /= OtherSide.RenderResult.Size();

//                Vector2 rportalPos = new Vector2(.5f, rightAngles ? 0 : wtf / wtf2).RotatedBy(otherPortal.rotation + MathF.PI / 2, new(.5f));

//                PortalView ??= WorldRenderLib.GetEffect("Effects/PortalView.fxb");

//                PortalView.Parameters["player"].SetValue(rplayerPos);
//                PortalView.Parameters["portalCenter"].SetValue(rportalPos);
//                PortalView.Parameters["portalSize"].SetValue(48 / rectOtherSide);
//                PortalView.Parameters["time"].SetValue((float)Main.gameTimeCache.TotalGameTime.TotalSeconds);

//                PortalView.Parameters["portalColor"].SetValue(PortalHelper.GetPortalColor(portalId).ToVector4());

//                PortalView.CurrentTechnique.Passes[0].Apply();

//                Main.spriteBatch.Draw(OtherSide.RenderResult, renderPos, null, Color.White, angle, OtherSide.RenderResult.Size() / 2, 1f, effects, 0f);

//                Main.spriteBatch.End();
//                Main.spriteBatch.Begin();
//            }
//            return;
//        }
//        public override void Kill(Projectile projectile, int timeLeft)
//        {
//            OtherSide.Enabled = false;
//            Portals.Remove(projectile);
//        }

//        float WrapAngle(float rad) => (rad % MathF.Tau + MathF.Tau) % MathF.Tau;

//        Rectangle GetPortalWorldViewRect(Projectile proj, Point viewSize, out Vector2 center)
//        {
//            Vector2 portalCenter = proj.position + new Vector2(proj.width, proj.height) / 2;

//            Vector2 bl = new(portalCenter.X - viewSize.X / 2, portalCenter.Y);
//            Vector2 br = new(portalCenter.X + viewSize.X / 2, portalCenter.Y);
//            Vector2 tl = new(portalCenter.X - viewSize.X / 2, portalCenter.Y - viewSize.Y);
//            Vector2 tr = new(portalCenter.X + viewSize.X / 2, portalCenter.Y - viewSize.Y);

//            float angle = proj.rotation - MathF.PI / 2;

//            bl = bl.RotatedBy(angle, portalCenter);
//            br = br.RotatedBy(angle, portalCenter);
//            tl = tl.RotatedBy(angle, portalCenter);
//            tr = tr.RotatedBy(angle, portalCenter);

//            //if (!WorldRenderer.RenderingAny)
//            //{
//            //    DrawLine(tl, tr, Color.Lime);
//            //    DrawLine(bl, br, Color.Lime);
//            //    DrawLine(tl, bl, Color.Lime);
//            //    DrawLine(tr, br, Color.Lime);
//            //}

//            int minX = (int)MathF.Round(MathF.Min(MathF.Min(bl.X, br.X), MathF.Min(tl.X, tr.X)));
//            int maxX = (int)MathF.Round(MathF.Max(MathF.Max(bl.X, br.X), MathF.Max(tl.X, tr.X)));
//            int minY = (int)MathF.Round(MathF.Min(MathF.Min(bl.Y, br.Y), MathF.Min(tl.Y, tr.Y)));
//            int maxY = (int)MathF.Round(MathF.Max(MathF.Max(bl.Y, br.Y), MathF.Max(tl.Y, tr.Y)));

//            center = new Vector2(maxX - minX, maxY - minY) / 2;

//            Rectangle rect = new(minX, minY, maxX - minX, maxY - minY);

//            //if (!WorldRenderer.RenderingAny)
//            //    DrawRect(rect, Color.Red);

//            return rect;
//        }

//        bool VisibleFromThisPoint(Vector2 testPos, Vector2 myPos, Projectile portal)
//        {
//            Vector2 playerToPortalCenter = testPos - myPos;
//            float playerPortalAngle = WrapAngle(portal.rotation - MathF.Atan2(playerToPortalCenter.Y, playerToPortalCenter.X));

//            return (playerPortalAngle <= 0 || playerPortalAngle >= MathF.PI / 2)
//                && (playerPortalAngle <= MathF.Tau - MathF.PI / 2 || playerPortalAngle >= MathF.Tau);
//        }

//        void DrawLine(Vector2 worldA, Vector2 worldB, Color color)
//        {
//            Vector2 diff = worldB - worldA;
//            float angle = MathF.Atan2(diff.Y, diff.X);
//            float length = diff.Length();

//            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, worldA - Main.screenPosition, new(0, 0, 1, 1), color, angle, Vector2.Zero, new Vector2(length, 1), SpriteEffects.None, 0f);
//        }
//        void DrawRect(Rectangle rect, Color color, int thickness = 1)
//        {
//            if (color.A == 0)
//                return;

//            rect.Location = (rect.Location.ToVector2() - Main.screenPosition).ToPoint();

//            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X + thickness, rect.Y, rect.Width - thickness, thickness), color);
//            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Y, thickness, rect.Height - thickness), color);

//            if (rect.Height > thickness)
//                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Bottom - thickness, Math.Max(thickness, rect.Width - thickness), thickness), color);

//            if (rect.Width > thickness)
//                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.Right - thickness, rect.Y + thickness, thickness, Math.Max(thickness, rect.Height - thickness)), color);
//        }

//        public static void DrawPortals()
//        {
//            Portals.RemoveAll(p => Main.projectile[p.whoAmI] != p);
//            Portals.Sort((a, b) => (int)(b.Center.DistanceSQ(Main.LocalPlayer.Center) - a.Center.DistanceSQ(Main.LocalPlayer.Center)));

//            foreach (Projectile p in Portals)
//                if (p.TryGetGlobalProjectile<PortalGateHooks>(out var hooks))
//                    hooks.DrawPortal(p);
//        }

//        private static void Main_DrawInfernoRings(On.Terraria.Main.orig_DrawInfernoRings orig, Main self)
//        {
//            orig(self);
//            DrawPortals();
//        }
//    }
//}
