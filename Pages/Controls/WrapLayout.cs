using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Graphics;
using System;

namespace Lock.Pages.Controls
{
    public class WrapLayout : Layout<View>
    {
        private double _totalHeight;
        private double _totalWidth;

        protected override void LayoutChildren(double x, double y, double width, double height)
        {
            double currentX = x;
            double currentY = y;
            double maxHeight = 0;

            foreach (var child in Children)
            {
                if (!child.IsVisible) continue;

                var desiredSize = child.DesiredSize;
                double childWidth = desiredSize.Width;
                double childHeight = desiredSize.Height;

                if (currentX + childWidth > width + x && currentX > x)
                {
                    currentY += maxHeight;
                    currentX = x;
                    maxHeight = 0;
                }

                child.Layout(new Rect(currentX, currentY, childWidth, childHeight));

                currentX += childWidth;
                maxHeight = Math.Max(maxHeight, childHeight);
            }
        }

        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
        {
            double currentX = 0;
            double currentY = 0;
            double maxHeight = 0;
            double totalHeight = 0;
            double totalWidth = 0;

            foreach (var child in Children)
            {
                if (!child.IsVisible) continue;

                child.Measure(widthConstraint, heightConstraint);
                var desiredSize = child.DesiredSize;

                if (currentX + desiredSize.Width > widthConstraint && currentX > 0)
                {
                    currentY += maxHeight;
                    currentX = 0;
                    maxHeight = 0;
                }

                currentX += desiredSize.Width;
                maxHeight = Math.Max(maxHeight, desiredSize.Height);
                totalHeight = currentY + maxHeight;
                totalWidth = Math.Max(totalWidth, currentX);
            }

            _totalHeight = totalHeight;
            _totalWidth = totalWidth;

            return new Size(Math.Min(totalWidth, widthConstraint), totalHeight);
        }

        // Spacing property
        public static readonly BindableProperty SpacingProperty =
            BindableProperty.Create(nameof(Spacing), typeof(double), typeof(WrapLayout), 0.0,
                propertyChanged: (bindable, oldValue, newValue) =>
                {
                    ((WrapLayout)bindable).InvalidateLayout();
                });

        public double Spacing
        {
            get => (double)GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }
    }
}