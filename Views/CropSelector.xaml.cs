using System;
using System.Linq;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using WallpaperScheduler.Helpers;
using WallpaperScheduler.Models;
using Windows.Foundation;

namespace WallpaperScheduler.Views
{
    public class CursorGrid : Grid
    {
        public void SetCursor(InputCursor cursor)
        {
            ProtectedCursor = cursor;
        }
    }

    public sealed partial class CropSelector : UserControl
    {
        private const double MinNormWidth = 0.1;

        private readonly Rectangle _sel;
        private readonly CursorGrid[] _handles = new CursorGrid[4];
        private double _imageAspect = 16.0 / 9.0;
        private double _ratio = 1;          // normalized selection W:H (screenAspect / imageAspect)
        private double _contentW, _contentH; // image content area within the control (px)
        private Point _contentOffset;
        private Rect _selNorm;               // selection, normalized 0..1 relative to the image
        private bool _moving;
        private int _activeCorner = -1;      // 0=TL,1=TR,2=BL,3=BR
        private Point _lastNorm;

        public CropSelector()
        {
            InitializeComponent();
            var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            var overlay = new SolidColorBrush(Microsoft.UI.Colors.Black) { Opacity = 0.15 };
            _sel = new Rectangle
            {
                Stroke = accent,
                StrokeThickness = 2,
                Fill = overlay,
                RadiusX = 4,
                RadiusY = 4
            };
            Overlay.Children.Add(_sel);
            for (int i = 0; i < 4; i++)
            {
                var ellipse = new Ellipse
                {
                    Width = 16,
                    Height = 16,
                    Fill = accent,
                    Stroke = new SolidColorBrush(Microsoft.UI.Colors.White),
                    StrokeThickness = 1.5
                };
                _handles[i] = new CursorGrid
                {
                    Width = 16,
                    Height = 16
                };
                _handles[i].Children.Add(ellipse);
                Overlay.Children.Add(_handles[i]);
            }

            _handles[0].SetCursor(InputSystemCursor.Create(InputSystemCursorShape.SizeNorthwestSoutheast));
            _handles[1].SetCursor(InputSystemCursor.Create(InputSystemCursorShape.SizeNortheastSouthwest));
            _handles[2].SetCursor(InputSystemCursor.Create(InputSystemCursorShape.SizeNortheastSouthwest));
            _handles[3].SetCursor(InputSystemCursor.Create(InputSystemCursorShape.SizeNorthwestSoutheast));
        }

        public void Load(WallpaperItem item, double maxBoxW, double maxBoxH)
        {
            Img.Source = new BitmapImage(new Uri(item.FullPath));
            using var src = new System.Drawing.Bitmap(item.FullPath);
            _imageAspect = (double)src.Width / src.Height;

            // responsive: fit the wallpaper's real ratio into the available box
            double w = maxBoxW;
            double h = w / _imageAspect;
            if (h > maxBoxH) { h = maxBoxH; w = h * _imageAspect; }
            Width = w;
            Height = h;
            UpdateContentRect();

            _ratio = CropHelper.ScreenAspect / _imageAspect;

            if (item.HasCustomCrop)
            {
                _selNorm = new Rect(
                    Math.Clamp(item.CropLeft, 0, 1),
                    Math.Clamp(item.CropTop, 0, 1),
                    Math.Clamp(item.CropWidth, 0, 1),
                    Math.Clamp(item.CropHeight, 0, 1));
            }
            else
            {
                // largest screen-aspect rect, centered
                double nw, nh;
                if (_ratio >= 1) { nw = 1; nh = nw / _ratio; if (nh > 1) { nh = 1; nw = nh * _ratio; } }
                else { nh = 1; nw = nh * _ratio; }
                _selNorm = new Rect((1 - nw) / 2, (1 - nh) / 2, nw, nh);
            }
            ApplyVisual();
        }

        public void ApplyTo(WallpaperItem item)
        {
            item.CropLeft = _selNorm.X;
            item.CropTop = _selNorm.Y;
            item.CropWidth = _selNorm.Width;
            item.CropHeight = _selNorm.Height;
        }

        private void UpdateContentRect()
        {
            double ctrlAspect = Width / Height;
            if (_imageAspect > ctrlAspect)
            {
                _contentW = Width;
                _contentH = Width / _imageAspect;
                _contentOffset = new Point(0, (Height - _contentH) / 2);
            }
            else
            {
                _contentH = Height;
                _contentW = Height * _imageAspect;
                _contentOffset = new Point((Width - _contentW) / 2, 0);
            }
        }

