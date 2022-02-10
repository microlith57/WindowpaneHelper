using System;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace Celeste.Mod.WindowpaneHelper {
    public class WindowpaneHelperModule : EverestModule {
        public static WindowpaneHelperModule Instance;
        private static On.Celeste.Backdrop.hook_IsVisible backdropIsVisibleHook;
        private static On.Celeste.MapData.hook_ParseBackdrop backdropParseHook;

        public WindowpaneHelperModule() {
            Instance = this;
        }

        public override void Load() {
            IL.Celeste.BackdropRenderer.Render += modBackdropRendererRender;

            On.Celeste.Backdrop.IsVisible += (backdropIsVisibleHook = (On.Celeste.Backdrop.orig_IsVisible orig, Backdrop self, Level level) => {
                if (self.Tags.Contains("windowpanehelperonly")) { return true; }
                if (Windowpane.GroupLeaderInfo?.Keys != null && self.Tags.Intersect(Windowpane.GroupLeaderInfo.Keys).Any()) { return true; }
                return orig(self, level);
            });

            On.Celeste.MapData.ParseBackdrop += (backdropParseHook = (On.Celeste.MapData.orig_ParseBackdrop orig, MapData self, BinaryPacker.Element child, BinaryPacker.Element above) => {
                Backdrop backdrop = orig(self, child, above);
                if (backdrop.Tags.Count == 1) {
                    backdrop.Tags = new HashSet<string>(backdrop.Tags.First().Split(','));
                }
                return backdrop;
            });
        }

        public override void Unload() {
            IL.Celeste.BackdropRenderer.Render -= modBackdropRendererRender;
            On.Celeste.Backdrop.IsVisible -= backdropIsVisibleHook;
            On.Celeste.MapData.ParseBackdrop -= backdropParseHook;
        }

        private void modBackdropRendererRender(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            Logger.Log("WindowpaneHelper", "Patching BackdropRenderer.Render");

            // go just after `if (backdrop.Visible) {`
            if (!cursor.TryGotoNext(MoveType.After,
                                    instr => instr.MatchLdloc(2),
                                    instr => instr.MatchLdfld(typeof(Backdrop), "Visible"),
                                    instr => instr.MatchBrfalse(out ILLabel _))) { return; }
            // find the end of the if block
            cursor.Prev.MatchBrfalse(out ILLabel continue_label);

            // if the backdrop has the tag, jump over the if block
            cursor.Emit(OpCodes.Ldloc, 2);
            cursor.EmitDelegate<Func<Backdrop, bool>>(backdrop => backdrop.Tags.Contains("windowpanehelperonly"));
            cursor.Emit(OpCodes.Brtrue_S, continue_label);
        }
    }
}
