using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoC_Bot;
using CoC_Bot.API;

namespace LavaLoonDeploy.Visuals
{
    class PointsObject : VisualObject
    {
        PointF[] Points;
        float DrawRadiusInTiles;

        public PointsObject(string name, Color argbColor, IEnumerable<PointFT> points, float drawRadiusInTiles = 2)
        {
            Name = name;
            ArgbColor = argbColor;
            Points = points.Select(x => (PointF)x.ToScreenAbsolute()).ToArray();
            DrawRadiusInTiles = drawRadiusInTiles;
        }

        public PointsObject(string name, Color argbColor, IEnumerable<PointF> points, float drawRadiusInTiles = 2)
        {
            Name = name;
            ArgbColor = argbColor;
            Points = points.ToArray();
            DrawRadiusInTiles = drawRadiusInTiles;
        }

        internal override void Draw(Graphics graphics)
        {
            // find the radius of x tiles
            Point p1 = new PointFT(0f, 0f).ToScreenAbsolute();
            Point p2 = new PointFT(0f, DrawRadiusInTiles).ToScreenAbsolute();
            double distance = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

            float widthAndHeight = (float) distance;
            float whOver2 = widthAndHeight/2;

            foreach (var point in Points)
            {
                graphics.FillEllipse(new SolidBrush(ArgbColor), point.X - whOver2, point.Y - whOver2, widthAndHeight, widthAndHeight);
            }
               

        }
    }
}