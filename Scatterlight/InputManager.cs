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
        private Vector3d _position;
        private Vector3d _lookat;
        private Vector3d _up;
        private bool _screenshot;
        private float _moveSpeed = 1f;
        private float _fov = 1f;
        private const float TurnSpeed = 1f;
        private readonly Dictionary<Key, Action<float>> _bindings;
        private const string StateFilename = "state.xml";

        public InputManager(KeyboardDevice keyboard)
        {
            Frame = 0;
            _position = new Vector3d(10, 0, 0);
            _lookat = new Vector3d(-1, 0, 0);
            _up = new Vector3d(0, 1, 0);
            _keyboard = keyboard;
            _bindings = new Dictionary<Key, Action<float>>
                {
                    {Key.W, dt => _position += _lookat * dt * _moveSpeed},
                    {Key.S, dt => _position -= _lookat * dt * _moveSpeed},
                    {Key.A, dt => _position += Vector3d.Cross(_up, _lookat) * dt * _moveSpeed},
                    {Key.D, dt => _position -= Vector3d.Cross(_up, _lookat) * dt * _moveSpeed},
                    {Key.ShiftLeft, dt => _position += _up * dt * _moveSpeed},
                    {Key.Space, dt => _position -= _up * dt * _moveSpeed},
                    {Key.Q, dt => _up = Vector3d.Transform(_up, Matrix4d.CreateFromAxisAngle(_lookat, TurnSpeed * dt))},
                    {Key.E, dt => _up = Vector3d.Transform(_up, Matrix4d.CreateFromAxisAngle(_lookat, -TurnSpeed * dt))},
                    {Key.Left, dt => _lookat = Vector3d.Transform(_lookat, Matrix4d.CreateFromAxisAngle(_up, TurnSpeed * dt * _fov))},
                    {Key.Right, dt => _lookat = Vector3d.Transform(_lookat, Matrix4d.CreateFromAxisAngle(_up, -TurnSpeed * dt * _fov))},
                    {Key.Up, dt => _lookat = Vector3d.Transform(_lookat, Matrix4d.CreateFromAxisAngle(Vector3d.Cross(_up, _lookat), TurnSpeed * dt * _fov))},
                    {Key.Down, dt => _lookat = Vector3d.Transform(_lookat, Matrix4d.CreateFromAxisAngle(Vector3d.Cross(_up, _lookat), -TurnSpeed * dt * _fov))},
                    {Key.R, dt => _moveSpeed *= 1 + dt},
                    {Key.F, dt =>  _moveSpeed *= 1 - dt},
                    {Key.N, dt => _fov *= 1 + dt},
                    {Key.M, dt =>  _fov *= 1 - dt}
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
                case Key.L:
                    SaveState();
                    RenderWindow.SetStatus("Saved state");
                    break;
                case Key.K:
                    LoadState();
                    RenderWindow.SetStatus("Loaded state");
                    break;
            }
        }

        private void SaveState()
        {
            var xdoc = new XDocument(new XElement("state",
                                    new XElement("position",
                                                 new XElement("x", _position.X.ToString(CultureInfo.InvariantCulture)),
                                                 new XElement("y", _position.Y.ToString(CultureInfo.InvariantCulture)),
                                                 new XElement("z", _position.Z.ToString(CultureInfo.InvariantCulture))),
                                    new XElement("lookat",
                                                 new XElement("x", _lookat.X.ToString(CultureInfo.InvariantCulture)),
                                                 new XElement("y", _lookat.Y.ToString(CultureInfo.InvariantCulture)),
                                                 new XElement("z", _lookat.Z.ToString(CultureInfo.InvariantCulture))),
                                    new XElement("up",
                                                 new XElement("x", _up.X.ToString(CultureInfo.InvariantCulture)),
                                                 new XElement("y", _up.Y.ToString(CultureInfo.InvariantCulture)),
                                                 new XElement("z", _up.Z.ToString(CultureInfo.InvariantCulture))),
                                    new XElement("movespeed", _moveSpeed.ToString(CultureInfo.InvariantCulture)),
                                    new XElement("frame", Frame.ToString(CultureInfo.InvariantCulture)),
                                    new XElement("fov", _fov.ToString(CultureInfo.InvariantCulture))));
            xdoc.Save(StateFilename);
        }

        private void LoadState()
        {
            if (System.IO.File.Exists(StateFilename) == false)
                return;
            int frame;
            LoadState(out _position, out _lookat, out _up, out _moveSpeed, out frame, out _fov);
            Frame = frame;
        }

        public static void LoadState(out Vector3d position, out Vector3d lookat, out Vector3d up, out float moveSpeed, out int frame, out float fov)
        {
            var root = XDocument.Load(StateFilename).Root;
            position = ParseVector3(root.Element("position"));
            lookat = ParseVector3(root.Element("lookat"));
            up = ParseVector3(root.Element("up"));
            moveSpeed = float.Parse(root.Element("movespeed").Value);
            frame = int.Parse(root.Element("frame").Value);
            fov = float.Parse(root.Element("fov").Value);
        }

        private static Vector3d ParseVector3(XContainer element)
        {
            return new Vector3d(double.Parse(element.Element("x").Value),
                                double.Parse(element.Element("y").Value),
                                double.Parse(element.Element("z").Value));
        }

        public Vector3 Position
        {
            get { return (Vector3)_position; }
        }

        public Vector3 Lookat
        {
            get { return (Vector3)_lookat; }
        }

        public Vector3 Up
        {
            get { return (Vector3)_up; }
        }

        public float Fov
        {
            get { return _fov; }
        }

        public int Frame { get; set; }

        public void Update(float dt)
        {
            var keyDown = false;
            foreach (var binding in _bindings.Where(binding => _keyboard[binding.Key]))
            {
                binding.Value(dt);
                keyDown = true;
            }
            if (keyDown)
                Frame = 0;
            _up = Vector3d.Cross(Vector3d.Cross(_lookat, _up), _lookat);
            _lookat.Normalize();
            _up.Normalize();
        }

        public bool CheckForScreenshot()
        {
            var value = _screenshot;
            _screenshot = false;
            return value;
        }
    }
}
