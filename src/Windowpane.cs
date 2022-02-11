using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using Celeste.Mod.Entities;

namespace Celeste.Mod.WindowpaneHelper {
    [Tracked]
    [CustomEntity("WindowpaneHelper/Windowpane")]
    public class Windowpane : Entity {
        public static Dictionary<string, Tuple<VirtualRenderTarget, Windowpane>> GroupLeaderInfo;

        public ForcefulBackdropRenderer Background;
        public ForcefulBackdropRenderer Foreground;

        public VirtualRenderTarget Target => (GroupLeaderInfo?[StylegroundTag]?.Item1);
        public Windowpane Leader => (GroupLeaderInfo?[StylegroundTag]?.Item2);
        public bool IsLeader => (GroupLeaderInfo.ContainsKey(StylegroundTag) && Leader != null && Leader == this);

        public Color WipeColor;
        public Color OverlayColor;
        public BlendState BlendState;

        public bool PunchThrough;
        public bool RenderingBelow;
        public bool RenderingAbove;

        public readonly string StylegroundTag;

        public Vector2? Node;

        public Windowpane(EntityData data, Vector2 offset) : base(data.Position + offset) {
            GroupLeaderInfo ??= new Dictionary<string, Tuple<VirtualRenderTarget, Windowpane>>();

            Collider = new Hitbox(data.Width, data.Height);
            Depth = data.Int("depth", 11000);

            WipeColor = ParseColor(data.Attr("wipeColor", "000000ff"), Color.Black);
            OverlayColor = ParseColor(data.Attr("overlayColor", "ffffffff"), Color.White);
            StylegroundTag = data.Attr("stylegroundTag", "");

            switch (data.Attr("blendState", "alphablend").ToLower()) {
                case "additive":
                    BlendState = BlendState.Additive; break;
                case "alphablend":
                    BlendState = BlendState.AlphaBlend; break;
                case "nonpremultiplied":
                    BlendState = BlendState.NonPremultiplied; break;
                case "opaque":
                    BlendState = BlendState.Opaque; break;
                default:
                    BlendState = BlendState.AlphaBlend; break;
            }

            switch (data.Attr("renderPosition", "inLevel").ToLower()) {
                case "below":
                case "behind":
                    RenderingAbove = false;
                    RenderingBelow = true;
                    break;
                case "above":
                    RenderingAbove = true;
                    RenderingBelow = false;
                    break;
                case "inLevel":
                default:
                    RenderingAbove = false;
                    RenderingBelow = false;
                    break;
            }

            PunchThrough = data.Bool("punchThrough", false);

            Background = new ForcefulBackdropRenderer();
            Foreground = new ForcefulBackdropRenderer();

            Node = data.FirstNodeNullable(offset);
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            Background.Backdrops = SceneAs<Level>().Background.Backdrops.Where((b) => b.Tags.Contains(StylegroundTag)).ToList();
            Foreground.Backdrops = SceneAs<Level>().Foreground.Backdrops.Where((b) => b.Tags.Contains(StylegroundTag)).ToList();

            Add(new BeforeRenderHook(BeforeRender));

            var transitionListener = new TransitionListener();
            transitionListener.OnInBegin = OnTransitionIn;
            Add(transitionListener);

            JoinGroup();
        }

        public override void Removed(Scene scene) {
            if (IsLeader) {
                var next = (scene as Level).Tracker.GetEntities<Windowpane>()
                                                   .Select(e => (e as Windowpane))
                                                   .Where(e => (e != null && e != this && e.StylegroundTag == this.StylegroundTag));

                if (next.Any()) {
                    next.First().JoinGroup(forceLeader: true);
                } else {
                    Target.Dispose();
                    GroupLeaderInfo.Remove(StylegroundTag);
                }
            }
            base.Removed(scene);
        }

        public override void SceneEnd(Scene scene) {
            if (IsLeader) {
                Target.Dispose();
                GroupLeaderInfo.Remove(StylegroundTag);
            }
            Background.Backdrops = new List<Backdrop>();
            Foreground.Backdrops = new List<Backdrop>();
            base.SceneEnd(scene);
        }

        public override void HandleGraphicsReset() {
            if (IsLeader) {
                GroupLeaderInfo[StylegroundTag] = new Tuple<VirtualRenderTarget, Windowpane>(
                            VirtualContent.CreateRenderTarget("microlith57-windowpanehelper-" + StylegroundTag, 320, 180),
                            this);
            }
        }

