using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Resources;
using System.Xml;

namespace GeoMap
{
    internal static class MapResolver
    {
        public static async Task<RootObject> GetJsonMapAsync(string filepath)
        {
            RootObject rootObject = null;
            if (string.IsNullOrEmpty(filepath))
            {
                return rootObject;
            }
            try
            {
                Uri resourceuri = new Uri(filepath, UriKind.Relative);
                StreamResourceInfo resouceinfo = Application.GetResourceStream(resourceuri);

                if (resouceinfo != null && resouceinfo.Stream.Length > 0)
                {
                    using (StreamReader stream = new StreamReader(resouceinfo.Stream))
                    {
                        string resstring = await stream.ReadToEndAsync();
                        return JsonConvert.DeserializeObject<RootObject>(resstring);
                    }
                }
            }
            catch{ }
            try
            {
                if (!File.Exists(filepath))
                {
                    throw new FileNotFoundException(String.Format("This file {0} was not found.", filepath));
                }
                string filestr = File.ReadAllText(filepath);
                rootObject = JsonConvert.DeserializeObject<RootObject>(filestr);
            }
            catch { }
            return rootObject;
        }
    }
}
