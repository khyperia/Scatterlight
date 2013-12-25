using System;
using System.Runtime.InteropServices;
using Cloo;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace Scatterlight
{
    class GraphicsInterop : IDisposable
    {
        [DllImport("opengl32.dll")]
        extern static IntPtr wglGetCurrentDC();

        private readonly ComputeDevice _device;
        private readonly ComputeContext _context;
        private readonly int _pub;
        private readonly int _texture;
        private readonly ComputeCommandQueue _queue;
        private ComputeBuffer<Vector4> _openCl;
        private int _width;
        private int _height;

        public GraphicsInterop()
        {
            var glHandle = ((IGraphicsContextInternal)GraphicsContext.CurrentContext).Context.Handle;
            var wglHandle = wglGetCurrentDC();
            _device = ComputePlatform.Platforms[0].Devices[0];
            var p1 = new ComputeContextProperty(ComputeContextPropertyName.Platform, Device.Platform.Handle.Value);
            var p2 = new ComputeContextProperty(ComputeContextPropertyName.CL_GL_CONTEXT_KHR, glHandle);
            var p3 = new ComputeContextProperty(ComputeContextPropertyName.CL_WGL_HDC_KHR, wglHandle);
            var cpl = new ComputeContextPropertyList(new[] { p1, p2, p3 });
            _context = new ComputeContext(ComputeDeviceTypes.Gpu, cpl, null, IntPtr.Zero);
            _queue = new ComputeCommandQueue(Context, Device, ComputeCommandQueueFlags.None);

            GL.ClearColor(0f, 0f, 1f, 1f);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(0, 1.0f, 0, 1.0f, -1.0f, 1.0f);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            GL.GenBuffers(1, out _pub);
            GL.Enable(EnableCap.Texture2D);
            _texture = GL.GenTexture();
        }

        public void OnResize(int width, int height)
        {
            _width = width;
            _height = height;
            GL.Viewport(0, 0, width, height);
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, _pub);
            GL.BufferData(BufferTarget.PixelUnpackBuffer, new IntPtr(width * height * sizeof(float) * 4), IntPtr.Zero, BufferUsageHint.DynamicCopy);
            if (_openCl != null)
                _openCl.Dispose();
            _openCl = ComputeBuffer<Vector4>.CreateFromGLBuffer<Vector4>(_queue.Context, ComputeMemoryFlags.WriteOnly, _pub);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            const int glLinear = 9729;
            GL.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, new[] { glLinear });
            GL.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, new[] { glLinear });
        }

        public void Draw(Action<ComputeBuffer<Vector4>, ComputeCommandQueue> renderer)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Finish();
            _queue.AcquireGLObjects(new[] { _openCl }, null);
            renderer(_openCl, _queue);
            _queue.ReleaseGLObjects(new[] { _openCl }, null);
            _queue.Finish();
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, _pub);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, _width, _height, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.Begin(BeginMode.Quads);
            GL.TexCoord2(0f, 1f);
            GL.Vertex3(0f, 0f, 0f);
            GL.TexCoord2(0f, 0f);
            GL.Vertex3(0f, 1f, 0f);
            GL.TexCoord2(1f, 0f);
            GL.Vertex3(1f, 1f, 0f);
            GL.TexCoord2(1f, 1f);
            GL.Vertex3(1f, 0f, 0f);
            GL.End();
        }

        public void Dispose()
        {
            Context.Dispose();
            _queue.Dispose();
            _openCl.Dispose();
            var tmpPub = _pub;
            GL.DeleteBuffers(1, ref tmpPub);
            GL.DeleteTexture(_texture);
        }

        public ComputeContext Context
        {
            get { return _context; }
        }

        public ComputeDevice Device
        {
            get { return _device; }
        }
    }
}
