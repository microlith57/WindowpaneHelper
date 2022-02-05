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
        public static ForcefulBackdropRenderer Background;
        public static ForcefulBackdropRenderer Foreground;

        public VirtualRenderTarget Target => (GroupLeaderInfo?[StylegroundTag]?.Item1);
        public Windowpane Leader => (GroupLeaderInfo?[StylegroundTag]?.Item2);
        public bool IsLeader => (GroupLeaderInfo.ContainsKey(StylegroundTag) && Leader != null && Leader == this);

        public Color WipeColor;
        public Color OverlayColor;

        public readonly string StylegroundTag;

        public Vector2? Node;

        public Windowpane(EntityData data, Vector2 offset) : base(data.Position + offset) {
            Collider = new Hitbox(data.Width, data.Height);
            Depth = data.Int("depth", 11000);

            WipeColor = ParseColor(data.Attr("wipeColor", "000000ff"), Color.Black);
            OverlayColor = data.HexColor("overlayColor", Color.White);
            StylegroundTag = data.Attr("stylegroundTag", "");

            Background ??= new ForcefulBackdropRenderer();
            Foreground ??= new ForcefulBackdropRenderer();
            GroupLeaderInfo ??= new Dictionary<string, Tuple<VirtualRenderTarget, Windowpane>>();

            Node = data.FirstNodeNullable(offset);
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            if (Background.Backdrops.Count == 0) {
                Background.Backdrops = SceneAs<Level>().Background.Backdrops;
            }
            if (Foreground.Backdrops.Count == 0) {
                Foreground.Backdrops = SceneAs<Level>().Foreground.Backdrops;
            }

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

            Draw.Rect(Position, Width, Height, WipeColor);
            Draw.SpriteBatch.Draw((RenderTarget2D)Target, Position, sourceRectangle, OverlayColor);
        }

        private void BeforeRender() {
            if (IsLeader) {
                Background.BeforeRender(Scene, only: StylegroundTag);
                Foreground.BeforeRender(Scene, only: StylegroundTag);

                if (Target == null) { JoinGroup(); }
                Engine.Graphics.GraphicsDevice.SetRenderTarget(Target);
                Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);

                Background.Render(Scene, only: StylegroundTag);
                Foreground.Render(Scene, only: StylegroundTag);
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
