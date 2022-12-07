using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Metadata;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using WorldRenderLib.Wrappers;
using WorldRenderLib.Wrapping;

namespace WorldRenderLib
{
    internal class PortalView : GlobalProjectile
    {
        private static List<Projectile> Portals = new();

        public override bool InstancePerEntity => true;

        WorldRenderer OtherSide = null!;

        static IPortalHelperWrapper PortalHelperWrapper = TypeWrapping.CreateWrapper<IPortalHelperWrapper>(typeof(PortalHelper), null!);
        static IModLoaderWrapper ModLoaderWrapper = TypeWrapping.CreateWrapper<IModLoaderWrapper>(typeof(ModLoader), null!);
        static Effect? PortalShader;

        Rectangle OldViewBox;
        Vector2 ViewBoxVelocity;
        Vector2 LastRenderRelativePlayer;
        Vector2 LastRenderRenderPosToPortal;

        public PortalView() { }

        public override void Load()
        {
            On.Terraria.Main.DrawInfernoRings += Main_DrawInfernoRings;
        }
        public override void Unload()
        {
            On.Terraria.Main.DrawInfernoRings -= Main_DrawInfernoRings;
        }
        private static void Main_DrawInfernoRings(On.Terraria.Main.orig_DrawInfernoRings orig, Main self)
        {
            orig(self);

            
        }

        public static void DrawPortals()
        {
            Portals.RemoveAll(p => Main.projectile[p.whoAmI] != p);
            Portals.Sort((a, b) => (int)(b.Center.DistanceSQ(Main.LocalPlayer.Center) - a.Center.DistanceSQ(Main.LocalPlayer.Center)));

            foreach (Projectile p in Portals)
                if (p.TryGetGlobalProjectile<PortalView>(out var hooks))
                    hooks.DrawPortal(p);
        }

        public override GlobalProjectile NewInstance(Projectile target)
        {
            PortalView hooks = (PortalView)base.NewInstance(target);

            if (ModLoaderWrapper.IsLoading)
                return hooks;

            int projIndex = target.whoAmI;
            hooks.OtherSide = new()
            {
                Enabled = true,
                Owner = hooks,
                CheckValid = () => 
                {
                    Projectile proj = Main.projectile[projIndex];
                    return proj.active && proj.type == ProjectileID.PortalGunGate && proj == target;
                }
            };

            Portals.Add(target);

            return hooks;
        }
        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        {
            return entity.type == ProjectileID.PortalGunGate;
        }
        public override void Kill(Projectile projectile, int timeLeft)
        {
            OtherSide.Enabled = false;
            Portals.Remove(projectile);
        }

