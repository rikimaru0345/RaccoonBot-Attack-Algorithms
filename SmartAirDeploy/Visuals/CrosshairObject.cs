using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoC_Bot.API;

namespace LavaLoonDeploy.Visuals
{
	class CrosshairObject : VisualObject
	{
		PointF[] points;
		Pen pen;
		int size;

		public CrosshairObject(PointF[] points, Color? color, int size = 41)
		{
			this.points = points;
			pen = color == null ? Pens.Red : new Pen((Color)color, 3);
			this.size = size;
		}

		public CrosshairObject(PointFT[] points, Color? color, int size = 41)
		{
			this.points = points.Select(x => (PointF)x.ToScreenAbsolute()).ToArray();
			pen = color == null ? Pens.Red : new Pen((Color)color, 3);
			this.size = size;
		}

		internal override void Draw(Graphics g)
		{
			foreach(var point in points)
			{
				g.SmoothingMode = SmoothingMode.HighQuality;
				g.DrawLine(pen, point.X - size / 2, point.Y, point.X + size / 2, point.Y);
				g.DrawLine(pen, point.X, point.Y - size / 2, point.X, point.Y + size / 2);
			}
		}
	}
}
