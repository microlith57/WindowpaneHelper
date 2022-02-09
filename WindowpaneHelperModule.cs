using System;
using System.Linq;
using System.Collections.Generic;
using Monocle;

namespace Celeste.Mod.WindowpaneHelper {
    public class WindowpaneHelperModule : EverestModule {
        public static WindowpaneHelperModule Instance;
        private static On.Celeste.Backdrop.hook_Update backdropUpdateHook;
        private static On.Celeste.Backdrop.hook_Render backdropRenderHook;

        public WindowpaneHelperModule() {
            Instance = this;
        }

        public override void Load() {
            On.Celeste.Backdrop.Update += (backdropUpdateHook = (On.Celeste.Backdrop.orig_Update orig, Backdrop self, Scene scene) => {
                if (self.Tags.Intersect(Windowpane.GroupLeaderInfo.Keys).Any()) {
                    bool orig_Visible = self.Visible, orig_ForceVisible = self.ForceVisible;
                    self.Visible = true;
                    self.ForceVisible = true;
                    orig(self, scene);
                    self.Visible = orig_Visible;
                    self.ForceVisible = orig_ForceVisible;
                } else { orig(self, scene); }
            });

            On.Celeste.Backdrop.Render += (backdropRenderHook = (On.Celeste.Backdrop.orig_Render orig, Backdrop self, Scene scene) => {
                if (!ForcefulBackdropRenderer.Rendering || self.Tags.Intersect(Windowpane.GroupLeaderInfo.Keys).Any()) {
                    orig(self, scene);
                }
            });
        }

        public override void Unload() {
            On.Celeste.Backdrop.Update -= backdropUpdateHook;
            On.Celeste.Backdrop.Render -= backdropRenderHook;
        }
    }
}
