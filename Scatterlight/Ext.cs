using System;
using System.IO;

namespace Scatterlight
{
    static class Ext
    {
        public static string UniqueFilename(string filename, string ext)
        {
            return UniqueFileInDirectory(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), filename, ext);
        }

        public static string UniqueFileInDirectory(string directory, string filename, string ext)
        {
            var screenshotNumber = 0;
            string file;
            while (File.Exists(file = Path.Combine(directory, string.Format("{0}{1}.{2}", filename, screenshotNumber, ext))))
                screenshotNumber++;
            return file;
        }

        public static string UniqueDirectory(string dirname)
        {
            var screenshotNumber = 0;
            string file;
            while (Directory.Exists(file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), string.Format("{0}{1}", dirname, screenshotNumber))))
                screenshotNumber++;
            return file;
        }
    }
}