        public void DrawPortal(Projectile projectile)
        {
            int portalId = (int)projectile.ai[1];
            int otherPortalProjectile = PortalHelperWrapper.FoundPortals[projectile.owner, 1 - portalId];

            if (otherPortalProjectile == -1)
            {
                OtherSide.Enabled = false;
                return;
            }

            Projectile otherPortal = Main.projectile[otherPortalProjectile];

            const float portalSize = 24;

            Vector2 portalDir = new(-MathF.Sin(projectile.ai[0]), MathF.Cos(projectile.ai[0]));

            Vector2 portalScreen = projectile.Center - Main.screenPosition;
            Vector2 playerScreen = Main.LocalPlayer.Center - Main.screenPosition;

            if (WorldRenderer.RenderingAny && WorldRenderer.CurrentRenderer.Owner is PortalView otherHooks)
            {
                playerScreen = otherHooks.LastRenderRelativePlayer;
            }

            if (portalScreen.X < 0 || portalScreen.Y < 0 || portalScreen.X > Main.screenWidth || portalScreen.Y > Main.screenHeight)
            {
                if (!WorldRenderer.RenderingAny)
                    OtherSide.Enabled = false;
                return;
            }

            Vector2 portalToPlayer = playerScreen - portalScreen;

            float dot = Vector2.Dot(portalToPlayer, portalDir);
            if (dot < 0)
            {
                if (!WorldRenderer.RenderingAny)
                {
                    OtherSide.Enabled = false;
                    return;
                }
            }

            OtherSide.Enabled = true;

            float angleDiff = projectile.ai[0] - otherPortal.ai[0] + MathF.PI;

            Vector2 portalRight = portalDir.RotatedBy(Math.Tau / 4) * portalSize + portalScreen;
            Vector2 portalLeft = portalDir.RotatedBy(-Math.Tau / 4) * portalSize + portalScreen;

            Vector2 portalLeftHitDir = (portalLeft - playerScreen).RotatedBy(0.05);
            Vector2 portalCenterHitDir = portalScreen - playerScreen;
            Vector2 portalRightHitDir = (portalRight - playerScreen).RotatedBy(-0.05);

            portalLeftHitDir.Normalize();
            portalCenterHitDir.Normalize();
            portalRightHitDir.Normalize();

            float leftAngle = MathF.Atan2(portalLeftHitDir.Y, portalLeftHitDir.X);
            float centerAngle = projectile.ai[0] - MathF.PI / 2;
            float rightAngle = MathF.Atan2(portalRightHitDir.Y, portalRightHitDir.X);

            leftAngle = MathHelper.WrapAngle(leftAngle - centerAngle);
            rightAngle = MathHelper.WrapAngle(rightAngle - centerAngle);

            if (leftAngle > 0 && rightAngle < 0)
                portalCenterHitDir = -portalDir;

            Vector2 portalRightHit = RaycastToScreenBorder(portalRight, portalRightHitDir);
            Vector2 portalLeftHit = RaycastToScreenBorder(portalLeft, portalLeftHitDir);
            Vector2 portalCenterHit = RaycastToScreenBorder(portalScreen, portalCenterHitDir);
            
            bool flip = projectile.ai[0] == otherPortal.ai[0];

            if (OtherSide.IsReady)
            {
                PortalShader ??= WorldRenderLib.GetEffect("Effects/PortalShader.fxb");
                
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, RasterizerState.CullNone);

                Matrix projection = Matrix.CreateOrthographicOffCenter(0, Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height, 0, 0, 1);

                PortalShader.Parameters["MatrixTransform"].SetValue(projection);

                PortalShader.Parameters["flipPos"].SetValue(OldViewBox.Center.ToVector2() - Main.screenPosition);
                PortalShader.Parameters["flipNormal"].SetValue(portalDir.RotatedBy(Math.PI / 2));
                PortalShader.Parameters["flipEnabled"].SetValue(flip);

                PortalShader.Parameters["portalSize"].SetValue(48);
                PortalShader.Parameters["portalPos"].SetValue(portalScreen);
                PortalShader.Parameters["portalAngle"].SetValue(projectile.ai[0] - MathF.PI/2);
                PortalShader.Parameters["playerPos"].SetValue(playerScreen);
                PortalShader.Parameters["portalColor"].SetValue(PortalHelper.GetPortalColor(portalId).ToVector4());

                PortalShader.Parameters["time"].SetValue((float)Main.gameTimeCache.TotalGameTime.TotalSeconds);

                PortalShader.CurrentTechnique.Passes[0].Apply();

                Vector2 renderPos = OldViewBox.Center.ToVector2();

                if (WorldRenderer.RenderingAny)
                {
                    renderPos = projectile.Center - LastRenderRenderPosToPortal;
                }

                Main.spriteBatch.Draw(OtherSide.RenderResult, renderPos - Main.screenPosition, null, Color.White, angleDiff, OtherSide.RenderResult.Size() / 2, 1f, SpriteEffects.None, 0);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            }

