using System.Linq;
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using Celeste;
using Celeste.Mod.Entities;

namespace Celeste.Mod.WindowpaneHelper {
    [Tracked]
    [CustomEntity("WindowpaneHelper/Windowpane")]
    public class Windowpane : Entity {
        public static Dictionary<string, Tuple<VirtualRenderTarget, Windowpane>> GroupLeaderInfo
            = default(Dictionary<string, Tuple<VirtualRenderTarget, Windowpane>>);

        public VirtualRenderTarget Target => (GroupLeaderInfo?[StylegroundTag]?.Item1);
        public Windowpane Leader => (GroupLeaderInfo?[StylegroundTag]?.Item2);
        public bool IsLeader => (Leader == this);

        public Color WipeColor;
        public Color DrawColor;
        public bool Background;
        public bool Foreground;

        public readonly string StylegroundTag;

        private BackdropRenderer bgRenderer = new BackdropRenderer();
        private BackdropRenderer fgRenderer = new BackdropRenderer();

        public Windowpane(EntityData data, Vector2 offset) : base(data.Position + offset) {
            Collider = new Hitbox(data.Width, data.Height);
            Tag = (int)Tags.TransitionUpdate | (int)Tags.FrozenUpdate;
            Depth = data.Int("depth", 11000);

            WipeColor = data.HexColor("wipeColor", Color.Black);
            DrawColor = data.HexColor("drawColor", Color.White);
            Background = data.Bool("background", true);
            Foreground = data.Bool("foreground", false);
            StylegroundTag = data.Attr("stylegroundTag", "");
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
                Windowpane next = (scene as Level).Tracker.GetEntities<Windowpane>()
                                                          .Select(e => (e as Windowpane))
                                                          .Where(e => (e != null && e != this))
                                                          .First();

                if (next == null) {
                    Target.Dispose();
                    GroupLeaderInfo.Remove(StylegroundTag);
                } else {
                    next.JoinGroup(forceLeader: true);
                }
            }
        }

        public override void SceneEnd(Scene scene) {
            if (IsLeader) {
                Target.Dispose();
                GroupLeaderInfo.Remove(StylegroundTag);
            }
        }

        public override void Render() {
            base.Render();

            Vector2 camera_pos = SceneAs<Level>().Camera.Position.Floor();
            Rectangle sourceRectangle = new Rectangle(
                (int)(base.X - camera_pos.X), (int)(base.Y - camera_pos.Y),
                (int)base.Width, (int)base.Height);

            Draw.SpriteBatch.Draw((RenderTarget2D)Target, Position, sourceRectangle, DrawColor);
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
            bgRenderer.Backdrops = SceneAs<Level>().Background.Backdrops
                .Where((bg) => bg.Tags.Contains(StylegroundTag))
                .ToList<Backdrop>();
            fgRenderer.Backdrops = SceneAs<Level>().Background.Backdrops
                .Where((bg) => bg.Tags.Contains(StylegroundTag))
                .ToList<Backdrop>();
        }
    }
}