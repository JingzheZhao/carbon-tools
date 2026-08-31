using System.Drawing;
using System.Windows.Forms;

using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace CarbonTools.Components
{
    /// <summary>
    /// Adds a persistent swatch to the component body: a single click opens
    /// Rhino's color dialog directly, and the swatch always shows the
    /// currently selected color, no right-click menu needed.
    /// </summary>
    public class CCGroupComponentAttributes : GH_ComponentAttributes
    {
        private const int SwatchHeight = 20;
        private const int Margin = 4;

        private RectangleF _swatchBounds;

        public CCGroupComponentAttributes(CCGroupComponent owner) : base(owner)
        {
        }

        protected override void Layout()
        {
            base.Layout();

            var bounds = Bounds;
            bounds.Height += SwatchHeight + Margin;
            Bounds = bounds;

            _swatchBounds = new RectangleF(
                bounds.X + Margin,
                bounds.Bottom - SwatchHeight - Margin,
                bounds.Width - Margin * 2,
                SwatchHeight);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);

            if (channel != GH_CanvasChannel.Objects)
            {
                return;
            }

            var component = (CCGroupComponent)Owner;
            var color = component.CurrentColor;

            using (var brush = new SolidBrush(color))
            {
                graphics.FillRectangle(brush, _swatchBounds);
            }

            graphics.DrawRectangle(Pens.Black, _swatchBounds.X, _swatchBounds.Y, _swatchBounds.Width, _swatchBounds.Height);

            var textColor = GetContrastingTextColor(color);
            using (var textBrush = new SolidBrush(textColor))
            {
                var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                graphics.DrawString("Click to pick color", GH_FontServer.Small, textBrush, _swatchBounds, format);
            }
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && _swatchBounds.Contains(e.CanvasLocation))
            {
                var component = (CCGroupComponent)Owner;
                var color = component.CurrentColor;

                if (Rhino.UI.Dialogs.ShowColorDialog(ref color))
                {
                    component.SetSwatchColor(color);
                }

                return GH_ObjectResponse.Handled;
            }

            return base.RespondToMouseDown(sender, e);
        }

        private static Color GetContrastingTextColor(Color c)
        {
            double luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
            return luminance > 0.6 ? Color.Black : Color.White;
        }
    }
}