        private Rect NormToDisplay(Rect n)
            => new(_contentOffset.X + n.X * _contentW, _contentOffset.Y + n.Y * _contentH,
                   n.Width * _contentW, n.Height * _contentH);

        private Point DisplayToNorm(Point p)
            => new((p.X - _contentOffset.X) / _contentW, (p.Y - _contentOffset.Y) / _contentH);

        private void ApplyVisual()
        {
            var d = NormToDisplay(_selNorm);
            Canvas.SetLeft(_sel, d.X);
            Canvas.SetTop(_sel, d.Y);
            _sel.Width = d.Width;
            _sel.Height = d.Height;

            Point[] corners = { new(d.X, d.Y), new(d.X + d.Width, d.Y), new(d.X, d.Y + d.Height), new(d.X + d.Width, d.Y + d.Height) };
            for (int i = 0; i < 4; i++)
            {
                Canvas.SetLeft(_handles[i], corners[i].X - _handles[i].Width / 2);
                Canvas.SetTop(_handles[i], corners[i].Y - _handles[i].Height / 2);
            }
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pos = e.GetCurrentPoint(Root).Position;
            _activeCorner = HitCorner(pos);
            if (_activeCorner >= 0)
            {
                _moving = false;
            }
            else if (IsInSel(pos))
            {
                _moving = true;
                _activeCorner = -1;
            }
            else
            {
                return;
            }
            _lastNorm = DisplayToNorm(pos);
            Root.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_moving && _activeCorner < 0) return;
            var norm = DisplayToNorm(e.GetCurrentPoint(Root).Position);
            if (_moving)
            {
                double dx = norm.X - _lastNorm.X;
                double dy = norm.Y - _lastNorm.Y;
                Move(dx, dy);
            }
            else
            {
                ResizeFromCorner(norm);
            }
            _lastNorm = norm;
            ApplyVisual();
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _moving = false;
            _activeCorner = -1;
            Root.ReleasePointerCapture(e.Pointer);
        }

        private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _moving = false;
            _activeCorner = -1;
        }

        private int HitCorner(Point p)
        {
            var d = NormToDisplay(_selNorm);
            Point[] corners = { new(d.X, d.Y), new(d.X + d.Width, d.Y), new(d.X, d.Y + d.Height), new(d.X + d.Width, d.Y + d.Height) };
            for (int i = 0; i < 4; i++)
            {
                if (Math.Abs(p.X - corners[i].X) <= 18 && Math.Abs(p.Y - corners[i].Y) <= 18) return i;
            }
            return -1;
        }

        private bool IsInSel(Point p)
        {
            var d = NormToDisplay(_selNorm);
            return p.X >= d.X && p.X <= d.X + d.Width && p.Y >= d.Y && p.Y <= d.Y + d.Height;
        }

        private void Move(double dx, double dy)
        {
            double x = Math.Clamp(_selNorm.X + dx, 0, 1 - _selNorm.Width);
            double y = Math.Clamp(_selNorm.Y + dy, 0, 1 - _selNorm.Height);
            _selNorm = new Rect(x, y, _selNorm.Width, _selNorm.Height);
        }

        private void ResizeFromCorner(Point p)
        {
            // anchor = opposite corner of the dragged corner
            double anchorX, anchorY;
            switch (_activeCorner)
            {
                case 0: anchorX = _selNorm.Right; anchorY = _selNorm.Bottom; break;
                case 1: anchorX = _selNorm.X;     anchorY = _selNorm.Bottom; break;
                case 2: anchorX = _selNorm.Right; anchorY = _selNorm.Y;      break;
                default: anchorX = _selNorm.X;     anchorY = _selNorm.Y;      break;
            }

            double newW = Math.Abs(p.X - anchorX);
            double maxW = Math.Min(1, Math.Max(anchorX, 1 - anchorX));
            newW = Math.Clamp(newW, MinNormWidth, maxW);
            double newH = newW / _ratio;
            if (newH > 1) { newH = 1; newW = newH * _ratio; }

            double newX = (_activeCorner == 0 || _activeCorner == 2) ? anchorX - newW : anchorX;
            double newY = (_activeCorner == 0 || _activeCorner == 1) ? anchorY - newH : anchorY;
            newX = Math.Clamp(newX, 0, 1 - newW);
            newY = Math.Clamp(newY, 0, 1 - newH);

            _selNorm = new Rect(newX, newY, newW, newH);
        }
    }
}