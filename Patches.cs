using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Capture;
using Terraria.ModLoader;

namespace WorldRenderLib
{
    public class Patches : ILoadable
    {
        static WorldRenderLib Mod => ModContent.GetInstance<WorldRenderLib>();

        public void Load(Mod mod)
        {
            On.Terraria.Main.DrawInfernoRings += Main_DrawInfernoRings;
            IL.Terraria.Graphics.Capture.CaptureInterface.DrawButtons += CaptureInterface_DrawButtons;
            IL.Terraria.Graphics.Capture.CaptureInterface.UpdateButtons += CaptureInterface_UpdateButtons;
            On.Terraria.Graphics.Capture.CaptureInterface.Draw += CaptureInterface_Draw;
            On.Terraria.Graphics.Capture.CaptureInterface.Update += CaptureInterface_Update;
        }

        public void Unload()
        {
            On.Terraria.Main.DrawInfernoRings -= Main_DrawInfernoRings;
            IL.Terraria.Graphics.Capture.CaptureInterface.DrawButtons -= CaptureInterface_DrawButtons;
            IL.Terraria.Graphics.Capture.CaptureInterface.UpdateButtons -= CaptureInterface_UpdateButtons;
            On.Terraria.Graphics.Capture.CaptureInterface.Draw -= CaptureInterface_Draw;
            On.Terraria.Graphics.Capture.CaptureInterface.Update -= CaptureInterface_Update;
        }

