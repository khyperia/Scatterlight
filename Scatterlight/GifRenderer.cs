using System;
using System.Diagnostics;
using System.IO;
using OpenTK;

namespace Scatterlight
{
    class GifRenderer
    {
        private const int Frames = 10;
        private const double ReductionAmount = 32.0;

        public static void RenderGif(KernelManager manager)
        {
            RenderWindow.SetStatus("Rendering gif swish");
            var camera = InputManager.LoadState();
            var left = Vector3d.Cross(camera.Lookat, camera.Up);
            var originalPos = camera.Position;
            var directory = Ext.UniqueDirectory("temp");
            Directory.CreateDirectory(directory);
            var filenames = new string[Frames];
            for (var i = 0; i < Frames; i++)
            {
                var offset = Math.Sin((double)i / Frames * (Math.PI * 2)) * camera.MoveSpeed / ReductionAmount;
                camera.Position = originalPos + offset * left;
                var bitmap = manager.GetScreenshot(camera, 480);
                var filename = Ext.UniqueFileInDirectory(directory, "swish", "png");
                bitmap.Save(filename);
                filenames[i] = filename;
            }
            RenderWindow.SetStatus("Converting to gif");
            var file = Ext.UniqueFilename("render", "gif");
            var psi = new ProcessStartInfo("C:\\Program Files\\ImageMagick-6.8.7-Q16\\Convert.exe", "\"" + string.Join("\" \"", filenames) + "\" \"" + file + "\"") { WorkingDirectory = directory };
            Process.Start(psi).WaitForExit();
            Directory.Delete(directory, true);
            RenderWindow.SetStatus("Done rendering gif swish");
        }
    }
}
