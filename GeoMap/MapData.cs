using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace GeoMap
{
    public class GeoJsonPointCollection
    {
        public GeoJsonPointCollection()
        {
            Points = new List<System.Windows.Point>();
            Properties = new PropertiesModel();
        }
        public List<System.Windows.Point> Points { get; set; }

        public PropertiesModel Properties { get; set; }
    }
    public class GeoPathCollection
    {
        public Path PathGeometry { get; set; }
        public PropertiesModel Properties { get; set; }
    }

    public class PropertiesModel
    {
        public long AdCode { get; set; }
        public string Name { get; set; }
        public long ParentAdCode { get; set; }
        public System.Windows.Point Center { get; set; }
        public System.Windows.Point Centroid { get; set; }
    }

    public class RootObject
    {
        public string type { get; set; }
        public List<Feature> features { get; set; }
    }
}
