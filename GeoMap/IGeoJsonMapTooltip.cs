using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeoMap
{
    public interface IGeoJsonMapTooltip : INotifyPropertyChanged
    {
        int AdCode { get; set; }
    }
}
