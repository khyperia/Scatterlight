using OpenTK;

namespace Scatterlight
{
    struct CameraConfig
    {
        private Vector3d _position;
        private Vector3d _lookat;
        private Vector3d _up;
        private float _moveSpeed;
        private int _frame;
        private float _fov;

        public CameraConfig(Vector3d position, Vector3d lookat, Vector3d up, float moveSpeed, int frame, float fov)
        {
            _position = position;
            _lookat = Vector3d.Normalize(lookat);
            _up = Vector3d.Cross(Vector3d.Cross(_lookat, Vector3d.Normalize(up)), _lookat);
            _moveSpeed = moveSpeed;
            _frame = frame;
            _fov = fov;
        }

        public Vector3d Position
        {
            get { return _position; }
            set { _position = value; }
        }

        public Vector3d Lookat
        {
            get { return _lookat; }
            set { _lookat = value; }
        }

        public Vector3d Up
        {
            get { return _up; }
            set { _up = value; }
        }

        public float MoveSpeed
        {
            get { return _moveSpeed; }
            set { _moveSpeed = value; }
        }

        public int Frame
        {
            get { return _frame; }
            set { _frame = value; }
        }

        public float Fov
        {
            get { return _fov; }
            set { _fov = value; }
        }

        public float FocalDistance
        {
            get { return _moveSpeed * 3f; }
        }

        public static CameraConfig CatmullRom(CameraConfig p0, CameraConfig p1, CameraConfig p2, CameraConfig p3, float t)
        {
            return new CameraConfig(
                CatmullRom(p0._position, p1._position, p2._position, p3._position, t),
                CatmullRom(p0._lookat, p1._lookat, p2._lookat, p3._lookat, t),
                CatmullRom(p0._up, p1._up, p2._up, p3._up, t),
                CatmullRom(p0._moveSpeed, p1._moveSpeed, p2._moveSpeed, p3._moveSpeed, t),
                p1._frame,
                CatmullRom(p0._fov, p1._fov, p2._fov, p3._fov, t));
        }

        private static Vector3d CatmullRom(Vector3d p0, Vector3d p1, Vector3d p2, Vector3d p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;

            return ((2 * p1) +
                    (-p0 + p2) * t +
                    (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 +
                    (-p0 + 3 * p1 - 3 * p2 + p3) * t3) / 2;
        }

        private static float CatmullRom(float p0, float p1, float p2, float p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;

            return ((2 * p1) +
                    (-p0 + p2) * t +
                    (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 +
                    (-p0 + 3 * p1 - 3 * p2 + p3) * t3) / 2;
        }
    }
}