        public override void Render() {
            base.Render();

            if (Target == null) { return; }

            if (PunchThrough) {
                Draw.SpriteBatch.End();
                Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, (Scene as Level).GameplayRenderer.Camera.Matrix);

                Draw.Rect(Position, Width, Height, Color.Transparent);
            }

            if (!RenderingBelow && !RenderingAbove) {
                DrawInterior(Scene as Level);
            }

            if (PunchThrough && (RenderingBelow || RenderingAbove) && BlendState == BlendState.AlphaBlend) {
                Draw.SpriteBatch.End();
                Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, (Scene as Level).GameplayRenderer.Camera.Matrix);
            }
        }

        internal void DrawInterior(Level level) {
            JoinGroup();

            Vector2 camera_pos = level.Camera.Position.Floor();
            Vector2 source_pos = (Node.HasValue ? Node.Value : Position) - camera_pos;
            Rectangle sourceRectangle = new Rectangle(
                (int)(source_pos.X), (int)(source_pos.Y),
                (int)base.Width, (int)base.Height);

            // change to preferred blend state
            if (BlendState != BlendState.AlphaBlend) {
                Draw.SpriteBatch.End();
                Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, (Scene as Level).GameplayRenderer.Camera.Matrix);
            }

            Draw.Rect(Position, Width, Height, WipeColor);
            Draw.SpriteBatch.Draw((RenderTarget2D)Target, Position, sourceRectangle, OverlayColor);

            // restore default spritebatch settings
            if (BlendState != BlendState.AlphaBlend) {
                Draw.SpriteBatch.End();
                Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, (Scene as Level).GameplayRenderer.Camera.Matrix);
            }
        }

        private void BeforeRender() {
            if (IsLeader) {
                Background.BeforeRender(Scene);
                Foreground.BeforeRender(Scene);

                if (Target == null) { JoinGroup(); }
                Engine.Graphics.GraphicsDevice.SetRenderTarget(Target);
                Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);

                Background.Render(Scene);
                Foreground.Render(Scene);
            }
        }

        public static void RenderBehindLevel(Level level) {
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, level.GameplayRenderer.Camera.Matrix);
            foreach (Windowpane pane in level.Tracker.GetEntities<Windowpane>()) {
                if (pane.RenderingBelow) { pane.DrawInterior(level); }
            }
            Draw.SpriteBatch.End();
        }

        public static void RenderAboveLevel(Level level) {
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, level.GameplayRenderer.Camera.Matrix);
            foreach (Windowpane pane in level.Tracker.GetEntities<Windowpane>()) {
                if (pane.RenderingAbove) { pane.DrawInterior(level); }
            }
            Draw.SpriteBatch.End();
        }

        internal void JoinGroup(bool forceLeader = false) {
            if (forceLeader || !GroupLeaderInfo.ContainsKey(StylegroundTag) || Leader == null || Target == null || Target.IsDisposed) {
                GroupLeaderInfo[StylegroundTag] = new Tuple<VirtualRenderTarget, Windowpane>(
                            VirtualContent.CreateRenderTarget("microlith57-windowpanehelper-" + StylegroundTag, 320, 180),
                            this);
            }
        }

        private void OnTransitionIn() {
            JoinGroup();
        }

        private Color ParseColor(string hex, Color defaultValue) {
            var chars = hex.TrimStart('#').ToCharArray();

            if (!chars.All((c) => "0123456789ABCDEF".Contains(char.ToUpper(c)))) {
                return defaultValue;
            }

            if (chars.Length == 6 || chars.Length == 8) {
                float a = 1f;
                if (chars.Length == 8) {
                    a = (float)(Calc.HexToByte(chars[6]) * 16 + Calc.HexToByte(chars[7])) / 255f;
                }
                float r = (float)(Calc.HexToByte(chars[0]) * 16 + Calc.HexToByte(chars[1])) / 255f;
                float g = (float)(Calc.HexToByte(chars[2]) * 16 + Calc.HexToByte(chars[3])) / 255f;
                float b = (float)(Calc.HexToByte(chars[4]) * 16 + Calc.HexToByte(chars[5])) / 255f;
                return new Color(r, g, b) * a;
            }
            return defaultValue;
        }
    }
}
