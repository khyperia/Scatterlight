using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Cloo;
using OpenTK;

namespace Scatterlight
{
    class KernelManager : IDisposable
    {
        private static readonly long[] LaunchSize = new long[2];
        private readonly InputManager _input;
        private readonly ComputeProgram _program;
        private readonly ComputeKernel[] _kernels;
        private readonly long[] _localSize;
        private long[] _globalSize;
        private int _width;
        private int _height;
        private int _trueFrame;
        private bool _doingScreenshot;

        private const int SlowRenderCount = 2;
        private const double ScreenshotAspectRatio = 16.0 / 9.0;

        public KernelManager(GraphicsInterop interop, InputManager input, string source)
        {
            _input = input;
            var localSizeSingle = (long)Math.Sqrt(interop.Device.MaxWorkGroupSize);
            _localSize = new[] { localSizeSingle, localSizeSingle };
            //_localSize = new[] { interop.Device.MaxWorkGroupSize, 1 };

            _program = new ComputeProgram(interop.Context, source);
            try
            {
                _program.Build(new[] { interop.Device }, "", null, IntPtr.Zero);
            }
            catch (InvalidBinaryComputeException)
            {
                Console.WriteLine(_program.GetBuildLog(interop.Device));
                return;
            }
            catch (BuildProgramFailureComputeException)
            {
                Console.WriteLine(_program.GetBuildLog(interop.Device));
                return;
            }
            Console.WriteLine(_program.GetBuildLog(interop.Device));
            _kernels = _program.CreateAllKernels().ToArray();
        }

        public void ResizeLaunch(int width, int height)
        {
            _width = width;
            _height = height;
            _globalSize = GlobalLaunchsizeFor(width, height);
        }

        private long[] GlobalLaunchsizeFor(int width, int height)
        {
            return new[]
                {
                    (width + _localSize[0] - 1) / _localSize[0] * _localSize[0],
                    (height + _localSize[1] - 1) / _localSize[1] * _localSize[1]
                };
        }

        public void Render(ComputeMemory buffer, ComputeCommandQueue queue)
        {
            if (_doingScreenshot || _kernels == null)
                return;
            if (VideoRenderer.CheckForVideo(this))
                return;
            CoreRender(buffer, queue, _kernels, new Vector4((Vector3)_input.Camera.Position), new Vector4((Vector3)_input.Camera.Lookat), new Vector4((Vector3)_input.Camera.Up), _input.Camera.Frame, _trueFrame, _input.Camera.Fov, _width, _height, _globalSize, _localSize);
            _input.Frame++;
            _trueFrame++;
            if (_input.CheckForScreenshot())
            {
                _doingScreenshot = true;
                ThreadPool.QueueUserWorkItem(o => Screenshot());
            }
            if (_input.CheckForGif())
            {
                _doingScreenshot = true;
                ThreadPool.QueueUserWorkItem(o =>
                {
                    GifRenderer.RenderGif(this);
                    _doingScreenshot = false;
                });
            }
        }

        private static void CoreRender(ComputeMemory buffer, ComputeCommandQueue queue, IEnumerable<ComputeKernel> kernels, Vector4 position, Vector4 lookat, Vector4 up, int frame, int trueFrame, float fov, int width, int height, long[] globalSize, long[] localSize)
        {
            foreach (var kernel in kernels)
            {
                kernel.SetMemoryArgument(0, buffer);
                kernel.SetValueArgument(1, width);
                kernel.SetValueArgument(2, height);
                kernel.SetValueArgument(3, position);
                kernel.SetValueArgument(4, lookat);
                kernel.SetValueArgument(5, up);
                kernel.SetValueArgument(6, frame);
                kernel.SetValueArgument(7, trueFrame);
                kernel.SetValueArgument(8, fov);
                queue.Execute(kernel, LaunchSize, globalSize, localSize, null);
                queue.Finish();
            }
        }

        public Bitmap GetScreenshot(CameraConfig camera, int screenshotHeight)
        {
            var screenshotWidth = (int) (screenshotHeight*ScreenshotAspectRatio);
            var computeBuffer = new ComputeBuffer<Vector4>(_program.Context, ComputeMemoryFlags.ReadWrite, screenshotWidth * screenshotHeight);
            var queue = new ComputeCommandQueue(_program.Context, _program.Context.Devices[0], ComputeCommandQueueFlags.None);

            var globalSize = GlobalLaunchsizeFor(screenshotWidth, screenshotHeight);

            for (var i = 0; i < SlowRenderCount; i++)
                CoreRender(computeBuffer, queue, _kernels, new Vector4((Vector3)camera.Position), new Vector4((Vector3)camera.Lookat), new Vector4((Vector3)camera.Up), 0, i, camera.Fov, screenshotWidth, screenshotHeight, globalSize, _localSize);
            for (var i = 0; i < camera.Frame; i++)
                CoreRender(computeBuffer, queue, _kernels, new Vector4((Vector3)camera.Position), new Vector4((Vector3)camera.Lookat), new Vector4((Vector3)camera.Up), i, i, camera.Fov, screenshotWidth, screenshotHeight, globalSize, _localSize);

            var pixels = new Vector4[screenshotWidth * screenshotHeight];
            queue.ReadFromBuffer(computeBuffer, ref pixels, true, null);
            queue.Finish();

            computeBuffer.Dispose();
            queue.Dispose();

            var bmp = new Bitmap(screenshotWidth, screenshotHeight);
            var destBuffer = new int[screenshotWidth * screenshotHeight];
            for (var y = 0; y < screenshotHeight; y++)
            {
                for (var x = 0; x < screenshotWidth; x++)
                {
                    var pixel = pixels[x + y * screenshotWidth];
                    if (float.IsNaN(pixel.X) || float.IsNaN(pixel.Y) || float.IsNaN(pixel.Z))
                    {
                        Console.WriteLine("Warning! Caught NAN pixel while taking screenshot!");
                        continue;
                    }
                    destBuffer[y * screenshotWidth + x] = (byte)(pixel.X * 255) << 16 | (byte)(pixel.Y * 255) << 8 | (byte)(pixel.Z * 255);
                }
            }
            var bmpData = bmp.LockBits(new Rectangle(0, 0, screenshotWidth, screenshotHeight), ImageLockMode.ReadWrite, PixelFormat.Format32bppRgb);
            Marshal.Copy(destBuffer, 0, bmpData.Scan0, destBuffer.Length);
            bmp.UnlockBits(bmpData);

            return bmp;
        }

        private void Screenshot()
        {
            RenderWindow.SetStatus("Rendering screenshot");

            var bmp = GetScreenshot(InputManager.LoadState(), 2048);

            RenderWindow.SetStatus("Saving screenshot");

            
            bmp.Save(Ext.UniqueFilename("screenshot", "png"));
            RenderWindow.SetStatus("Took screenshot");
            _doingScreenshot = false;
        }

        public void Dispose()
        {
            if (_kernels != null)
                foreach (var kernel in _kernels)
                    kernel.Dispose();
            _program.Dispose();
        }
    }
}
