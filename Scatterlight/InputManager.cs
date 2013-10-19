using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using OpenTK;
using OpenTK.Input;

namespace Scatterlight
{
    class InputManager
    {
        private readonly KeyboardDevice _keyboard;
        private CameraConfig _config;
        private bool _screenshot;
        private const float TurnSpeed = 1f;
        private readonly Dictionary<Key, Action<float>> _bindings;
        private const string StateFilename = "state.xml";

        public InputManager(KeyboardDevice keyboard)
        {
            _config = new CameraConfig(new Vector3d(10, 0, 0), new Vector3d(-1, 0, 0), new Vector3d(0, 1, 0), 1, 0, 1);
            _keyboard = keyboard;
            _bindings = new Dictionary<Key, Action<float>>
                {
                    {Key.W, dt => _config.Position += _config.Lookat * dt * _config.MoveSpeed},
                    {Key.S, dt => _config.Position -= _config.Lookat * dt * _config.MoveSpeed},
                    {Key.A, dt => _config.Position += Vector3d.Cross(_config.Up, _config.Lookat) * dt * _config.MoveSpeed},
                    {Key.D, dt => _config.Position -= Vector3d.Cross(_config.Up, _config.Lookat) * dt * _config.MoveSpeed},
                    {Key.ShiftLeft, dt => _config.Position += _config.Up * dt * _config.MoveSpeed},
                    {Key.Space, dt => _config.Position -= _config.Up * dt * _config.MoveSpeed},
                    {Key.Q, dt => _config.Up = Vector3d.Transform(_config.Up, Matrix4d.CreateFromAxisAngle(_config.Lookat, TurnSpeed * dt))},
                    {Key.E, dt => _config.Up = Vector3d.Transform(_config.Up, Matrix4d.CreateFromAxisAngle(_config.Lookat, -TurnSpeed * dt))},
                    {Key.Left, dt => _config.Lookat = Vector3d.Transform(_config.Lookat, Matrix4d.CreateFromAxisAngle(_config.Up, TurnSpeed * dt * _config.Fov))},
                    {Key.Right, dt => _config.Lookat = Vector3d.Transform(_config.Lookat, Matrix4d.CreateFromAxisAngle(_config.Up, -TurnSpeed * dt * _config.Fov))},
                    {Key.Up, dt => _config.Lookat = Vector3d.Transform(_config.Lookat, Matrix4d.CreateFromAxisAngle(Vector3d.Cross(_config.Up, _config.Lookat), TurnSpeed * dt * _config.Fov))},
                    {Key.Down, dt => _config.Lookat = Vector3d.Transform(_config.Lookat, Matrix4d.CreateFromAxisAngle(Vector3d.Cross(_config.Up, _config.Lookat), -TurnSpeed * dt * _config.Fov))},
                    {Key.R, dt => _config.MoveSpeed *= 1 + dt},
                    {Key.F, dt =>  _config.MoveSpeed *= 1 - dt},
                    {Key.N, dt => _config.Fov *= 1 + dt},
                    {Key.M, dt => _config. Fov *= 1 - dt}
                };
            _keyboard.KeyDown += KeyboardOnKeyDown;
        }

        private void KeyboardOnKeyDown(object sender, KeyboardKeyEventArgs keyboardKeyEventArgs)
        {
            switch (keyboardKeyEventArgs.Key)
            {
                case Key.P:
                    _screenshot = true;
                    break;
                case Key.O:
                    VideoRenderer.TakeVideo();
                    RenderWindow.SetStatus("Started video");
                    break;
                case Key.J:
                    VideoRenderer.AddFrame(_config);
                    RenderWindow.SetStatus("Added keyframe");
                    break;
                case Key.H:
                    VideoRenderer.ClearFrames();
                    RenderWindow.SetStatus("Cleared keyframes");
                    break;
                case Key.L:
                    SaveState();
                    RenderWindow.SetStatus("Saved state");
                    break;
                case Key.K:
                    LoadStateInst();
                    RenderWindow.SetStatus("Loaded state");
                    break;
            }
        }

        private void SaveState()
        {
            var xdoc = new XDocument(SaveStateToElement(_config));
            xdoc.Save(StateFilename);
        }

        public static XElement SaveStateToElement(CameraConfig config)
        {
            return new XElement("state",
                new XElement("position",
                    new XElement("x", config.Position.X.ToString(CultureInfo.InvariantCulture)),
                    new XElement("y", config.Position.Y.ToString(CultureInfo.InvariantCulture)),
                    new XElement("z", config.Position.Z.ToString(CultureInfo.InvariantCulture))),
                new XElement("lookat",
                    new XElement("x", config.Lookat.X.ToString(CultureInfo.InvariantCulture)),
                    new XElement("y", config.Lookat.Y.ToString(CultureInfo.InvariantCulture)),
                    new XElement("z", config.Lookat.Z.ToString(CultureInfo.InvariantCulture))),
                new XElement("up",
                    new XElement("x", config.Up.X.ToString(CultureInfo.InvariantCulture)),
                    new XElement("y", config.Up.Y.ToString(CultureInfo.InvariantCulture)),
                    new XElement("z", config.Up.Z.ToString(CultureInfo.InvariantCulture))),
                new XElement("movespeed", config.MoveSpeed.ToString(CultureInfo.InvariantCulture)),
                new XElement("frame", config.Frame.ToString(CultureInfo.InvariantCulture)),
                new XElement("fov", config.Fov.ToString(CultureInfo.InvariantCulture)));
        }

        private void LoadStateInst()
        {
            if (System.IO.File.Exists(StateFilename) == false)
                return;
            _config = LoadState();
        }

        public static CameraConfig LoadState()
        {
            var root = XDocument.Load(StateFilename).Root;
            return LoadStateFromElement(root);
        }

        public static CameraConfig LoadStateFromElement(XElement root)
        {
            return new CameraConfig(
                ParseVector3(root.Element("position")),
                ParseVector3(root.Element("lookat")),
                ParseVector3(root.Element("up")),
                float.Parse(root.Element("movespeed").Value),
                int.Parse(root.Element("frame").Value),
                float.Parse(root.Element("fov").Value));
        }

        private static Vector3d ParseVector3(XContainer element)
        {
            return new Vector3d(double.Parse(element.Element("x").Value),
                                double.Parse(element.Element("y").Value),
                                double.Parse(element.Element("z").Value));
        }

        public void Update(float dt)
        {
            var keyDown = false;
            foreach (var binding in _bindings.Where(binding => _keyboard[binding.Key]))
            {
                binding.Value(dt);
                keyDown = true;
            }
            if (keyDown)
                _config.Frame = 0;
            _config.Up = Vector3d.Cross(Vector3d.Cross(_config.Lookat, _config.Up), _config.Lookat);
            _config.Lookat = Vector3d.Normalize(_config.Lookat);
            _config.Up = Vector3d.Normalize(_config.Up);
        }

        public bool CheckForScreenshot()
        {
            var value = _screenshot;
            _screenshot = false;
            return value;
        }

        public CameraConfig Camera
        {
            get { return _config; }
        }

        public int Frame
        {
            get { return _config.Frame; }
            set { _config.Frame = value; }
        }
    }
}
