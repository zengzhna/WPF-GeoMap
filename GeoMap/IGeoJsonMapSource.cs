using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace GeoMap
{
    public interface IGeoJsonMapSource
    {
        /// <summary>
        /// geojson adcode
        /// </summary>
        int AdCode { get; set; }
        Brush ShapeFill { get; set; }
    }
}
