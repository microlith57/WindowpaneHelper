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
                bool orig_Visible = self.Visible, orig_ForceVisible = self.ForceVisible;
                if (self.OnlyIn.Count == 0 && (Windowpane.Background.Backdrops.Contains(self) || Windowpane.Foreground.Backdrops.Contains(self))) {
                    self.Visible = true;
                    self.ForceVisible = true;
                }
                orig(self, scene);
                self.Visible = orig_Visible;
                self.ForceVisible = orig_ForceVisible;
            });

            On.Celeste.Backdrop.Render += (backdropRenderHook = (On.Celeste.Backdrop.orig_Render orig, Backdrop self, Scene scene) => {
                if (!ForcefulBackdropRenderer.Rendering && self.OnlyIn.Count == 0 && (Windowpane.Background.Backdrops.Contains(self) || Windowpane.Foreground.Backdrops.Contains(self))) {
                    return;
                }
                orig(self, scene);
            });
        }

        public override void Unload() {
            On.Celeste.Backdrop.Update -= backdropUpdateHook;
            On.Celeste.Backdrop.Render -= backdropRenderHook;
        }
    }
}
