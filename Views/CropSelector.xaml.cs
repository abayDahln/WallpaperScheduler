using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using WallpaperScheduler.Models;
using Windows.Foundation;

namespace WallpaperScheduler.Views
{
    public sealed partial class CropSelector : UserControl
    {
        private readonly Rectangle _sel;
        private readonly Ellipse _handle;
        private bool _moving;
        private bool _resizing;
        private Point _lastPos;
        private Rect _selRect;

        public CropSelector()
        {
            InitializeComponent();
            _sel = new Rectangle
            {
                Stroke = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Microsoft.UI.Colors.Black) { Opacity = 0.15 },
                RadiusX = 4,
                RadiusY = 4
            };
            _handle = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                Stroke = new SolidColorBrush(Microsoft.UI.Colors.White),
                StrokeThickness = 1.5
            };
            Overlay.Children.Add(_sel);
            Overlay.Children.Add(_handle);
        }

        public void Load(WallpaperItem item)
        {
            Img.Source = new BitmapImage(new Uri(item.FullPath));
            using var src = new System.Drawing.Bitmap(item.FullPath);
            double aspect = (double)src.Width / src.Height;
            double maxW = Math.Min(560, Math.Max(320, 480 * aspect));
            Width = maxW;
            Height = maxW / aspect;

            double left = item.CropLeft * Width;
            double top = item.CropTop * Height;
            _selRect = new Rect(left, top, Math.Max(20, item.CropWidth * Width), Math.Max(20, item.CropHeight * Height));
            ApplySelVisual();
        }

        public void ApplyTo(WallpaperItem item)
        {
            item.CropLeft = _selRect.X / Width;
            item.CropTop = _selRect.Y / Height;
            item.CropWidth = _selRect.Width / Width;
            item.CropHeight = _selRect.Height / Height;
        }

        private void ApplySelVisual()
        {
            Canvas.SetLeft(_sel, _selRect.X);
            Canvas.SetTop(_sel, _selRect.Y);
            _sel.Width = _selRect.Width;
            _sel.Height = _selRect.Height;
            Canvas.SetLeft(_handle, _selRect.X + _selRect.Width - _handle.Width / 2);
            Canvas.SetTop(_handle, _selRect.Y + _selRect.Height - _handle.Height / 2);
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pos = e.GetCurrentPoint(Root).Position;
            if (IsInHandle(pos))
            {
                _resizing = true;
            }
            else if (IsInSel(pos))
            {
                _moving = true;
            }
            else
            {
                return;
            }
            _lastPos = pos;
            Root.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_moving && !_resizing) return;
            var pos = e.GetCurrentPoint(Root).Position;
            double dx = pos.X - _lastPos.X;
            double dy = pos.Y - _lastPos.Y;
            if (_moving) Move(dx, dy);
            else Resize(dx, dy);
            _lastPos = pos;
            ApplySelVisual();
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _moving = false;
            _resizing = false;
            Root.ReleasePointerCapture(e.Pointer);
        }

        private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _moving = false;
            _resizing = false;
        }

        private bool IsInSel(Point p)
            => p.X >= _selRect.X && p.X <= _selRect.X + _selRect.Width
            && p.Y >= _selRect.Y && p.Y <= _selRect.Y + _selRect.Height;

        private bool IsInHandle(Point p)
        {
            double cx = _selRect.X + _selRect.Width;
            double cy = _selRect.Y + _selRect.Height;
            return Math.Abs(p.X - cx) <= 16 && Math.Abs(p.Y - cy) <= 16;
        }

        private void Move(double dx, double dy)
        {
            double x = Math.Clamp(_selRect.X + dx, 0, Width - _selRect.Width);
            double y = Math.Clamp(_selRect.Y + dy, 0, Height - _selRect.Height);
            _selRect = new Rect(x, y, _selRect.Width, _selRect.Height);
        }

        private void Resize(double dx, double dy)
        {
            double w = Math.Clamp(_selRect.Width + dx, 20, Width - _selRect.X);
            double h = Math.Clamp(_selRect.Height + dy, 20, Height - _selRect.Y);
            _selRect = new Rect(_selRect.X, _selRect.Y, w, h);
        }
    }
}