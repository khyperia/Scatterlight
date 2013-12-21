using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AviFile;

namespace Scatterlight
{
    static class VideoRenderer
    {
        private const string VideoFilename = "video.xml";

        private const int StepsPerPoint = 60;
        private const int FrameCount = 10;
        private static readonly List<CameraConfig> Frames = Load();
        private static int? _frame;
        private static AviManager _aviManager;
        private static VideoStream _videoStream;

        public static void AddFrame(CameraConfig config)
        {
            config.Frame = FrameCount;
            Frames.Add(config);
            Save();
        }

        public static void ClearFrames()
        {
            Frames.Clear();
            Save();
        }

        public static void TakeVideo()
        {
            if (_frame.HasValue == false)
                _frame = 0;
        }

        private static List<CameraConfig> Load()
        {
            if (!File.Exists(VideoFilename))
                return new List<CameraConfig>();
            var root = XDocument.Load(VideoFilename).Root;
            return root == null ? new List<CameraConfig>() : root.Elements().Select(InputManager.LoadStateFromElement).ToList();
        }

        private static void Save()
        {
            var element = new XElement("root", Frames.Select(InputManager.SaveStateToElement).Cast<object>().ToArray());
            new XDocument(element).Save(VideoFilename);
        }

        public static bool CheckForVideo(KernelManager kernelManager)
        {
            if (_frame == null)
                return false;
            var i = _frame.Value / StepsPerPoint;
            if (i >= Frames.Count - 1)
            {
                _frame = null;
                _aviManager.Close();
                RenderWindow.SetStatus("Finished video");
                return false;
            }
            RenderWindow.SetStatus("Rendering frame " + _frame.Value + " of " + (Frames.Count - 1) * StepsPerPoint);
            var d0 = i == 0 ? Frames[0] : Frames[i - 1];
            var d1 = Frames[i];
            var d2 = Frames[i + 1];
            var d3 = i == Frames.Count - 2 ? Frames[Frames.Count - 1] : Frames[i + 2];
            var t = (float)(_frame.Value % StepsPerPoint) / StepsPerPoint;
            var config = CameraConfig.CatmullRom(d0, d1, d2, d3, t);
            var bmp = kernelManager.GetScreenshot(config, 720);

            if (_frame.Value % 256 == 0 || _aviManager == null)
            {
                if (_aviManager != null)
                    _aviManager.Close();
                _videoStream = null;
                _aviManager = new AviManager(Ext.UniqueFilename("video", "avi"), false);
            }
            if (_videoStream == null)
                _videoStream = _aviManager.AddVideoStream(false, 25, bmp);
            else
                _videoStream.AddFrame(bmp);

            _frame = _frame.Value + 1;

            return true;
        }
    }
}
