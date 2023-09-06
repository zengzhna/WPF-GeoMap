using GeoJSON.Net.Feature;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace GeoMap
{
    public class GeoJsonMap : UserControl
    {
        private const int TileSize = 256;
        private const int EarthRadius = 6378137;
        private const double InitialResolution = 2 * Math.PI * EarthRadius / TileSize;
        private const double OriginShift = 2 * Math.PI * EarthRadius / 2;
        private const string OutlinedPathName = "OutlinedPath";
        private const string ExPandPathName = "ExPandPath";

        private readonly Canvas canvas;
        private readonly Canvas mapCanvas;
        private RootObject mapSourceData;
        private Path outlinedPath;
        private readonly List<GeoJsonPointCollection> geoJsonPointList;
        private readonly List<GeoJsonPointCollection> geoJsonExpandPointList;
        private readonly List<GeoPathCollection> geoPathList;
        private Point dragOrigin;
        private bool isDrag;
        private double mapWidth;
        private double mapHeight;
        private bool isMouseWheel;



        public GeoJsonMap()
        {
            canvas = new Canvas();
            mapCanvas = new Canvas();
            geoJsonPointList = new List<GeoJsonPointCollection>();
            geoJsonExpandPointList = new List<GeoJsonPointCollection>();
            geoPathList = new List<GeoPathCollection>();
            canvas.Children.Add(mapCanvas);
            Content = canvas;
        }
        public override void OnApplyTemplate()
        {
            if (EnableOutlined)
            {
                outlinedPath = CreatePath(false);
                outlinedPath.Name = OutlinedPathName;
                outlinedPath.SetBinding(Shape.StrokeProperty,
                    new Binding { Path = new PropertyPath(OutlinedStrokeProperty), Source = this });
                outlinedPath.SetBinding(Shape.StrokeThicknessProperty,
                    new Binding { Path = new PropertyPath(OutlinedStrokeThicknessProperty), Source = this });
                outlinedPath.Fill = Brushes.Transparent;
            }
            SetMapCanvasEffect();
            SizeChanged += (sender, e) =>
            {
                double w_scale = e.NewSize.Width / e.PreviousSize.Width;
                double h_scale = e.NewSize.Height / e.PreviousSize.Height;
                Draw(w_scale, h_scale, false, true, true);
            };
            Canvas.SetLeft(this.mapCanvas, 0);
            Canvas.SetTop(this.mapCanvas, 0);
            //this.MouseMove += (sender, e) =>
            //{
            //    MousePosition = e.GetPosition(canvas);
            //};

            this.MouseWheel += (sender, e) =>
            {
                if (!EnableZoomingAndPanning) return;
                isMouseWheel = true;
                double premapscale = GeoMapScale;
                e.Handled = true;
                var rt = this.mapCanvas.RenderTransform as ScaleTransform;
                GeoMapScale = rt == null ? 1 : rt.ScaleX;
                GeoMapScale += e.Delta > 0 ? .05 : -.05;
                GeoMapScale = GeoMapScale < 1 ? 1 : GeoMapScale;
                Point m_point = e.GetPosition(mapCanvas);
                GeomapScaleTransform((e.Delta > 0 ? 1.05 : 0.95), m_point);
                isMouseWheel = false;
            };
            this.MouseLeftButtonDown += (sender, e) =>
            {
                if (!EnableZoomingAndPanning) return;
                if (e.ClickCount > 1) return;
                dragOrigin = e.GetPosition(this);
                isDrag = true;
            };
            this.PreviewMouseRightButtonDown += (sender, e) =>
            {
                if (e.ClickCount > 1)
                {
                    e.Handled = true;
                    if (MouseRightDoubleClickCommand?.CanExecute(Source) == null ? false : MouseRightDoubleClickCommand.CanExecute(Source))
                    {
                        MouseRightDoubleClickCommand?.Execute(Source);
                    }
                }
            };
            FrameworkElement framework = FindTopVisualParent<FrameworkElement>(this);
            if (framework != null)
            {
                framework.MouseLeftButtonUp += Framework_MouseLeftButtonUp;
            }
        }

        /// <summary>
        /// Release the left mouse button on the parent window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Framework_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDrag) return;
            if (!EnableZoomingAndPanning) return;

            Point end = e.GetPosition(this);
            Point delta = new Point(dragOrigin.X - end.X, dragOrigin.Y - end.Y);

            double left = Canvas.GetLeft(mapCanvas) - delta.X;
            double top = Canvas.GetTop(mapCanvas) - delta.Y;

            if (left < 0)
            {
                left = GeoMapScale == 1 ? 0 : left;
            }
            else if (left > this.ActualWidth - mapWidth)
            {
                left = this.ActualWidth - (GeoMapMargin.Left * 2) - mapWidth;
            }
            if (top < 0)
            {
                top = GeoMapScale == 1 ? 0 : top;
            }
            else if (top > this.ActualHeight - mapHeight)
            {
                top = this.ActualHeight - (GeoMapMargin.Top * 2) - mapHeight;
            }

            if (DisableAnimations)
            {
                Canvas.SetLeft(mapCanvas, left);
                Canvas.SetTop(mapCanvas, top);
            }
            else
            {
                mapCanvas.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(left, AnimationsSpeed));
                mapCanvas.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(top, AnimationsSpeed));
            }
            isDrag = false;
        }

        /// <summary>
        /// Update geomap zoom ratio 
        /// </summary>
        /// <param name="zoomratio">zoom ratio</param>
        /// <param name="oripoint">The origin of the current zoom </param>
        private void GeomapScaleTransform(double zoomratio, Point oripoint, bool isanimation = false)
        {
            this.mapCanvas.RenderTransformOrigin = new Point(0, 0);
            if (isanimation)
            {
                //this.mapcanvas.RenderTransform = new ScaleTransform(1.0, 1.0);
                this.mapCanvas.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(GeoMapScale, new Duration(AnimationsSpeed)));
                this.mapCanvas.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(GeoMapScale, new Duration(AnimationsSpeed)));
            }
            else
            {
                this.mapCanvas.RenderTransform = new ScaleTransform(GeoMapScale, GeoMapScale);
            }
            //set map offset
            if (GeoMapScale > 1.0)
            {
                double mx_offset = (oripoint.X * zoomratio) - oripoint.X;
                double my_offset = (oripoint.Y * zoomratio) - oripoint.Y;
                double left = Canvas.GetLeft(this.mapCanvas) - mx_offset;
                double top = Canvas.GetTop(this.mapCanvas) - my_offset;
                if (DisableAnimations)
                {
                    Canvas.SetLeft(this.mapCanvas, left);
                    Canvas.SetTop(this.mapCanvas, top);
                }
                else
                {
                    if (isanimation)
                    {
                        this.mapCanvas.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(left, new Duration(AnimationsSpeed)));
                        this.mapCanvas.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(top, new Duration(AnimationsSpeed)));
                    }
                    else
                    {
                        this.mapCanvas.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(left, new Duration(new TimeSpan(0))));
                        this.mapCanvas.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(top, new Duration(new TimeSpan(0))));
                    }
                }
            }
        }
        private void Draw(bool isresetsource, bool isresetposition = false)
        {
            Draw(1, 1, isresetsource, isresetposition, true);
        }
        /// <summary>
        /// Render map data
        /// </summary>
        /// <param name="isresetposition">Whether to reset the map location</param>
        private async void Draw(double w_changescale, double h_changescale, bool isresetsource, bool isresetposition, bool isanimation)
        {
            if (this.ActualHeight < 1 || this.ActualWidth < 1)
            {
                return;
            }
            this.mapCanvas.Children.Clear();
            this.mapCanvas.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, new Duration(new TimeSpan(0, 0, 0, 0, 500))));
            geoJsonPointList.Clear();
            geoJsonExpandPointList.Clear();
            geoPathList.Clear();
            mapWidth = 0;
            mapHeight = 0;
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                this.mapCanvas.Children.Add(new TextBlock
                {
                    Text = "Designer preview is not currently available",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Effect = new DropShadowEffect
                    {
                        ShadowDepth = 2,
                        RenderingBias = RenderingBias.Performance
                    }
                });
                return;
            }

            if (isresetsource || mapSourceData == null)
            {
                mapSourceData = await MapResolver.GetJsonMapAsync(Source);
            }
            if (mapSourceData == null) return;

            List<List<Point>> points = new List<List<Point>>();
            int adcode = 0;
            long parent_adcode = 0;
            foreach (var feature in mapSourceData.features)
            {
                PropertiesModel properties = new PropertiesModel();
                properties.AdCode = !feature.Properties.Keys.Any(x => x == "adcode") ? adcode++ : long.Parse(feature.Properties["adcode"].ToString());
                properties.Name = !feature.Properties.Keys.Any(x => x == "name") ? null : feature.Properties["name"]?.ToString();
                properties.ParentAdCode = !feature.Properties.Keys.Any(x => x == "parent") ? 0 : long.Parse(JObject.Parse(feature.Properties["parent"].ToString())["adcode"].ToString());
                parent_adcode = properties.ParentAdCode;
                double[] centerarr = !feature.Properties.Keys.Any(x => x == "center") ? null : JsonConvert.DeserializeObject<double[]>(feature.Properties["center"]?.ToString());
                //double[] centroidarr =  feature.Properties["centroid"] == null ? null : JsonConvert.DeserializeObject<double[]>( feature.Properties["centroid"]?.ToString());
                if (centerarr != null && centerarr.Count() == 2)
                {
                    properties.Center = new Point(centerarr[0], centerarr[1]);
                }
                //if (centroidarr != null && centroidarr.Count() == 2)
                //{
                //    geoJsonPoint.Properties.Centroid = new Point(centroidarr[0], centroidarr[1]);
                //}
                CreatePoints(feature.Geometry, properties);
            }
            DrawMapPath(out double p_width, out double p_height);

            DrawExpandMapPath(geoJsonExpandPointList, p_width, p_height, parent_adcode);

            if (isresetposition)
            {
                double left;
                double top;
                if (GeoMapScale == 1.0)
                {
                    left = (this.ActualWidth - (GeoMapMargin.Left * 2) - mapWidth) / 2;
                    top = (this.ActualHeight - (GeoMapMargin.Top * 2) - mapHeight) / 2;
                }
                else
                {
                    left = Canvas.GetLeft(this.mapCanvas) * w_changescale;
                    top = Canvas.GetTop(this.mapCanvas) * h_changescale;
                }
                if (!isanimation)
                {
                    Canvas.SetLeft(this.mapCanvas, left);
                    Canvas.SetTop(this.mapCanvas, top);
                }
                else
                {
                    this.mapCanvas.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(top, AnimationsSpeed));
                    this.mapCanvas.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(left, AnimationsSpeed));
                }
            }
            DrawShapeData();
        }

        private void DrawMapPath(out double p_width, out double p_height)
        {
            p_width = 0;
            p_height = 0;
            if (geoJsonPointList.Count == 0)
            {
                return;
            }
            double min_x = geoJsonPointList.Min(x => x.Points.Min(y => y.X));
            double min_y = geoJsonPointList.Min(x => x.Points.Min(y => y.Y));
            double max_x = geoJsonPointList.Max(x => x.Points.Max(y => y.X));
            double max_y = geoJsonPointList.Max(x => x.Points.Max(y => y.Y));
            p_width = max_x - min_x;
            p_height = max_y - min_y;
            Point min_pixelpoint = FromCoordinatesToPixel(min_y, min_x);
            Point max_pixelpoint = FromCoordinatesToPixel(max_y, max_x);
            double w_scale = (max_pixelpoint.X - min_pixelpoint.X) / (max_pixelpoint.Y - min_pixelpoint.Y);
            double h_scale = (max_pixelpoint.Y - min_pixelpoint.Y) / (max_pixelpoint.X - min_pixelpoint.X);

            if (this.ActualHeight * w_scale > this.ActualWidth)
            {
                mapWidth = this.ActualWidth - (GeoMapMargin.Left * 2);
                mapHeight = this.ActualWidth * h_scale - (GeoMapMargin.Top * 2);
            }
            else
            {
                mapWidth = (this.ActualHeight * w_scale) - (GeoMapMargin.Left * 2);
                mapHeight = this.ActualHeight - (GeoMapMargin.Top * 2);
            }
            if (mapHeight < 1 || mapWidth < 1)
            {
                return;
            }
            this.mapCanvas.Width = mapWidth;
            this.mapCanvas.Height = mapHeight;

            double h_pixelscale = mapHeight / p_height;
            double w_pixelscale = mapWidth / p_width;

            IEnumerable<PropertiesModel> propertieslist = geoJsonPointList.GroupBy(x => x.Properties).Select(x => x.Key);
            if (EnableOutlined && outlinedPath != null)
            {
                this.mapCanvas.Children.Add(outlinedPath);
            }
            GeometryGroup geometrygroup = new GeometryGroup();

            foreach (var prop in propertieslist)
            {
                Path path = CreatePath(ShapeHoverable);
                this.mapCanvas.Children.Add(path);
                Panel.SetZIndex(path, 0);
                StreamGeometry stgeometry = new StreamGeometry();
                stgeometry.FillRule = FillRule.EvenOdd;
                path.Data = stgeometry;
                if (EnableOutlined && outlinedPath != null)
                {
                    geometrygroup.Children.Add(stgeometry);
                }
                using (StreamGeometryContext ctx = stgeometry.Open())
                {
                    foreach (var item in geoJsonPointList)
                    {
                        if (prop.AdCode == item.Properties.AdCode)
                        {
                            int index = 0;
                            IList<Point> points = new List<Point>();
                            foreach (var point in item.Points)
                            {
                                Point curpoint = new Point(((point.X - (double)min_x) * w_pixelscale) + GeoMapMargin.Left,
                                        ((max_y - point.Y) * h_pixelscale) + GeoMapMargin.Top);
                                points.Add(curpoint);
                                if (index == 0)
                                {
                                    ctx.BeginFigure(curpoint, true, true);
                                }
                                index++;
                            }
                            ctx.PolyLineTo(points, true, true);
                        }
                    }
                }
                GeoPathCollection pathCollection = new GeoPathCollection();
                geoPathList.Add(pathCollection);
                pathCollection.PathGeometry = path;
                pathCollection.Properties = prop;
            }

            if (EnableOutlined && outlinedPath != null)
            {
                outlinedPath.Data = geometrygroup.GetOutlinedPathGeometry();
            }
        }

        private void DrawExpandMapPath(List<GeoJsonPointCollection> expandlist, double p_width, double p_height, long parentadcode)
        {
            if (mapHeight < 1 || mapWidth < 1)
            {
                return;
            }
            if (expandlist.Count == 0)
            {
                return;
            }

            double min_x = expandlist.Min(x => x.Points.Min(y => y.X));
            double min_y = expandlist.Min(x => x.Points.Min(y => y.Y));
            double max_x = expandlist.Max(x => x.Points.Max(y => y.X));
            double max_y = expandlist.Max(x => x.Points.Max(y => y.Y));
            double s_width = max_x - min_x;
            double s_height = max_y - min_y;

            Point min_pixelpoint = FromCoordinatesToPixel(min_y, min_x);
            Point max_pixelpoint = FromCoordinatesToPixel(max_y, max_x);

            double w_scale = (max_pixelpoint.X - min_pixelpoint.X) / (max_pixelpoint.Y - min_pixelpoint.Y);
            double h_scale = (max_pixelpoint.Y - min_pixelpoint.Y) / (max_pixelpoint.X - min_pixelpoint.X);
            double zoom = 0.03;
            if (parentadcode == 460000)
            {
                zoom = 0.002;
            }
            double exmapHeight = mapHeight * (s_height / p_height) * (zoom * h_scale * (mapHeight / mapWidth));
            double exmapWidth = mapWidth * (s_width / p_width) * (zoom * w_scale * (mapWidth / mapHeight));

            double h_pixelscale = exmapHeight / h_scale;
            double w_pixelscale = exmapWidth / w_scale;

            Canvas exmapCanvas = new Canvas();

            Path mappath = CreatePath(ShapeHoverable);
            exmapCanvas.Children.Add(mappath);

            mappath.Fill = ShapeFill;
            mappath.StrokeThickness = ShapeStrokeThickness * 1.2;
            GeometryGroup geogroup = new GeometryGroup();
            geogroup.FillRule = FillRule.Nonzero;
            mappath.Data = geogroup;

            IEnumerable<PropertiesModel> expropertieslist = expandlist.GroupBy(x => x.Properties).Select(x => x.Key);
            StreamGeometry stgeometry = new StreamGeometry();
            stgeometry.FillRule = FillRule.EvenOdd;
            foreach (var exprop in expropertieslist)
            {
                //expand path
                using (StreamGeometryContext ctx = stgeometry.Open())
                {
                    foreach (var item in expandlist)
                    {
                        if (exprop.AdCode == item.Properties.AdCode)
                        {
                            int index = 0;
                            IList<Point> points = new List<Point>();
                            foreach (var point in item.Points)
                            {
                                Point curpoint = new Point((point.X - (double)min_x) * w_pixelscale,
                                    (max_y - point.Y) * h_pixelscale);
                                points.Add(curpoint);
                                if (index == 0)
                                {
                                    ctx.BeginFigure(curpoint, true, true);
                                }
                                index++;
                            }
                            ctx.PolyLineTo(points, true, true);
                        }
                    }
                }
                geogroup.Children.Add(stgeometry);
                GeoPathCollection pathCollection = new GeoPathCollection();
                pathCollection.PathGeometry = mappath;
                pathCollection.Properties = exprop;
                geoPathList.Add(pathCollection);
            }

            double w_margin = w_pixelscale * 1.2;
            double h_margin = h_pixelscale * 1.2;
            Rect rect = new Rect(-w_margin, -h_margin, (s_width * w_pixelscale) + (w_margin * 2), (s_height * h_pixelscale) + (h_margin * 2));

            Path rectpath = CreatePath(ShapeHoverable);
            rectpath.Effect = null;
            if (EnableOutlined)
            {
                rectpath.Name = ExPandPathName;
                rectpath.SetBinding(Shape.StrokeProperty,
                    new Binding { Path = new PropertyPath(OutlinedStrokeProperty), Source = this });
                rectpath.SetBinding(Shape.StrokeThicknessProperty,
                    new Binding { Path = new PropertyPath(OutlinedStrokeThicknessProperty), Source = this });
                rectpath.Fill = Brushes.Transparent;
            }
            else
            {
                rectpath.Fill = Brushes.Transparent;
                rectpath.StrokeThickness = ShapeStrokeThickness * 1.3;
            }

            RectangleGeometry rectgeometry = new RectangleGeometry();
            rectgeometry.Rect = rect;
            rectpath.Data = rectgeometry;
            GeoPathCollection rectpathCollection = new GeoPathCollection();
            rectpathCollection.PathGeometry = rectpath;
            rectpathCollection.Properties = expropertieslist.FirstOrDefault();
            geoPathList.Add(rectpathCollection);

            Canvas.SetLeft(exmapCanvas, mapWidth - rect.Width + w_margin + GeoMapMargin.Left);
            Canvas.SetTop(exmapCanvas, mapHeight - rect.Height + h_margin + GeoMapMargin.Top);

            exmapCanvas.Children.Add(rectpath);

            this.mapCanvas.Children.Add(exmapCanvas);
        }
        private Path CreatePath(bool enableevent = true)
        {
            Path path = new Path();
            path.SetBinding(Shape.StrokeProperty,
                new Binding { Path = new PropertyPath(ShapeStrokeProperty), Source = this });
            path.SetBinding(Shape.StrokeThicknessProperty,
                new Binding { Path = new PropertyPath(ShapeStrokeThicknessProperty), Source = this });
            path.SetBinding(Shape.FillProperty,
                new Binding { Path = new PropertyPath(ShapeFillProperty), Source = this });
            path.RenderTransformOrigin = new Point(0.5, 0.5);
            path.StrokeEndLineCap = PenLineCap.Round;
            path.StrokeStartLineCap = PenLineCap.Round;
            path.StrokeLineJoin = PenLineJoin.Round;
            if (enableevent)
            {
                path.MouseEnter += Path_MouseEnter;
                path.MouseLeave += Path_MouseLeave;
                path.MouseMove += Path_MouseMove;
                path.PreviewMouseLeftButtonDown += Path_MouseLeftButtonDown;
            }
            return path;
        }
        /// <summary>
        /// set map canvas dropshadow
        /// </summary>
        private void SetMapCanvasEffect()
        {
            if (this.mapCanvas != null && EnableDropShadow)
            {
                Color color = ((SolidColorBrush)ShapeStroke).Color;
                if (EnableOutlined)
                {
                    color = ((SolidColorBrush)OutlinedStroke).Color;
                }
                DropShadowEffect effect = new DropShadowEffect();
                effect.BlurRadius = 10;
                effect.Opacity = .5;
                effect.ShadowDepth = 10;
                effect.Direction = 270;
                effect.RenderingBias = RenderingBias.Performance;
                effect.Color = color;
                this.mapCanvas.Effect = effect;
            }
        }
        /// <summary>
        /// Converts given lat/lon in WGS84 Datum to XY in Spherical Mercator EPSG:900913
        /// </summary>
        /// <param name="lat"></param>
        /// <param name="lon"></param>
        /// <returns></returns>
        public Point FromCoordinatesToPixel(double lat, double lon)
        {
            var p = new Point();
            p.X = lon * OriginShift / 180;
            p.Y = Math.Log(Math.Tan((90 + lat) * Math.PI / 360)) / (Math.PI / 180);
            p.Y = p.Y * OriginShift / 180;
            return p;
        }
        private void Path_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Path path && e.ClickCount > 1)
            {
                e.Handled = true;
                GeoPathCollection curgeopath = geoPathList.FirstOrDefault(x => x.PathGeometry == path);
                if (curgeopath != null && GeometryMouseDoubleClickCommand?.CanExecute(curgeopath) == null ? false : GeometryMouseDoubleClickCommand.CanExecute(curgeopath))
                {
                    GeometryMouseDoubleClickCommand?.Execute(curgeopath);
                }
            }
        }
        private void Path_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!ShapeHoverable) return;

            Point location = e.GetPosition(this);
            if (location.Y + 5 + GeoMapTooltip.ActualHeight > this.ActualHeight)
            {
                Canvas.SetTop(GeoMapTooltip, location.Y - 5 - GeoMapTooltip.ActualHeight);
            }
            else
            {
                Canvas.SetTop(GeoMapTooltip, location.Y + 5);
            }
            if (location.X + 5 + GeoMapTooltip.ActualWidth > this.ActualWidth)
            {
                Canvas.SetLeft(GeoMapTooltip, location.X - 5 - GeoMapTooltip.ActualWidth);
            }
            else
            {
                Canvas.SetLeft(GeoMapTooltip, location.X + 5);
            }
        }

        private void Path_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!ShapeHoverable) return;

            if (sender is Path path)
            {
                Panel.SetZIndex(path, 0);
                path.Opacity = 1;
                if (EnableOutlined && (path.Name == ExPandPathName || path.Name == OutlinedPathName))
                {
                    path.StrokeThickness = OutlinedStrokeThickness;
                }
                else
                {
                    path.StrokeThickness = ShapeStrokeThickness;
                }
                GeoMapTooltip.Visibility = Visibility.Collapsed;
            }
        }

        private void Path_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!ShapeHoverable) return;

            if (sender is Path path)
            {
                Panel.SetZIndex(path, 1);
                path.Opacity = 0.7;
                if (EnableOutlined && (path.Name == ExPandPathName || path.Name == OutlinedPathName))
                {
                    path.StrokeThickness = OutlinedStrokeThickness + 1.5;
                }
                else
                {
                    path.StrokeThickness = ShapeStrokeThickness + 1.5;
                }

                GeoMapTooltip.Visibility = Visibility.Visible;
                GeoJsonMapTooltipModel model = new GeoJsonMapTooltipModel();
                var curgeopath = geoPathList.FirstOrDefault(x => x.PathGeometry == path);
                if (curgeopath != null)
                {
                    if (!string.IsNullOrEmpty(curgeopath.Properties.Name) && LanguagePack != null && LanguagePack.Count > 0)
                    {
                        if (LanguagePack.Any(x => x.Key == curgeopath.Properties.AdCode))
                        {
                            curgeopath.Properties.Name = LanguagePack[curgeopath.Properties.AdCode];
                        }
                    }
                    model.Properties = curgeopath.Properties;
                    if (TooltipValues != null && TooltipValues.Count() > 0)
                    {
                        var curtooltip = TooltipValues.FirstOrDefault(x => ((IGeoJsonMapTooltip)x).AdCode == curgeopath.Properties.AdCode);
                        if (curtooltip != null)
                        {
                            model.Content = curtooltip;
                        }
                    }
                }
                GeoMapTooltip.DataContext = model;
            }
        }

        private void CreatePoints(GeoJSON.Net.Geometry.IGeometryObject geometryObject, PropertiesModel properties, bool isextract = false)
        {
            switch (geometryObject.Type)
            {
                case GeoJSON.Net.GeoJSONObjectType.MultiPoint:
                case GeoJSON.Net.GeoJSONObjectType.LineString:
                case GeoJSON.Net.GeoJSONObjectType.Point:
                    GeoJsonPointCollection geoJsonPoint;
                    GeoJsonPointCollection gepJsonExpandPoint;
                    if (geometryObject.Type == GeoJSON.Net.GeoJSONObjectType.MultiPoint)
                    {
                        foreach (var item in ((GeoJSON.Net.Geometry.MultiPoint)geometryObject).Coordinates)
                        {
                            geoJsonPoint = new GeoJsonPointCollection();
                            geoJsonPoint.Properties = properties;
                            Point point = new Point(item.Coordinates.Longitude, item.Coordinates.Latitude);
                            geoJsonPoint.Points.Add(point);
                            geoJsonPointList.Add(geoJsonPoint);
                        }
                    }
                    else if (geometryObject.Type == GeoJSON.Net.GeoJSONObjectType.LineString)
                    {
                        if (!isextract)
                        {
                            geoJsonPoint = new GeoJsonPointCollection();
                            geoJsonPoint.Properties = properties;
                            foreach (var item in ((GeoJSON.Net.Geometry.LineString)geometryObject).Coordinates)
                            {
                                Point point = new Point(item.Longitude, item.Latitude);
                                geoJsonPoint.Points.Add(point);
                            }
                            geoJsonPointList.Add(geoJsonPoint);
                        }
                        else
                        {
                            gepJsonExpandPoint = new GeoJsonPointCollection();
                            gepJsonExpandPoint.Properties = properties;
                            foreach (var item in ((GeoJSON.Net.Geometry.LineString)geometryObject).Coordinates)
                            {
                                Point point = new Point(item.Longitude, item.Latitude);
                                gepJsonExpandPoint.Points.Add(point);
                            }
                            geoJsonExpandPointList.Add(gepJsonExpandPoint);
                        }

                    }
                    else if (geometryObject.Type == GeoJSON.Net.GeoJSONObjectType.Point)
                    {
                        if (geometryObject is GeoJSON.Net.Geometry.Point geopoint)
                        {
                            geoJsonPoint = new GeoJsonPointCollection();
                            geoJsonPoint.Properties = properties;
                            Point point = new Point(geopoint.Coordinates.Longitude, geopoint.Coordinates.Latitude);
                            geoJsonPoint.Points.Add(point);
                            geoJsonPointList.Add(geoJsonPoint);
                        }
                    }

                    break;
                case GeoJSON.Net.GeoJSONObjectType.MultiLineString:
                    foreach (var item in ((GeoJSON.Net.Geometry.MultiLineString)geometryObject).Coordinates)
                    {
                        CreatePoints(item, properties, isextract);
                    }
                    break;
                case GeoJSON.Net.GeoJSONObjectType.Polygon:
                    foreach (var item in ((GeoJSON.Net.Geometry.Polygon)geometryObject).Coordinates)
                    {
                        CreatePoints(item, properties, isextract);
                    }
                    break;
                case GeoJSON.Net.GeoJSONObjectType.MultiPolygon:

                    bool _isextract = false;
                    foreach (var item in ((GeoJSON.Net.Geometry.MultiPolygon)geometryObject).Coordinates)
                    {
                        if (properties.AdCode == 460300)
                        {
                            _isextract = true;
                            continue;
                        }
                        CreatePoints(item, properties, isextract);
                        if (properties.AdCode == 460000)
                        {
                            //Extraction processing in South China Sea
                            _isextract = true;
                            break;
                        }
                    }
                    if (_isextract)
                    {
                        foreach (var item in ((GeoJSON.Net.Geometry.MultiPolygon)geometryObject).Coordinates)
                        {
                            CreatePoints(item, properties, _isextract);
                        }
                    }

                    break;
                case GeoJSON.Net.GeoJSONObjectType.GeometryCollection:
                    foreach (var item in ((GeoJSON.Net.Geometry.GeometryCollection)geometryObject).Geometries)
                    {
                        CreatePoints(item, properties, isextract);
                    }
                    break;
                case GeoJSON.Net.GeoJSONObjectType.Feature:
                    break;
                case GeoJSON.Net.GeoJSONObjectType.FeatureCollection:
                    break;
                default:
                    break;
            }
        }

        private static T FindTopVisualParent<T>(DependencyObject source) where T : DependencyObject
        {
            T previousSource = null;
            while (source != null)
            {
                source = VisualTreeHelper.GetParent(source);
                if (source != null)
                    previousSource = source as T;
                else
                    break;
            }
            return previousSource as T;
        }

        #region DependencyProperty


        /// <summary>
        /// Identifies the <see cref="Source"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(string), typeof(GeoJsonMap), new PropertyMetadata(SourcePropertyChanged));

        /// <summary>
        /// Gets or sets the map json file source
        /// </summary>
        public string Source
        {
            get { return (string)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="GeoMapTooltip"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty GeoMapTooltipProperty =
            DependencyProperty.Register(nameof(GeoMapTooltip), typeof(FrameworkElement), typeof(GeoJsonMap), new PropertyMetadata(GeoMapTooltipPropertyChanged));
        /// <summary>
        /// Gets or sets the map tooltip
        /// </summary>
        public FrameworkElement GeoMapTooltip
        {
            get { return (FrameworkElement)GetValue(GeoMapTooltipProperty); }
            set { SetValue(GeoMapTooltipProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="DisableAnimations"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty DisableAnimationsProperty = DependencyProperty.Register(
            nameof(DisableAnimations), typeof(bool), typeof(GeoJsonMap), new PropertyMetadata(false));
        /// <summary>
        /// Gets or sets whether the chart is animated
        /// </summary>
        public bool DisableAnimations
        {
            get { return (bool)GetValue(DisableAnimationsProperty); }
            set { SetValue(DisableAnimationsProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="AnimationsSpeed"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty AnimationsSpeedProperty = DependencyProperty.Register(
            nameof(AnimationsSpeed), typeof(TimeSpan), typeof(GeoJsonMap), new PropertyMetadata(new TimeSpan(0, 0, 0, 0, 200)));
        /// <summary>
        /// Gets or sets animations speed
        /// </summary>
        public TimeSpan AnimationsSpeed
        {
            get { return (TimeSpan)GetValue(AnimationsSpeedProperty); }
            set { SetValue(AnimationsSpeedProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="EnableZoomingAndPanning"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty EnableZoomingAndPanningProperty = DependencyProperty.Register(
            nameof(EnableZoomingAndPanning), typeof(bool), typeof(GeoJsonMap), new PropertyMetadata(default(bool)));
        /// <summary>
        /// Gets or sets whether the map allows zooming and panning
        /// </summary>
        public bool EnableZoomingAndPanning
        {
            get { return (bool)GetValue(EnableZoomingAndPanningProperty); }
            set { SetValue(EnableZoomingAndPanningProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="TooltipValues"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty TooltipValuesProperty = DependencyProperty.Register(
            nameof(TooltipValues), typeof(IEnumerable<object>), typeof(GeoJsonMap), new PropertyMetadata());
        /// <summary>
        /// Gets or sets map tips, Object must inherit <see cref="IGeoJsonMapTooltip"/> 
        /// </summary>
        public IEnumerable<object> TooltipValues
        {
            get { return (IEnumerable<object>)GetValue(TooltipValuesProperty); }
            set { SetValue(TooltipValuesProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="ShapeHoverable"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty ShapeHoverableProperty = DependencyProperty.Register(
            nameof(ShapeHoverable), typeof(bool), typeof(GeoJsonMap), new PropertyMetadata(true));
        /// <summary>
        /// Gets or sets whether the Path hover
        /// </summary>
        public bool ShapeHoverable
        {
            get { return (bool)GetValue(ShapeHoverableProperty); }
            set { SetValue(ShapeHoverableProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="ShapeStrokeThickness"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty ShapeStrokeThicknessProperty = DependencyProperty.Register(
            nameof(ShapeStrokeThickness), typeof(double), typeof(GeoJsonMap), new PropertyMetadata(.2));
        /// <summary>
        /// Gets or sets the width of the Path outline.
        /// </summary>
        public double ShapeStrokeThickness
        {
            get { return (double)GetValue(ShapeStrokeThicknessProperty); }
            set { SetValue(ShapeStrokeThicknessProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="ShapeStroke"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty ShapeStrokeProperty = DependencyProperty.Register(
            nameof(ShapeStroke), typeof(Brush), typeof(GeoJsonMap), new PropertyMetadata(Brushes.Black));
        /// <summary>
        /// Gets or sets the Brush that specifies how the Path outline is painted.
        /// </summary>
        public Brush ShapeStroke
        {
            get { return (Brush)GetValue(ShapeStrokeProperty); }
            set { SetValue(ShapeStrokeProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="ShapeFill"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty ShapeFillProperty = DependencyProperty.Register(
            nameof(ShapeFill), typeof(Brush), typeof(GeoJsonMap), new PropertyMetadata(Brushes.Gray));
        /// <summary>
        /// Gets or sets the Brush that specifies how the path's interior is painted.
        /// </summary>
        public Brush ShapeFill
        {
            get { return (Brush)GetValue(ShapeFillProperty); }
            set { SetValue(ShapeFillProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="GeometryMouseDoubleClickCommand"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty GeometryMouseDoubleClickCommandProperty = DependencyProperty.Register(
            nameof(GeometryMouseDoubleClickCommand), typeof(ICommand), typeof(GeoJsonMap), new PropertyMetadata());
        /// <summary>
        /// Gets or sets mouse double click event of path
        /// </summary>
        public ICommand GeometryMouseDoubleClickCommand
        {
            get { return (ICommand)GetValue(GeometryMouseDoubleClickCommandProperty); }
            set { SetValue(GeometryMouseDoubleClickCommandProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="MouseRightDoubleClickCommand"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty MouseRightDoubleClickCommandProperty = DependencyProperty.Register(
            nameof(MouseRightDoubleClickCommand), typeof(ICommand), typeof(GeoJsonMap), new PropertyMetadata());
        /// <summary>
        /// Gets or sets mouse right double click event of map canvas
        /// </summary>
        public ICommand MouseRightDoubleClickCommand
        {
            get { return (ICommand)GetValue(MouseRightDoubleClickCommandProperty); }
            set { SetValue(MouseRightDoubleClickCommandProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="MapDataItemsSource"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty MapDataItemsSourceProperty = DependencyProperty.Register(
            nameof(MapDataItemsSource), typeof(IEnumerable<object>), typeof(GeoJsonMap), new PropertyMetadata(MapDataItemsSourcePropertyChanged));
        /// <summary>
        /// Gets or sets map data source, Object must inherit <see cref="IGeoJsonMapSource"/> 
        /// </summary>
        public IEnumerable<object> MapDataItemsSource
        {
            get { return (IEnumerable<object>)GetValue(MapDataItemsSourceProperty); }
            set { SetValue(MapDataItemsSourceProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="MousePosition"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty MousePositionProperty = DependencyProperty.Register(
            nameof(MousePosition), typeof(Point), typeof(GeoJsonMap), new PropertyMetadata());
        /// <summary>
        /// Gets or sets position of the mouse relative to the canvas 
        /// </summary>
        public Point MousePosition
        {
            get { return (Point)GetValue(MousePositionProperty); }
            set { SetValue(MousePositionProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="LanguagePack"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty LanguagePackProperty = DependencyProperty.Register(
            nameof(LanguagePack), typeof(Dictionary<long, string>), typeof(GeoJsonMap), new PropertyMetadata());
        /// <summary>
        /// Gets or sets replace the map name displayed on the tooltip 
        /// </summary>
        public Dictionary<long, string> LanguagePack
        {
            get { return (Dictionary<long, string>)GetValue(LanguagePackProperty); }
            set { SetValue(LanguagePackProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="GeoMapMargin"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty GeoMapMarginProperty = DependencyProperty.Register(
            nameof(GeoMapMargin), typeof(Thickness), typeof(GeoJsonMap), new PropertyMetadata(new Thickness(20)));
        /// <summary>
        /// Gets or sets the distance from the map to the boundary 
        /// </summary>
        public Thickness GeoMapMargin
        {
            get { return (Thickness)GetValue(GeoMapMarginProperty); }
            set { SetValue(GeoMapMarginProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="GeoMapScale"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty GeoMapScaleProperty = DependencyProperty.Register(
            nameof(GeoMapScale), typeof(double), typeof(GeoJsonMap), new PropertyMetadata(1.0, GeoMapScalePropertyChanged));

        /// <summary>
        /// Gets or sets the distance from the map to the boundary 
        /// </summary>
        public double GeoMapScale
        {
            get { return (double)GetValue(GeoMapScaleProperty); }
            set { SetValue(GeoMapScaleProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="EnableDropShadow"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty EnableDropShadowProperty = DependencyProperty.Register(
            nameof(EnableDropShadow), typeof(bool), typeof(GeoJsonMap), new PropertyMetadata(false));
        /// <summary>
        /// Gets or sets whether to enable the Map DropShadow 
        /// </summary>
        public bool EnableDropShadow
        {
            get { return (bool)GetValue(EnableDropShadowProperty); }
            set { SetValue(EnableDropShadowProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="EnableOutlined"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty EnableOutlinedProperty = DependencyProperty.Register(
            nameof(EnableOutlined), typeof(bool), typeof(GeoJsonMap), new PropertyMetadata(false));
        /// <summary>
        /// Gets or set whether to enable the Map outlined
        /// </summary>
        public bool EnableOutlined
        {
            get { return (bool)GetValue(EnableOutlinedProperty); }
            set { SetValue(EnableOutlinedProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="OutlinedStrokeThickness"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty OutlinedStrokeThicknessProperty = DependencyProperty.Register(
            nameof(OutlinedStrokeThickness), typeof(double), typeof(GeoJsonMap), new PropertyMetadata(.8));
        /// <summary>
        /// Gets or sets the width of the Map outline.
        /// </summary>
        public double OutlinedStrokeThickness
        {
            get { return (double)GetValue(OutlinedStrokeThicknessProperty); }
            set { SetValue(OutlinedStrokeThicknessProperty, value); }
        }
        /// <summary>
        /// Identifies the <see cref="ShapeStroke"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty OutlinedStrokeProperty = DependencyProperty.Register(
            nameof(OutlinedStroke), typeof(Brush), typeof(GeoJsonMap), new PropertyMetadata(Brushes.Red));
        /// <summary>
        /// Gets or sets the Brush that specifies how the Map outline is painted.
        /// </summary>
        public Brush OutlinedStroke
        {
            get { return (Brush)GetValue(OutlinedStrokeProperty); }
            set { SetValue(OutlinedStrokeProperty, value); }
        }


        private static void GeoMapScalePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GeoJsonMap geojsonmap && !geojsonmap.isMouseWheel)
            {
                double zoomratio = ((double)e.NewValue > (double)e.OldValue) ? 1.0 + ((double)e.NewValue - (double)e.OldValue)
                    : 1.0 + ((double)e.NewValue - (double)e.OldValue);
                Point oripoint = new Point(geojsonmap.mapWidth / 2, geojsonmap.mapHeight / 2);
                geojsonmap.GeomapScaleTransform(zoomratio, oripoint, true);
            }
        }
        private static void SourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GeoJsonMap geojsonmap)
            {
                //Canvas.SetLeft(geojsommap.mapcanvas, 0);
                //Canvas.SetTop(geojsommap.mapcanvas, 0);
                geojsonmap.GeoMapScale = 1.0;
                geojsonmap.mapCanvas.RenderTransform = new ScaleTransform(geojsonmap.GeoMapScale, geojsonmap.GeoMapScale);
                geojsonmap.Draw(true, true);
            }
        }
        private static void GeoMapTooltipPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GeoJsonMap geojsonmap)
            {
                if (e.OldValue is FrameworkElement oldtooltip)
                {
                    geojsonmap.canvas.Children.Remove(oldtooltip);
                }
                if (e.NewValue is FrameworkElement newtootip)
                {
                    newtootip.Visibility = Visibility.Collapsed;
                    geojsonmap.canvas.Children.Add(newtootip);
                }
            }
        }
        private static void MapDataItemsSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GeoJsonMap geojsonmap)
            {
                if (e.OldValue != null && geojsonmap.MapDataItemsSource != null)
                {
                    ((INotifyCollectionChanged)geojsonmap.MapDataItemsSource).CollectionChanged -= geojsonmap.MapDataItemsSourceCollectionChanged;
                }
                if (e.NewValue != null && geojsonmap.MapDataItemsSource != null)
                {
                    ((INotifyCollectionChanged)geojsonmap.MapDataItemsSource).CollectionChanged += geojsonmap.MapDataItemsSourceCollectionChanged;
                }
                geojsonmap.DrawShapeData((System.Collections.IList)e.OldValue);
            }
        }
        private void MapDataItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null && e.OldItems.Count > 0)
            {
                DrawShapeDataAnimation(e.OldItems, false);
            }
            if (e.NewItems != null && e.NewItems.Count > 0)
            {
                DrawShapeDataAnimation(e.NewItems, true);
            }
        }
        private void DrawShapeData(System.Collections.IList oldItems = null)
        {
            if (MapDataItemsSource != null && MapDataItemsSource.Count() > 0)
            {
                DrawShapeDataAnimation((System.Collections.IList)MapDataItemsSource, true);
            }
            if (oldItems != null && oldItems.Count > 0)
            {
                DrawShapeDataAnimation(oldItems, false);
            }
        }
        private void DrawShapeDataAnimation(System.Collections.IList items, bool isNew)
        {
            if (items != null && items.Count > 0)
            {
                foreach (var data in items)
                {
                    if (data is IGeoJsonMapSource geodata)
                    {
                        var curgeopath = geoPathList.FirstOrDefault(x => x.Properties.AdCode == geodata.AdCode);
                        if (curgeopath != null)
                        {
                            Storyboard sb = new Storyboard();
                            SolidColorBrush color;
                            if (isNew)
                            {
                                color = new SolidColorBrush(((SolidColorBrush)geodata.ShapeFill).Color);
                                SolidColorBrush oricolor = new SolidColorBrush(((SolidColorBrush)ShapeFill).Color);
                                curgeopath.PathGeometry.Fill = oricolor;  //Cannot Animate Immutable object
                            }
                            else
                            {
                                color = new SolidColorBrush(((SolidColorBrush)ShapeFill).Color);
                            }
                            SolidColorAnimation animation = new SolidColorAnimation(color, AnimationsSpeed);
                            Storyboard.SetTargetProperty(animation, new PropertyPath("(Path.Fill).(SolidColorBrush.Color)"));
                            sb.Children.Add(animation);
                            sb.Begin(curgeopath.PathGeometry);
                        }
                    }
                }
            }

        }
        #endregion
    }
    public class SolidColorAnimation : ColorAnimation
    {
        public SolidColorAnimation(Brush fromBrush, Brush toBrush, Duration duration)
        {
            FromBrush = (SolidColorBrush)toBrush;
            ToBrush = (SolidColorBrush)toBrush;
            Duration = duration;
        }
        public SolidColorAnimation(Brush toBrush, Duration duration)
        {
            ToBrush = (SolidColorBrush)toBrush;
            Duration = duration;
        }
        public SolidColorBrush ToBrush
        {
            get { return To == null ? null : new SolidColorBrush(To.Value); }
            set { To = value?.Color; }
        }
        public SolidColorBrush FromBrush
        {
            get { return From == null ? null : new SolidColorBrush(From.Value); }
            set { From = value?.Color; }
        }
    }
}
