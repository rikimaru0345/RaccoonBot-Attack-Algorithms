using CoC_Bot.API;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LavaLoonDeploy.Visuals
{
    class RectangleObject : VisualObject
    {
        RectangleT[] Rects;
        Pen Outline;

        public RectangleObject(string name, Color argbColor, IEnumerable<RectangleT> rects, Pen outline = null)
        {
            Name = name;
            ArgbColor = argbColor;
            Rects = rects.ToArray();
            Outline = outline ?? new Pen(Color.FromArgb(0xC0, 0x00, 0xFF, 0xFF), 2);
        }

        internal override void Draw(Graphics graphics)
        {
            foreach (var rect in Rects)
            {
                Point p1 = new PointFT((float)rect.Left, rect.Top).ToScreenAbsolute();
                Point p2 = new PointFT((float)rect.Right, rect.Top).ToScreenAbsolute();
                Point p3 = new PointFT((float)rect.Right, rect.Bottom).ToScreenAbsolute();
                Point p4 = new PointFT((float)rect.Left, rect.Bottom).ToScreenAbsolute();
                graphics.FillPolygon(new SolidBrush(ArgbColor), new[] { p1, p2, p3, p4 });
                graphics.DrawLines(Outline, new[] { p1, p2, p3, p4, p1 });
            }
        }
    }
}
