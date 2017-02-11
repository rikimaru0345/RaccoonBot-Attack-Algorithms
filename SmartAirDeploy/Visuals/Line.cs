using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoC_Bot.API;

namespace SmartAirDeploy
{
    class Line
    {
        public PointF Start;
        public PointF End;

        public Line(PointFT start, PointFT end)
        {
            Start = start.ToScreenAbsolute();
            End = end.ToScreenAbsolute();
        }

        public Line(PointF start, PointF end)
        {
            Start = start;
            End = end;
        }
    }
}
