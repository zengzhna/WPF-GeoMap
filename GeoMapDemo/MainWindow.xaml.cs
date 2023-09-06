using GeoMap;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Prism.Commands;
using System.IO;
using System.Globalization;

namespace GeoMapDemo
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private Dictionary<long, string> languagePack;
        private ObservableCollection<CityInfo> cityInfo;
        private string source = "GeoJson/100000.json";
        private List<GeoJsonFileModel> GeoJsonFileList;
        private ObservableCollection<GeoMapDataModel> mapDataItemsSource;
        private ObservableCollection<ColorModel> colorItemsSource;
        private double geoMapScale = 1.0;

        public MainWindow()
        {
            InitializeComponent();

            LanguagePack = new Dictionary<long, string>();
            LanguagePack[440000] = "Guangdong"; // change the language if necessary
            LanguagePack[110000] = "Beijing";


            GeoJsonFileList = new List<GeoJsonFileModel>();
            var files = Directory.GetFiles("GeoJson");
            foreach (var file in files)
            {
                if (System.IO.Path.GetExtension(file) == ".json")
                {
                    GeoJsonFileModel model = new GeoJsonFileModel();
                    model.Name = System.IO.Path.GetFileNameWithoutExtension(file);
                    model.Url = file;
                    GeoJsonFileList.Add(model);
                }
            }
            ColorItemsSource = new ObservableCollection<ColorModel>
            {
                new ColorModel{Color = (Brush)new BrushConverter().ConvertFromString("#FF00F6FF"), Remarks = "10000 - ∞"},
                new ColorModel{Color = (Brush)new BrushConverter().ConvertFromString("#AA00F6FF"), Remarks = "5000 - 10000"},
                new ColorModel{Color = (Brush)new BrushConverter().ConvertFromString("#8800F6FF"), Remarks = "2000 - 5000"},
                new ColorModel{Color = (Brush)new BrushConverter().ConvertFromString("#6600F6FF"), Remarks = "1000 - 2000"},
                new ColorModel{Color = (Brush)new BrushConverter().ConvertFromString("#4400F6FF"), Remarks = "0 - 1000"},
            };

            DataContext = this;

            CityInfo = new ObservableCollection<CityInfo>
            {
                new CityInfo{AdCode = 440000, Level = "province", Address = "广东省"},
            };
            MapDataItemsSource = new ObservableCollection<GeoMapDataModel>();
            Random rnd = new Random();
            foreach (var item in GeoJsonFileList)
            {
                int r = rnd.Next(0, ColorItemsSource.Count - 1);
                var model = new GeoMapDataModel { AdCode = int.Parse(item.Name), ShapeFill = ColorItemsSource[r].Color };
                MapDataItemsSource.Add(model);
            }


            GeometryMouseDoubleClickCommand = new DelegateCommand<GeoPathCollection>((geopath) =>
             {
                 var curcity = GeoJsonFileList.FirstOrDefault(x => x.Name == geopath.Properties.AdCode.ToString());
                 if (curcity != null)
                 {
                     Source = curcity.Url;
                     curcity.Parent = geopath.Properties.ParentAdCode.ToString();
                 }
             });
            PreBtnCommand = new DelegateCommand(() =>
            {
                var curcity = GeoJsonFileList.FirstOrDefault(x => x.Url == Source);
                var precity = GeoJsonFileList.FirstOrDefault(x => x.Name == curcity?.Parent);
                if (precity != null)
                {
                    Source = precity.Url;
                }
            });
            AddAreaDataBtnCommand = new DelegateCommand(() =>
            {
                foreach (var city in GeoJsonFileList)
                {
                    if (!MapDataItemsSource.Any(x => x.AdCode.ToString() == city.Name))
                    {
                        int r = rnd.Next(0, ColorItemsSource.Count - 1);
                        MapDataItemsSource.Add(
                            new GeoMapDataModel { AdCode = int.Parse(city.Name), ShapeFill = ColorItemsSource[r].Color }
                            );
                        break;
                    }
                }
            });
            RemoveAreaDataBtnCommand = new DelegateCommand(() =>
            {
                MapDataItemsSource.Remove(MapDataItemsSource.FirstOrDefault());
            });
            UpdateScaleBtnCommand = new DelegateCommand(() =>
            {
                GeoMapScale = GetRandomNumber(1.0, 1.5);
            });
        }
        public double GetRandomNumber(double minimum, double maximum)
        {
            Random random = new Random();
            return random.NextDouble() * (maximum - minimum) + minimum;
        }

        public DelegateCommand<GeoPathCollection> GeometryMouseDoubleClickCommand { get; set; }
        public DelegateCommand PreBtnCommand { get; set; }
        public DelegateCommand AddAreaDataBtnCommand { get; set; }
        public DelegateCommand RemoveAreaDataBtnCommand { get; set; }
        public DelegateCommand UpdateScaleBtnCommand { get; set; }

        public Dictionary<long, string> LanguagePack { get => languagePack; set { languagePack = value; RaisePropertyChanged(nameof(LanguagePack)); } }
        public ObservableCollection<CityInfo> CityInfo { get => cityInfo; set { cityInfo = value; RaisePropertyChanged(nameof(CityInfo)); } }
        /// <summary>
        /// China map resources http://datav.aliyun.com/tools/atlas/index.html
        /// </summary>
        public string Source { get => source; set { source = value; RaisePropertyChanged(nameof(Source)); } }
        public ObservableCollection<GeoMapDataModel> MapDataItemsSource { get => mapDataItemsSource; set { mapDataItemsSource = value; RaisePropertyChanged(nameof(MapDataItemsSource)); } }
        public ObservableCollection<ColorModel> ColorItemsSource { get => colorItemsSource; set { colorItemsSource = value; RaisePropertyChanged(nameof(ColorItemsSource)); } }
        public double GeoMapScale { get => geoMapScale; set { geoMapScale = value; RaisePropertyChanged(nameof(GeoMapScale)); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class NullValueToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ColorModel
    {
        public Brush Color { get; set; }
        public string Remarks { get; set; }
    }
    public class GeoMapDataModel : IGeoJsonMapSource
    {
        public int AdCode { get ; set; }
        public Brush ShapeFill { get; set ; }
    }
    public class GeoJsonFileModel
    {
        public string Name { get; set; }
        public string Parent { get; set; }
        public string Url { get; set; }
    }
    public class CityInfo : IGeoJsonMapTooltip
    {
        public int AdCode { get; set; }
        public string Level { get; set; }
        public string Address { get; set; }

        protected void RaisePropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
