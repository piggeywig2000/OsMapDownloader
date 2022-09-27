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
    internal class BoundEdge : Control
    {
        private Point _from = new Point();
        public Point From
        {
            get => _from;
            set
            {
                if (_from != value)
                {
                    _from = value;
                    InvalidateVisual();
                }
            }
        }

        private Point _to = new Point();
        public Point To
        {
            get => _to;
            set
            {
                if (_to != value)
                {
                    _to = value;
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
            ZIndex = IsSelected ? 3 : 1;
            context.DrawLine(new Pen(new SolidColorBrush(IsSelected ? new Color(255, 255, 0, 255) : new Color(255, 0, 255, 0)), 2, IsPreview ? new DashStyle(new double[] {2, 2}, 0) : null), From, To);
        }
    }
}
