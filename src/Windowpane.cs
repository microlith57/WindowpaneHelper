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

        public VirtualRenderTarget Target => (GroupLeaderInfo?[StylegroundTag]?.Item1);
        public Windowpane Leader => (GroupLeaderInfo?[StylegroundTag]?.Item2);
        public bool IsLeader => (GroupLeaderInfo.ContainsKey(StylegroundTag) && Leader != null && Leader == this);

        public Color WipeColor;
        public Color OverlayColor;
        public bool Background;
        public bool Foreground;

        public readonly string StylegroundTag;

        private BackdropRenderer bgRenderer;
        private BackdropRenderer fgRenderer;

        public Vector2? Node;

        public Windowpane(EntityData data, Vector2 offset) : base(data.Position + offset) {
            Collider = new Hitbox(data.Width, data.Height);
            Depth = data.Int("depth", 11000);

            WipeColor = ParseColor(data.Attr("wipeColor", "000000ff"), Color.Black);
            OverlayColor = data.HexColor("overlayColor", Color.White);
            Background = data.Bool("background", true);
            Foreground = data.Bool("foreground", false);
            StylegroundTag = data.Attr("stylegroundTag", "");

            bgRenderer = new BackdropRenderer();
            bgRenderer.Backdrops = new List<Backdrop>();
            fgRenderer = new BackdropRenderer();
            fgRenderer.Backdrops = new List<Backdrop>();

            GroupLeaderInfo ??= new Dictionary<string, Tuple<VirtualRenderTarget, Windowpane>>();

            Node = data.FirstNodeNullable(offset);
        }

        public override void Added(Scene scene) {
            base.Added(scene);

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

            Draw.SpriteBatch.Draw((RenderTarget2D)Target, Position, sourceRectangle, OverlayColor);
        }

        private void BeforeRender() {
            if (IsLeader) {
                bgRenderer.BeforeRender(Scene);
                fgRenderer.BeforeRender(Scene);

                if (Target == null) { JoinGroup(); }
                Engine.Graphics.GraphicsDevice.SetRenderTarget(Target);
                Engine.Graphics.GraphicsDevice.Clear(WipeColor);

                bgRenderer.Render(Scene);
                fgRenderer.Render(Scene);
            }
        }

        internal void JoinGroup(bool forceLeader = false) {
            if (forceLeader || !GroupLeaderInfo.ContainsKey(StylegroundTag)) {
                GroupLeaderInfo[StylegroundTag] = new Tuple<VirtualRenderTarget, Windowpane>(
                            VirtualContent.CreateRenderTarget("microlith57-windowpanehelper-" + StylegroundTag, 320, 180),
                            this);
            }
            RefreshBackdrops();
        }

        private void OnTransitionIn() {
            if (Leader.Scene != Scene) {
                JoinGroup(forceLeader: true);
            }
        }

        private void RefreshBackdrops() {
            if (Background) {
                bgRenderer.Backdrops = SceneAs<Level>().Background.Backdrops
                    .Where((bg) => string.IsNullOrEmpty(StylegroundTag) || bg.Tags.Contains(StylegroundTag))
                    .ToList<Backdrop>();
            } else { bgRenderer.Backdrops = new List<Backdrop>(); }
            if (Foreground) {
                fgRenderer.Backdrops = SceneAs<Level>().Foreground.Backdrops
                    .Where((bg) => string.IsNullOrEmpty(StylegroundTag) || bg.Tags.Contains(StylegroundTag))
                    .ToList<Backdrop>();
            } else { fgRenderer.Backdrops = new List<Backdrop>(); }
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
