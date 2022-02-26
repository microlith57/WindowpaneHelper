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
        // a group of windowpanes, all sharing a single styleground tag.
        // the relevant stylegrounds are rendered to a render target for the group, and then parts of that
        //   render target are rendered to the individual window entities.
        public struct Group {
            // the windowpane in charge of the render target
            public Windowpane Leader;
            // the render target itself
            public VirtualRenderTarget Target;
            // whether any windowpanes are visible this frame
            public bool AnyVisible = false;
            internal bool _shouldResetAnyVisible = true;

            public Group(Windowpane leader, VirtualRenderTarget target) {
                Leader = leader; Target = target;
            }
        }

        // used to store the render target and leading windowpane for each styleground tag
        public static Dictionary<string, Group> Groups;

        public ForcefulBackdropRenderer Background;
        public ForcefulBackdropRenderer Foreground;

        // quality of life getters
        public bool InGroup => Groups.ContainsKey(StylegroundTag);
        public Windowpane Leader => InGroup ? Groups[StylegroundTag].Leader : null;
        public VirtualRenderTarget Target => InGroup ? Groups[StylegroundTag].Target : null;
        public bool AnyVisible => InGroup && Groups[StylegroundTag].AnyVisible;
        public bool IsLeader => Leader != null && Leader == this;

        // keeping track of whether panes are visible
        private bool _renderingThisFrame = true;
        public bool RenderingThisFrame => (AnyVisible && _renderingThisFrame);

        public Color WipeColor;
        public Color OverlayColor;
        public BlendState BlendState;

        public bool PunchThrough;
        public bool RenderingBelow;
        public bool RenderingAbove;

        // list of (flag names, invert?) tuples
        public readonly List<Tuple<string, bool>> Flags = new List<Tuple<string, bool>>();

        // used to group windowpanes into groups
        public readonly string StylegroundTag;

        public Vector2? Node;

        public Windowpane(EntityData data, Vector2 offset) : base(data.Position + offset) {
            Groups ??= new Dictionary<string, Group>();

            Tag |= (byte)Tags.TransitionUpdate | (byte)Tags.FrozenUpdate;

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

            foreach (var raw_flag in data.Attr("flags", "").Split(',')) {
                var flag = raw_flag.Trim(new char[] { ' ' });
                if (flag.Length == 0) { continue; }

                if (flag.StartsWith("!")) {
                    Flags.Add(new Tuple<string, bool>(flag.TrimStart(new char[] { '!' }), true));
                } else {
                    Flags.Add(new Tuple<string, bool>(flag, false));
                }
            }

            PunchThrough = data.Bool("punchThrough", false);

            Background = new ForcefulBackdropRenderer();
            Foreground = new ForcefulBackdropRenderer();

            Node = data.FirstNodeNullable(offset);
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            Background.Backdrops = SceneAs<Level>().Background.Backdrops.Where(b => b.Tags.Contains(StylegroundTag)).ToList();
            Foreground.Backdrops = SceneAs<Level>().Foreground.Backdrops.Where(b => b.Tags.Contains(StylegroundTag)).ToList();

            Add(new BeforeRenderHook(BeforeRender));

            JoinGroup();
        }

        public override void Removed(Scene scene) {
            // transfer leadership if possible; otherwse cleanup
            if (IsLeader) {
                var next = (scene as Level).Tracker.GetEntities<Windowpane>()
                                                   .Select(e => (e as Windowpane))
                                                   .Where(e => (e != null && e != this && e.StylegroundTag == this.StylegroundTag));

                if (next.Any()) {
                    next.First().JoinGroup(forceLeader: true);
                } else {
                    Target.Dispose();
                    Groups.Remove(StylegroundTag);
                }
            }
            base.Removed(scene);
        }

        public override void SceneEnd(Scene scene) {
            // cleanup
            if (IsLeader) {
                Target.Dispose();
                Groups.Remove(StylegroundTag);
            }
            Background.Backdrops = new List<Backdrop>();
            Foreground.Backdrops = new List<Backdrop>();
            base.SceneEnd(scene);
        }

        public override void HandleGraphicsReset() {
            if (!IsLeader) { return; }

            if (!InGroup) { JoinGroup(); }
            var group = Groups[StylegroundTag];
            if (group.Target == null || group.Target.IsDisposed) {
                group.Target = CreateRenderTarget();
            }
            Groups[StylegroundTag] = group;
        }

        public override void Update() {
            base.Update();

            _renderingThisFrame = true;
            foreach (var flag in Flags) {
                if (flag.Item2) {
                    _renderingThisFrame &= !SceneAs<Level>().Session.GetFlag(flag.Item1);
                } else {
                    _renderingThisFrame &= SceneAs<Level>().Session.GetFlag(flag.Item1);
                }
            }

            if (!InGroup) { JoinGroup(); }

            // set group visibility
            var group = Groups[StylegroundTag];
            if (group._shouldResetAnyVisible) {
                group.AnyVisible = false;
                group._shouldResetAnyVisible = false;
            }
            group.AnyVisible |= _renderingThisFrame;
            Groups[StylegroundTag] = group;
        }

        public override void Render() {
            // unset group visibility
            if (!Groups[StylegroundTag]._shouldResetAnyVisible) {
                var group = Groups[StylegroundTag];
                group._shouldResetAnyVisible = true;
                Groups[StylegroundTag] = group;
            }

            if (!RenderingThisFrame) { return; }

            base.Render();

            // no point creating the target now, it won't have anything in it
            if (Target == null || Target.IsDisposed) { return; }

            // punch through if necessary
            if (PunchThrough) {
                Draw.SpriteBatch.End();
                Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, (Scene as Level).GameplayRenderer.Camera.Matrix);

                Draw.Rect(Position, Width, Height, Color.Transparent);
            }

            // draw the interior if that should be done now
            if (!RenderingBelow && !RenderingAbove) {
                DrawInterior(Scene as Level);
            }

            // restore default spritebatch settings if necessary
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
            if (IsLeader && AnyVisible) {
                Background.BeforeRender(Scene);
                Foreground.BeforeRender(Scene);

                // initialise & fill the render target
                JoinGroup();
                Engine.Graphics.GraphicsDevice.SetRenderTarget(Target);
                Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);

                Background.Render(Scene);
                Foreground.Render(Scene);
            }
        }

        public static void RenderBehindLevel(Level level) {
            // render all the windowpanes that should be rendered now
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, level.GameplayRenderer.Camera.Matrix);
            foreach (Windowpane pane in level.Tracker.GetEntities<Windowpane>()) {
                if (pane.RenderingBelow && pane.RenderingThisFrame) { pane.DrawInterior(level); }
            }
            Draw.SpriteBatch.End();
        }

        public static void RenderAboveLevel(Level level) {
            // render all the windowpanes that should be rendered now
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, level.GameplayRenderer.Camera.Matrix);
            foreach (Windowpane pane in level.Tracker.GetEntities<Windowpane>()) {
                if (pane.RenderingAbove && pane.RenderingThisFrame) { pane.DrawInterior(level); }
            }
            Draw.SpriteBatch.End();
        }

        internal void JoinGroup(bool forceLeader = false) {
            if (forceLeader || !InGroup || Leader == null || Target == null || Target.IsDisposed) {
                if (!InGroup) {
                    Groups[StylegroundTag] = new Group(this, CreateRenderTarget());
                }
                var group = Groups[StylegroundTag];
                group.Leader = this;
                Groups[StylegroundTag] = group;
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

        private VirtualRenderTarget CreateRenderTarget() {
            return VirtualContent.CreateRenderTarget("microlith57-windowpanehelper-" + StylegroundTag, 320, 180);
        }
    }
}
