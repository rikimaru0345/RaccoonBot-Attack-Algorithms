using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LavaLoonDeploy.Visuals
{
    class LinesObject : VisualObject
    {
        Line[] lines;
        Pen pen;

        public LinesObject(string name, Color argbColor, IEnumerable<Line> lines)
        {
            Name = name;
            ArgbColor = argbColor;
            this.lines = lines.ToArray();
            this.pen = new Pen(ArgbColor, 3);
        }

        public LinesObject(string name, Pen pen, IEnumerable<Line> lines)
        {
            Name = name;
            this.lines = lines.ToArray();
            this.pen = pen;
        }

        internal override void Draw(Graphics g)
        {
            foreach(var line in lines)
            {
                var start = line.Start;
                var end = line.End;
                g.DrawLine(pen, start.X, start.Y, end.X, end.Y);
            }
        }
    }
}