        private void Main_DrawInfernoRings(On.Terraria.Main.orig_DrawInfernoRings orig, Terraria.Main self)
        {
            orig(self);
            PortalView.DrawPortals();
        }
        private void CaptureInterface_DrawButtons(ILContext il)
        {
            ILCursor c = new(il);

            /*
              IL_0012: ldc.i4.s  9 -> 10
              IL_0014: stloc.0
             */

            if (!c.TryGotoNext(
                x=>x.MatchLdcI4(9),
                x=>x.MatchStloc(out _))) 
            {
                Mod.Logger.Error("Patch error in CaptureInterface.DrawButtons: PatchCount");
                return;
            }

            c.Instrs[c.Index].Operand = (sbyte)10;



            /*
              !IL_0282: ldloc.2 btnIndex -> realIndex
		       IL_0283: ldc.i4  1
		       IL_0284: add
		       IL_0285: stloc   btnIndex
               
		       IL_0286: ldloc   btnIndex
                    +#: stloc   realIndex
                    +#: ldloca  btnIndex
                    +#: call    ModifyButtonIndex

		            +#: ldloc   realIndex
		       IL_0287: ldloc.0
		       IL_0288: blt     loopStart

	          IL_028D: ldstr    ""
             */

            int btnIndex = -1;
            ILLabel loopStart = null!;

            if (!c.TryGotoNext(
                x=>x.MatchLdloc(out btnIndex),
                x=>x.MatchLdcI4(1),
                x=>x.MatchAdd(),
                x=>x.MatchStloc(btnIndex),

                x=>x.MatchLdloc(btnIndex),
                x=>x.MatchLdloc(out _),
                x=>x.MatchBlt(out loopStart!),
                x=>x.MatchLdstr("")

                ))
            {
                Mod.Logger.Error("Patch error in CaptureInterface.DrawButtons: ButtonIndex");
                return;
            }

            int realIndex = il.Method.Body.Variables.Count;
            il.Method.Body.Variables.Add(new(il.Import(typeof(int))));

            c.Next!.OpCode = OpCodes.Ldloc;
            c.Next!.Operand = il.Method.Body.Variables[realIndex];
            c.Index += 5;

            c.Emit(OpCodes.Stloc, realIndex);
            c.Emit(OpCodes.Ldloca, btnIndex);
            c.Emit<Patches>(OpCodes.Call, nameof(ModifyButtonIndex));
            c.Emit(OpCodes.Ldloc, realIndex);

            c.Index = 0;

            /*
              IL_0030: ldc.i4.s  24
              IL_0032: ldc.i4.s  46
              IL_0034: ldloc.2   btnIndex -> readIndex
              IL_0035: mul
              IL_0036: add
              IL_0037: conv.r4
              IL_0038: ldc.r4    24
              IL_003D: call      instance void [FNA]Microsoft.Xna.Framework.Vector2::.ctor(float32, float32)
             */

            if (!c.TryGotoNext(
                x=>x.MatchLdcI4(24),
                x=>x.MatchLdcI4(46),
                x=>x.MatchLdloc(btnIndex),
                x=>x.MatchMul(),
                x=>x.MatchAdd(),
                x=>x.MatchConvR4(),
                x=>x.MatchLdcR4(24),
                x=>x.MatchCall("Microsoft.Xna.Framework.Vector2", ".ctor")
                ))
            {
                Mod.Logger.Error("Patch error in CaptureInterface.DrawButtons: ButtonPosition");
                return;
            }

            c.Index += 2;
            c.Next.OpCode = OpCodes.Ldloc;
            c.Next.Operand = il.Method.Body.Variables[realIndex];

            c.Index = 0;

            /*
              IL_00DF: ldloc.2
		      IL_00E0: switch    (IL_010B, IL_011A, IL_0129, IL_0129, IL_0129, IL_0138, IL_015C, IL_016B, IL_017A)

		      IL_0109: br.s      IL_0187
              
		      IL_010B: ldsfld    class [ReLogic]ReLogic.Content.Asset`1<class [FNA]Microsoft.Xna.Framework.Graphics.Texture2D>[] Terraria.GameContent.TextureAssets::Camera
		      IL_0110: ldc.i4.7
		      IL_0111: ldelem.ref
		      IL_0112: callvirt  instance !0 class [ReLogic]ReLogic.Content.Asset`1<class [FNA]Microsoft.Xna.Framework.Graphics.Texture2D>::get_Value()
		      IL_0117: stloc.3
             */



            int texture = -1;
            ILLabel switchEnd = null!;

            if (!c.TryGotoNext(
                x=>x.MatchLdloc(btnIndex),
                x=>x.MatchSwitch(out _),

                x=>x.MatchBr(out switchEnd!),

                x=>x.MatchLdsfld("Terraria.GameContent.TextureAssets", "Camera"),
                x=>x.MatchLdcI4(7),
                x=>x.MatchLdelemRef(),
                x=>x.MatchCallvirt(out _),
                x=>x.MatchStloc(out texture)
                ))
            {
                Mod.Logger.Error("Patch error in CaptureInterface.DrawButtons: IconTexture");
                return;
            }

            c.Prev = switchEnd.Target!;

            c.Emit(OpCodes.Ldloc, btnIndex);
            c.Emit(OpCodes.Ldloca, texture);
            c.Emit<Patches>(OpCodes.Call, nameof(ModifyCaptureInterfaceButton));


            int hovered = -1;
            int hoverText = -1;

            /*
              IL_028D: ldstr     ""
	          IL_0292: stloc.1   hoverText
	          IL_0293: ldarg.0
	          IL_0294: ldfld     int32 Terraria.Graphics.Capture.CaptureInterface::HoveredMode
	          IL_0299: stloc.s   hovered
	          IL_029B: ldloc.s   hovered
                  
	          IL_029D: ldc.i4.m1
	          IL_029E: sub
	          IL_029F: switch    (IL_0383, IL_02D1, IL_02E4, IL_02F7, IL_0307, IL_0317, IL_0327, IL_034D, IL_035D, IL_036D)
              
	          IL_02CC: br        switchEnd
             */

            if (!c.TryGotoNext(
                x=>x.MatchLdstr(""),
                x=>x.MatchStloc(out hoverText),
                x=>x.MatchLdarg(out _),
                x=>x.MatchLdfld("Terraria.Graphics.Capture.CaptureInterface", "HoveredMode"),
                x=>x.MatchStloc(out hovered),
                x=>x.MatchLdloc(hovered),
                x=>x.MatchLdcI4(-1),
                x=>x.MatchSub(),
                x=>x.MatchSwitch(out _),
                x=>x.MatchBr(out switchEnd!)
                ))
            {
                Mod.Logger.Error("Patch error in CaptureInterface.DrawButtons: HoverText");
                return;
            }

            c.Next = switchEnd.Target!;
            c.Index += 2;

            c.Emit(OpCodes.Ldarg, 0);
            c.Emit<CaptureInterface>(OpCodes.Ldfld, "HoveredMode");
            c.Emit(OpCodes.Ldloca, hoverText);
            c.Emit<Patches>(OpCodes.Call, nameof(ModifyCaptureInterfaceHoverText));

            c.Index = 0;
        }
        private void CaptureInterface_UpdateButtons(ILContext il)
        {
            ILCursor c = new(il);

            /*
              IL_0012: ldc.i4.s  9 -> 10
              IL_0014: stloc.0
             */

            if (!c.TryGotoNext(
                x => x.MatchLdcI4(9),
                x => x.MatchStloc(out _)))
            {
                Mod.Logger.Error("Patch error in CaptureInterface.UpdateButtons: PatchCount");
                return;
            }

            c.Instrs[c.Index].Operand = (sbyte)10;

            /*
              !IL_0282: ldloc.2 btnIndex -> realIndex
		       IL_0283: ldc.i4  1
		       IL_0284: add
		       IL_0285: stloc   btnIndex
               
		       IL_0286: ldloc   btnIndex
                    +#: stloc   realIndex
                    +#: ldloca  btnIndex
                    +#: call    ModifyButtonIndex

		            +#: ldloc   realIndex
		       IL_0287: ldloc.0
		       IL_0288: blt     loopStart

	          IL_025E: ldc.i4.0
              IL_025F: ret
            */

            int btnIndex = -1;
            ILLabel loopStart = null!;

            if (!c.TryGotoNext(
                x => x.MatchLdloc(out btnIndex),
                x => x.MatchLdcI4(1),
                x => x.MatchAdd(),
                x => x.MatchStloc(btnIndex),

                x => x.MatchLdloc(btnIndex),
                x => x.MatchLdloc(out _),
                x => x.MatchBlt(out loopStart!),
                x => x.MatchLdcI4(0),
                x => x.MatchRet()

                ))
            {
                Mod.Logger.Error("Patch error in CaptureInterface.UpdateButtons: ButtonIndex");
                return;
            }

            int realIndex = il.Method.Body.Variables.Count;
            il.Method.Body.Variables.Add(new(il.Import(typeof(int))));

            c.Next!.OpCode = OpCodes.Ldloc;
            c.Next!.Operand = il.Method.Body.Variables[realIndex];
            c.Index += 5;

            c.Emit(OpCodes.Stloc, realIndex);
            c.Emit(OpCodes.Ldloca, btnIndex);
            c.Emit<Patches>(OpCodes.Call, nameof(ModifyButtonIndex));
            c.Emit(OpCodes.Ldloc, realIndex);

            c.Index = 0;

            /*
              IL_001F: ldc.i4.s  24
              IL_0021: ldc.i4.s  46
              IL_0023: ldloc.2
              IL_0024: mul
              IL_0025: add
              IL_0026: ldc.i4.s  24
              IL_0028: ldc.i4.s  42
             */

            if (!c.TryGotoNext(
                x => x.MatchLdcI4(24),
                x => x.MatchLdcI4(46),
                x => x.MatchLdloc(btnIndex),
                x => x.MatchMul(),
                x => x.MatchAdd(),
                x => x.MatchLdcI4(24),
                x => x.MatchLdcI4(42)
                ))
            {
                Mod.Logger.Error("Patch error in CaptureInterface.UpdateButtons: ButtonPosition");
                return;
            }

            c.Index += 2;
            c.Next.OpCode = OpCodes.Ldloc;
            c.Next.Operand = il.Method.Body.Variables[realIndex];


            int clicked = -1;

            /*
              IL_004B: ldsfld    bool Terraria.Main::mouseLeft
              IL_0050: brfalse.s IL_0059
              
              IL_0052: ldsfld    bool Terraria.Main::mouseLeftRelease
              IL_0057: br.s      IL_005A
              
              IL_0059: ldc.i4.0
              
              IL_005A: stloc.s   clicked

                   +#: ldloc     clicked
                   +#: brfalse   noClick
                   +#: ldloc     btnIndex
                   +#: call      HandleCaptureInterfaceClick
              noClick:    
             */

            if (!c.TryGotoNext(
                MoveType.After,
                x=>x.MatchLdsfld<Main>("mouseLeft"),
                x=>x.MatchBrfalse(out _),
                x=>x.MatchLdsfld<Main>("mouseLeftRelease"),
                x=>x.MatchBr(out _),
                x=>x.MatchLdcI4(0),
                x=>x.MatchStloc(out clicked)
                ))
            {
                Mod.Logger.Error("Patch error in CaptureInterface.UpdateButtons: ClickHandler");
                return;
            }

            ILLabel noClick = c.DefineLabel();

            c.Emit(OpCodes.Ldloc, clicked);
            c.Emit(OpCodes.Brfalse, noClick);
            c.Emit(OpCodes.Ldloc, btnIndex);
            c.Emit<Patches>(OpCodes.Call, nameof(HandleCaptureInterfaceClick));
            c.MarkLabel(noClick);
        }
        private void CaptureInterface_Update(On.Terraria.Graphics.Capture.CaptureInterface.orig_Update orig, CaptureInterface self)
        {
            orig(self);
            GifRecorder.Update();
        }
        private void CaptureInterface_Draw(On.Terraria.Graphics.Capture.CaptureInterface.orig_Draw orig, CaptureInterface self, SpriteBatch sb)
        {
            if (self.Active)
                GifRecorder.Draw();
            orig(self, sb);
        }

        public static void ModifyButtonIndex(ref int index)
        {
            if (index == 2)
                index = 9;

            else if (index > 2)
                index--;
        }
        public static void ModifyCaptureInterfaceButton(int index, ref Texture2D texture)
        {
            if (index == 9)
            {
                texture = GifRecorder.GetButtonIcon();
            }
        }
        public static void ModifyCaptureInterfaceHoverText(int index, ref string text)
        {
            if (index == 9)
                text = GifRecorder.GetButtonText();
        }
        public static void HandleCaptureInterfaceClick(int index)
        {
            if (index == 9)
            {
                GifRecorder.HandleButtonClick();
            }
        }


    }
}
