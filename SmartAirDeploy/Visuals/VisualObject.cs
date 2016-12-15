using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAirDeploy.Visuals
{
    abstract class VisualObject
    {
        protected Color ArgbColor { get; set; }
        protected string Name { get; set; }
        internal abstract void Draw(Graphics g);
    }
}