            if (!WorldRenderer.RenderingAny)
            {
                // Try follow screen edge
                Vector2 screenVelocity = Main.screenPosition - Main.screenLastPosition;
                portalCenterHit += portalCenterHitDir * (Math.Max(0, Vector2.Dot(portalCenterHitDir, screenVelocity)) + 16);

                Vector2 portalDiff = otherPortal.Center - projectile.Center;
                Rectangle viewBox = GetBoundingBox(portalRightHit, portalLeftHit, portalRight, portalLeft, portalCenterHit);

                Vector2 renderPosWorld = viewBox.Center.ToVector2() + Main.screenPosition;

                if (OtherSide.IsReady)
                {
                    LastRenderRelativePlayer = (Main.LocalPlayer.Center.RotatedBy(-angleDiff, renderPosWorld) - renderPosWorld + OtherSide.RenderResult.Size() / 2);
                    LastRenderRenderPosToPortal = projectile.Center - OldViewBox.Center.ToVector2();
                }

                Rectangle otherSideViewBox = viewBox;

                if (flip)
                    otherSideViewBox = FlipRectangle(otherSideViewBox, portalScreen, portalDir.RotatedBy(Math.PI/2));

                otherSideViewBox = RotateRectangle(otherSideViewBox, -angleDiff, portalScreen);

                otherSideViewBox.Offset(portalDiff.ToPoint());
                otherSideViewBox.Offset(Main.screenPosition.ToPoint());

                OtherSide.WorldRectangle = otherSideViewBox;

                OldViewBox = viewBox;
                OldViewBox.Offset(Main.screenPosition.ToPoint());
            }
        }

        static Vector2 RaycastToScreenBorder(Vector2 start, Vector2 dir)
        {
            dir.Normalize();

            float angle = MathF.Atan2(Math.Abs(dir.Y), Math.Abs(dir.X));

            float distX = dir.X > 0 ? Main.screenWidth - start.X : start.X;
            float distY = dir.Y > 0 ? Main.screenHeight - start.Y : start.Y;

            float lengthX = dir.X == 0 ? float.MaxValue : distX / MathF.Cos(angle);
            float lengthY = dir.Y == 0 ? float.MaxValue : distY / MathF.Sin(angle);

            return dir * Math.Min(lengthX, lengthY) + start;
        }
        static Rectangle GetBoundingBox(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector2 e)
        {
            float minX = Math.Min(Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X)), e.X);
            float minY = Math.Min(Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)), e.Y);
            float maxX = Math.Max(Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X)), e.X);
            float maxY = Math.Max(Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)), e.Y);

            return new((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));
        }
        static Rectangle RotateRectangle(Rectangle rect, float angle, Vector2 origin)
        {
            Vector2 tl = rect.TopLeft().RotatedBy(angle, origin);
            Vector2 tr = rect.TopRight().RotatedBy(angle, origin);
            Vector2 bl = rect.BottomLeft().RotatedBy(angle, origin);
            Vector2 br = rect.BottomRight().RotatedBy(angle, origin);

            return GetBoundingBox(tl, tr, bl, br, br);
        }
        static Rectangle FlipRectangle(Rectangle rect, Vector2 pos, Vector2 normal)
        {
            normal.Normalize();

            if (Main.oldKeyState[Keys.RightAlt] == KeyState.Up && Main.keyState[Keys.RightAlt] == KeyState.Down)
                Debugger.Break();

            Vector2 tl = rect.TopLeft()     - pos;
            Vector2 tr = rect.TopRight()    - pos;
            Vector2 bl = rect.BottomLeft()  - pos;
            Vector2 br = rect.BottomRight() - pos;

            tl += normal * -Vector2.Dot(normal, tl) * 2;
            tr += normal * -Vector2.Dot(normal, tr) * 2;
            bl += normal * -Vector2.Dot(normal, bl) * 2;
            br += normal * -Vector2.Dot(normal, br) * 2;

            return GetBoundingBox(tl + pos, tr + pos, bl + pos, br + pos, br + pos);
        }
    }
}
