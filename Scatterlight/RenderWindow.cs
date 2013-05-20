using System;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Input;

namespace Scatterlight
{
    class RenderWindow : GameWindow
    {
        private readonly InputManager _input;
        private readonly KernelManager _kernelManager;
        private readonly GraphicsInterop _interop;

        private const string MainTitle = "Scatterlight";
        private static string _newStatus;
        private static int _timeSinceTitleSwap;

        private int _fps;
        private int _lastFps;

        public RenderWindow(string kernelFile)
            : base(DefaultWidth, DefaultHeight, GraphicsMode.Default, MainTitle, GameWindowFlags.Default, DisplayDevice.Default, 0, 0, GraphicsContextFlags.ForwardCompatible)
        {
            _interop = new GraphicsInterop();
            _input = new InputManager(Keyboard);
            _kernelManager = new KernelManager(_interop, _input, System.IO.File.ReadAllText(kernelFile));
        }

        private static int DefaultWidth
        {
            get
            {
                var width = DisplayDevice.Default.Width - 1;
                width -= width % 32;
                return width;
            }
        }

        private static int DefaultHeight
        {
            get
            {
                var height = DisplayDevice.Default.Height - 65;
                height -= height % 32;
                return height;
            }
        }

        public static void SetStatus(string status)
        {
            _newStatus = status;
        }

        protected override void OnResize(System.EventArgs e)
        {
            base.OnResize(e);
            var width = ClientSize.Width;
            var height = ClientSize.Height;
            _interop.OnResize(width, height);
            _kernelManager.ResizeLaunch(width, height);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            _input.Update((float)e.Time);
            if (Keyboard[Key.Escape])
                Close();
            _timeSinceTitleSwap++;
            if (_timeSinceTitleSwap > 600)
            {
                _newStatus = null;
                _timeSinceTitleSwap = 0;
            }
            _fps++;
            var now = DateTime.UtcNow.Second;
            if (_lastFps != now)
            {
                _lastFps = now;
                Title = MainTitle + " - " + _fps + (_newStatus == null ? "" : " - " + _newStatus);
                _fps = 0;
            }
            base.OnUpdateFrame(e);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            _interop.Draw(_kernelManager.Render);
            SwapBuffers();
            base.OnRenderFrame(e);
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _interop.Dispose();
            _kernelManager.Dispose();
            base.OnClosed(e);
        }
    }
}
