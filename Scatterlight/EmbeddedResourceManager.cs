using System;
using System.IO;
using System.Reflection;

namespace Scatterlight
{
    class EmbeddedResourceManager
    {
        public static string GetText(string filename)
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Scatterlight." + filename);
            if (stream == null)
                throw new Exception("Resource " + filename + " not found");
            return new StreamReader(stream).ReadToEnd();
        }
    }
}