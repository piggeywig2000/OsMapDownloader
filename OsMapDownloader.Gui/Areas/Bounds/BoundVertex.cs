using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace OsMapDownloader.Gui.Areas.Bounds
{
    internal class BoundVertex : Control
    {
        private Point _center = new Point();
        public Point Center
        {
            get => _center;
            set
            {
                if (_center != value)
                {
                    _center = value;
                    InvalidateVisual();
                }
            }
        }

        private bool _isInvalid = false;
        public bool IsInvalid
        {
            get => _isInvalid;
            set
            {
                if (_isInvalid != value)
                {
                    _isInvalid = value;
                    InvalidateVisual();
                }
            }
        }

        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    InvalidateVisual();
                }
            }
        }

        private bool _isPreview = false;
        public bool IsPreview
        {
            get => _isPreview;
            set
            {
                if (_isPreview != value)
                {
                    _isPreview = value;
                    InvalidateVisual();
                }
            }
        }

        public override void Render(DrawingContext context)
        {
            ZIndex = IsSelected ? 4 : 2;
            context.DrawEllipse(new SolidColorBrush(IsInvalid ? new Color(255, 255, 204, 204) : new Color(255, 255, 255, 255)), new Pen(new SolidColorBrush(IsInvalid ? new Color(255, 255, 0, 0) : (IsSelected ? new Color(255, 255, 0, 255) : new Color(255, 0, 255, 0))), IsInvalid ? 4 : 2, IsPreview && !IsInvalid ? new DashStyle(new double[] { 1, 1 }, 0) : null), Center, IsInvalid ? 8 : 4, IsInvalid ? 8 : 4);
        }
    }
}
