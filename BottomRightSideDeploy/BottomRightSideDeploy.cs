using System;
using System.Collections.Generic;
using System.Linq;
using CoC_Bot.API;
using System.Reflection;
using System.Drawing;

[assembly: Addon("BottomRightSideDeploy", "test bottom right side", "CobraTST")]
namespace BottomRightSideDeploy
{
    [AttackAlgorithm("BottomRightSideDeploy", "test bottom right side")]
    public class BottomRightSideDeploy : BaseAttack
    {
        PointFT point;

        public BottomRightSideDeploy(Opponent opponent) : base(opponent)
        {
        }

        public override IEnumerable<int> AttackRoutine()
        {
            // Bottom right side
            var rightBottom = new PointFT((float)GameGrid.MaxX - 2, (float)GameGrid.DeployExtents.MinY);
            var bottomRight = new PointFT((float)GameGrid.MinX + 8, (float)GameGrid.DeployExtents.MinY);

            var center = new PointFT(bottomRight.X + 0.5f * (rightBottom.X - bottomRight.X),
                             bottomRight.Y + 0.5f * (rightBottom.Y - bottomRight.Y));

            // Screenshot for 3 points for the deployment line
            using (var bmp = Screenshot.Capture())
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    Visualize.RectangleT(bmp, new RectangleT((int)rightBottom.X, (int)rightBottom.Y, 1, 1), new Pen(Color.Blue));
                    Visualize.RectangleT(bmp, new RectangleT((int)bottomRight.X, (int)bottomRight.Y, 1, 1), new Pen(Color.Blue));

                    Visualize.RectangleT(bmp, new RectangleT((int)center.X, (int)center.Y, 1, 1), new Pen(Color.White));
                }
                var d = DateTime.UtcNow;
                Screenshot.Save(bmp, "Bottom Right {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}");
            }

            // Deploy troops
            var units = Deploy.GetTroops();
            units.OrderForDeploy();
            for (var i = 3; i >= 1; i--)
            {
                switch (i)
                {
                    case 3:
                        point = rightBottom;
                        break;
                    case 2:
                        point = bottomRight;
                        break;
                    case 1:
                        point = center;
                        break;
                }

                foreach (var unit in units)
                {
                    if (unit?.Count > 0)
                    {
                        foreach (var t in Deploy.AtPoint(unit, point, unit.Count / i))
                            yield return t;
                    }
                }
            }  
        }

        public override double ShouldAccept()
        {
            return 1;
        }
    }
}
