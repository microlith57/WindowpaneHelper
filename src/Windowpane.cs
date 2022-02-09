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

        public readonly string StylegroundTag;

        public Vector2? Node;

        public Windowpane(EntityData data, Vector2 offset) : base(data.Position + offset) {
            GroupLeaderInfo ??= new Dictionary<string, Tuple<VirtualRenderTarget, Windowpane>>();

            Collider = new Hitbox(data.Width, data.Height);
            Depth = data.Int("depth", 11000);

            WipeColor = ParseColor(data.Attr("wipeColor", "000000ff"), Color.Black);
            OverlayColor = ParseColor(data.Attr("overlayColor", "ffffffff"), Color.White);
            StylegroundTag = data.Attr("stylegroundTag", "");

            string blendState = data.Attr("blendState", "alphablend").ToLower();
            switch (blendState) {
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
                                                   .Where(e => (e != null && e != this));

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

        public override void Render() {
            base.Render();

            if (Target == null) { return; }

            Vector2 camera_pos = SceneAs<Level>().Camera.Position.Floor();
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

        internal void JoinGroup(bool forceLeader = false) {
            if (forceLeader || !GroupLeaderInfo.ContainsKey(StylegroundTag)) {
                GroupLeaderInfo[StylegroundTag] = new Tuple<VirtualRenderTarget, Windowpane>(
                            VirtualContent.CreateRenderTarget("microlith57-windowpanehelper-" + StylegroundTag, 320, 180),
                            this);
            }
        }

        private void OnTransitionIn() {
            if (Leader.Scene != Scene) {
                JoinGroup(forceLeader: true);
            }
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
