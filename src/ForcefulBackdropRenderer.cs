using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.WindowpaneHelper {
    /// <summary>
    /// A BackdropRenderer that always renders its backdrops, regardless of whether or not they are Visible.
    /// </summary>
    public class ForcefulBackdropRenderer : BackdropRenderer {
        private static FieldInfo usingSpritebatchInfo = typeof(BackdropRenderer).GetField("usingSpritebatch", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static bool Rendering = false;

        public void BeforeRender(Scene scene, string[] only) {
            Rendering = true;
            foreach (Backdrop backdrop in Backdrops) {
                if (only.Length > 0 && !backdrop.Tags.IsSupersetOf(only)) { continue; }

                bool orig_Visible = backdrop.Visible, orig_ForceVisible = backdrop.ForceVisible;
                backdrop.Visible = true;
                backdrop.ForceVisible = true;

                backdrop.BeforeRender(scene);

                backdrop.Visible = orig_Visible;
                backdrop.ForceVisible = orig_ForceVisible;
            }
            Rendering = false;
        }

        public void BeforeRender(Scene scene, string only) { this.BeforeRender(scene, new string[] { only }); }
        public override void BeforeRender(Scene scene) { this.BeforeRender(scene, new string[] { }); }

        public void Render(Scene scene, string[] only, bool drawFade) {
            Rendering = true;
            BlendState blendState = BlendState.AlphaBlend;
            foreach (Backdrop backdrop in Backdrops) {
                if (only.Length > 0 && !backdrop.Tags.IsSupersetOf(only)) { continue; }

                bool orig_Visible = backdrop.Visible, orig_ForceVisible = backdrop.ForceVisible;
                backdrop.Visible = true;
                backdrop.ForceVisible = true;

                if (backdrop is Parallax && (backdrop as Parallax).BlendState != blendState) {
                    EndSpritebatch();
                    blendState = (backdrop as Parallax).BlendState;
                }
                object usingSpritebatchObj = usingSpritebatchInfo.GetValue(this);
                bool usingSpritebatch = (usingSpritebatchObj is bool) && (bool)usingSpritebatchObj;
                if (backdrop.UseSpritebatch && !usingSpritebatch) {
                    StartSpritebatch(blendState);
                }
                if (!backdrop.UseSpritebatch && usingSpritebatch) {
                    EndSpritebatch();
                }
                backdrop.Render(scene);

                backdrop.Visible = orig_Visible;
                backdrop.ForceVisible = orig_ForceVisible;
            }
            if (Fade > 0f && drawFade) {
                Draw.Rect(-10f, -10f, 340f, 200f, FadeColor * Fade);
            }
            EndSpritebatch();
            Rendering = false;
        }

        public void Render(Scene scene, string only, bool drawFade) { this.Render(scene, new string[] { only }, drawFade); }
        public void Render(Scene scene, string[] only) { this.Render(scene, only, true); }
        public void Render(Scene scene, string only) { this.Render(scene, new string[] { only }, true); }
        public void Render(Scene scene, bool drawFade) { this.Render(scene, new string[] { }, drawFade); }

        public override void Render(Scene scene) { this.Render(scene, new string[] { }, true); }
    }
}
