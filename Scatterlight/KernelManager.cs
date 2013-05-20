using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using Cloo;
using OpenTK;

namespace Scatterlight
{
    class KernelManager : IDisposable
    {
        private readonly InputManager _input;
        private readonly ComputeProgram _program;
        private readonly ComputeKernel[] _kernels;
        private readonly long[] _localSize;
        private long[] _globalSize;
        private int _width;
        private int _height;
        private int _trueFrame;

        private const int ScreenshotHeight = 2048;
        private const double ScreenshotAspectRatio = 16.0 / 9.0;
        private const int ScreenshotWidth = (int)(ScreenshotHeight * ScreenshotAspectRatio);

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
            if (_kernels == null)
                return;
            CoreRender(buffer, queue, _kernels, new Vector4(_input.Position), new Vector4(_input.Lookat), new Vector4(_input.Up), _input.Frame, _trueFrame, _width, _height, _globalSize, _localSize);
            _input.Frame++;
            _trueFrame++;
            if (_input.CheckForScreenshot())
                ThreadPool.QueueUserWorkItem(o => Screenshot());
        }

        private static void CoreRender(ComputeMemory buffer, ComputeCommandQueue queue, IEnumerable<ComputeKernel> kernels, Vector4 position, Vector4 lookat, Vector4 up, int frame, int trueFrame, int width, int height, long[] globalSize, long[] localSize)
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
                queue.Execute(kernel, new long[2], globalSize, localSize, null);
                queue.Finish();
            }
        }

        private void Screenshot()
        {
            RenderWindow.SetStatus("Rendering screenshot");

            var computeBuffer = new ComputeBuffer<Vector4>(_program.Context, ComputeMemoryFlags.ReadWrite, ScreenshotWidth * ScreenshotHeight);
            var queue = new ComputeCommandQueue(_program.Context, _program.Context.Devices[0], ComputeCommandQueueFlags.None);

            Vector3d position, lookat, up;
            float moveSpeed;
            int frame;
            InputManager.LoadState(out position, out lookat, out up, out moveSpeed, out frame);

            var globalSize = GlobalLaunchsizeFor(ScreenshotWidth, ScreenshotHeight);

            for (var i = 0; i < 8; i++)
                CoreRender(computeBuffer, queue, _kernels, new Vector4((Vector3)position), new Vector4((Vector3)lookat), new Vector4((Vector3)up), 0, i, ScreenshotWidth, ScreenshotHeight, globalSize, _localSize);
            for (var i = 0; i < frame; i++)
                CoreRender(computeBuffer, queue, _kernels, new Vector4((Vector3)position), new Vector4((Vector3)lookat), new Vector4((Vector3)up), i, i, ScreenshotWidth, ScreenshotHeight, globalSize, _localSize);

            var pixels = new Vector4[ScreenshotWidth * ScreenshotHeight];
            queue.ReadFromBuffer(computeBuffer, ref pixels, true, null);
            queue.Finish();

            RenderWindow.SetStatus("Saving screenshot");

            var bmp = new Bitmap(ScreenshotWidth, ScreenshotHeight);
            for (var y = 0; y < ScreenshotHeight; y++)
            {
                for (var x = 0; x < ScreenshotWidth; x++)
                {
                    var pixel = pixels[x + y * ScreenshotWidth];
                    if (float.IsNaN(pixel.X) || float.IsNaN(pixel.Y) || float.IsNaN(pixel.Z))
                    {
                        Console.WriteLine("Warning! Caught NAN pixel while taking screenshot!");
                        continue;
                    }
                    bmp.SetPixel(x, y, Color.FromArgb((byte)(pixel.X * 255), (byte)(pixel.Y * 255), (byte)(pixel.Z * 255)));
                }
            }
            var screenshotNumber = 0;
            while (File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "screenshot" + screenshotNumber + ".png")))
                screenshotNumber++;
            bmp.Save(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "screenshot" + screenshotNumber + ".png"));
            RenderWindow.SetStatus("Took screenshot #" + screenshotNumber);
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
