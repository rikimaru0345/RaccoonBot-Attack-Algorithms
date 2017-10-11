using System;
using System.Drawing;
using System.Linq;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using System.Collections.Generic;

namespace SmartFourFingersDeploy
{
    static class SmartFourFingersHelper
    {
        public static IEnumerable<ElixirCollector> collectors; 
        public static IEnumerable<GoldMine> mines; 
        public static IEnumerable<DarkElixirDrill> drills;

        public static T NextOf<T>(this List<T> list, T item)
        {
            return list[(list.IndexOf(item) + 1) == list.Count ? 0 : (list.IndexOf(item) + 1)];
        }

        /// <summary>
        /// check Opppnent base to see if it's engineered base or not 
        /// </summary>
        /// <returns>true if engineered</returns>
        public static bool IsEngineeredBase()
        {
            var defenses = ArcherTower.Find()?.Count();
            defenses += WizardTower.Find()?.Count();
            defenses += AirDefense.Find().Count();

            if (defenses <= 3)
                return true;

            return false;
        }

        /// <summary>
        /// get where to deploy hereos according to user defined
        /// </summary>
        /// <param name="deployHeroesAt">user defined location 1 is TwonHall , 2 is Dark Elixir Storage</param>
        /// <returns></returns>
        public static PointFT GetHeroesTarget(int deployHeroesAt)
        {
            var target = new PointFT(0f, 0f);

            switch (deployHeroesAt)
            {
                case 1:
                    var th = TownHall.Find()?.Location.GetCenter();
                    target = th != null ? (PointFT)th : target;
                    break;
                case 2:
                    var de = DarkElixirStorage.Find()?.FirstOrDefault()?.Location.GetCenter();
                    target = de != null ? (PointFT)de : target;
                    break;
            }
            return target;
        }

        /// <summary>
        /// Get the core of the opponent base
        /// </summary>
        public static void SetCore()
        {
            var maxRedPointX = (GameGrid.RedPoints.Where(p => -18 < p.Y && p.Y < 18)?.Max(point => point.X) ?? GameGrid.MaxX);
            var minRedPointX = (GameGrid.RedPoints.Where(p => -18 < p.Y && p.Y < 18)?.Min(point => point.X) ?? GameGrid.MinX);
            var maxRedPointY = (GameGrid.RedPoints.Where(p => -18 < p.X && p.X < 18)?.Max(point => point.Y) ?? GameGrid.MaxY);
            var minRedPointY = (GameGrid.RedPoints.Where(p => -18 < p.X && p.X < 18)?.Min(point => point.Y) ?? GameGrid.MinY);

            // Build a box around the base
            var left = new PointFT(minRedPointX, maxRedPointY);
            var top = new PointFT(maxRedPointX, maxRedPointY);
            var right = new PointFT(maxRedPointX, minRedPointY);
            var bottom = new PointFT(minRedPointX, minRedPointY);

            // Draw border around the base
            var border = new RectangleT((int)minRedPointX, (int)maxRedPointY, (int)(maxRedPointX - minRedPointX), (int)(minRedPointY - maxRedPointY));

            // Core is center of the box
            SmartFourFingersDeploy.Core = border.GetCenter();
        }

        /// <summary>
        /// Check to see how many collector and mine near to the redline by user defined distance
        /// </summary>
        /// <param name="userDistance">Minimum distance for exposed colloctors and mines</param>
        /// <param name="minCollectors">minimum exposed collectors</param>
        /// <param name="minMines">minimum exposed mines</param>
        /// <param name="AttackName">Attack name for logs and debugging</param>
        /// <param name="debug">debug mode in advanced settings</param>
        /// <returns>true if matches user defined min collectores and mines</returns>
        public static bool IsBaseMinCollectorsAndMinesOutside(int userDistance, int minCollectors, int minMines, string AttackName, int debug)
        {
            var distance = userDistance * userDistance;

            var redPoints = GameGrid.RedPoints.Where(
                point =>
                !(point.X > 18 && point.Y > 18 || point.X > 18 && point.Y < -18 || point.X < -18 && point.Y > 18 ||
                point.X < -18 && point.Y < -18));

            collectors = ElixirCollector.Find().Where(c => c.Location.GetCenter()
                .DistanceSq(redPoints.OrderBy(p => p.DistanceSq(c.Location.GetCenter()))
                .FirstOrDefault()) <= distance);

            mines = GoldMine.Find().Where(c => c.Location.GetCenter()
                .DistanceSq(redPoints.OrderBy(p => p.DistanceSq(c.Location.GetCenter()))
                .FirstOrDefault()) <= distance);

            drills = DarkElixirDrill.Find().Where(c => c.Location.GetCenter()
                .DistanceSq(redPoints.OrderBy(p => p.DistanceSq(c.Location.GetCenter()))
                .FirstOrDefault()) <= distance);

            int collectorsCount = collectors != null ? collectors.Count() : 0;
            int minesCount = mines != null ? mines.Count() : 0;
            int drillsCount = drills != null ? drills.Count() : 0;

            // Set total count of targets
            SmartFourFingersDeploy.TotalTargetsCount = collectorsCount + minesCount + drillsCount;

            // four corners
            var top = new PointFT((float)GameGrid.DeployExtents.MaxX + 1, GameGrid.DeployExtents.MaxY + 4);
            var right = new PointFT((float)GameGrid.DeployExtents.MaxX + 1, GameGrid.DeployExtents.MinY - 4);
            var bottom = new PointFT((float)GameGrid.DeployExtents.MinX - 1, GameGrid.DeployExtents.MinY - 4);
            var left = new PointFT((float)GameGrid.DeployExtents.MinX - 1, GameGrid.DeployExtents.MaxY + 4);

            SetCore();

            var corners = new List<Tuple<PointFT, PointFT>>
            {
                new Tuple<PointFT, PointFT>(top, right),
                new Tuple<PointFT, PointFT>(bottom, right),
                new Tuple<PointFT, PointFT>(bottom, left),
                new Tuple<PointFT, PointFT>(top, left)
            };

            // loop throw the 4 sides and count targets on each side
            var targetsAtLine = new List<int>();
            foreach (var l in corners)
            {
                var colCount = collectors.Where(t => t.Location.GetCenter().
                        IsInTri(SmartFourFingersDeploy.Core, l.Item1, l.Item2))?.Count() ?? 0;
                var minCount = mines.Where(t => t.Location.GetCenter().
                        IsInTri(SmartFourFingersDeploy.Core, l.Item1, l.Item2))?.Count() ?? 0;
                var drillCount = drills.Where(t => t.Location.GetCenter().
                        IsInTri(SmartFourFingersDeploy.Core, l.Item1, l.Item2))?.Count() ?? 0;
                var total = colCount + minCount + drillCount;

                targetsAtLine.Add(total);
            }

            SmartFourFingersDeploy.TargetsAtLine = targetsAtLine;

            var op = new Opponent(0);
            //if (!op.IsForcedAttack )
            {
                Log.Info($"{AttackName} NO. of Colloctors & mines near from red line:");
                Log.Info($"elixir colloctors is {collectorsCount}");
                Log.Info($"gold mines is {minesCount}");
                Log.Info($"----------------------------");
                Log.Info($"sum of all is {collectorsCount + minesCount}");

                if (debug == 1)
                {
                    using (Bitmap bmp = Screenshot.Capture())
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            foreach (var c in collectors)
                            {
                                var point = c.Location.GetCenter();
                                Visualize.Target(bmp, point, 30, Color.Purple);
                            }

                            foreach (var c in mines)
                            {
                                var point = c.Location.GetCenter();
                                Visualize.Target(bmp, point, 30, Color.Gold);
                            }

                            foreach (var c in drills)
                            {
                                var point = c.Location.GetCenter();
                                Visualize.Target(bmp, point, 30, Color.Black);
                            }
                            DrawLine(bmp, Color.Red, SmartFourFingersDeploy.Core, top);
                            DrawLine(bmp, Color.Red, SmartFourFingersDeploy.Core, right);
                            DrawLine(bmp, Color.Red, SmartFourFingersDeploy.Core, bottom);
                            DrawLine(bmp, Color.Red, SmartFourFingersDeploy.Core, left);
                        }
                        var d = DateTime.UtcNow;
                        Screenshot.Save(bmp, "Collectors and Mines {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}");
                    }
                }
            }
            if (collectorsCount >= minCollectors && minesCount >= minMines)
                return true;
            else
            {
                Log.Warning($"{AttackName} this base doesn't meets Collocetors & Mines requirements");
                return false;
            }
        }

        /// <summary>
        /// Draws a line between two points on the bitmap
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="color"></param>
        /// <param name="startPoint"></param>
        /// <param name="endPoint"></param>
        public static void DrawLine(Bitmap canvas, Color color, PointFT startPoint, PointFT endPoint)
        {
            using (var g = Graphics.FromImage(canvas))
            {
                var pen = new Pen(color, 2);
                g.DrawLine(pen, startPoint.ToScreenAbsolute().X, startPoint.ToScreenAbsolute().Y, endPoint.ToScreenAbsolute().X, endPoint.ToScreenAbsolute().Y);
            }
        }

        /// <summary>
        /// Draws a Point on the canvas
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="color"></param>
        /// <param name="point"></param>
        public static void DrawPoint(Bitmap canvas, Color color, PointFT point)
        {
            using (var g = Graphics.FromImage(canvas))
            {
                var pen = new Pen(color, 2);
                g.DrawEllipse(pen, point.ToScreenAbsolute().X, point.ToScreenAbsolute().Y, 3, 3);
            }
        }

        public static PointFT[] GenerateGreenPoints()
        {
            var points = new HashSet<PointFT>();

            for (int i = (int)PointFT.MinDeployAreaX; i <= (int)PointFT.MaxDeployAreaX; i++)
            {
                float max = PointFT.MaxDeployAreaX + 0.5f;
                if (i >= (int)PointFT.MinDeployAreaX+5) //Skip Points off the Right Hand side of Screen.
                    points.Add(new PointFT(max, i)); //Top Right

                points.Add(new PointFT(i, max)); //Top Left

                if (i >= (int)PointFT.MinDeployAreaX + 11) //Skip points that are on the bottom cuttoff.
                    points.Add(new PointFT(-max, i)); //Bottom Left

                if (i >= (int)PointFT.MinDeployAreaX + 12 && i <= (int)PointFT.MaxDeployAreaX - 6) //Skip points that are on the bottom cuttoff. & Righthand side of screen.
                    points.Add(new PointFT(i, -(max + 1))); //Bottom Right //Make bottom right more outside Because of incorrect Y shift in the bot.
            }
            return points.ToArray();
        }

        /// <summary>
        /// See if point is inside a triangle or not
        /// </summary>
        /// <param name="pt">the point</param>
        /// <param name="v1">triangle point1</param>
        /// <param name="v2">triangle point2</param>
        /// <param name="v3">triangle point3</param>
        /// <returns>true if inside</returns>
        public static bool IsInTri(this PointFT pt, PointFT v1, PointFT v2, PointFT v3)
        {
            var TotalArea = CalcTriArea(v1, v2, v3);
            var Area1 = CalcTriArea(pt, v2, v3);
            var Area2 = CalcTriArea(pt, v1, v3);
            var Area3 = CalcTriArea(pt, v1, v2);

            if((Area1 + Area2 + Area3) > TotalArea)
                return false;
            else
                return true;
        }

        /// <summary>
        /// Find the area of a triangle. This function uses the 1/2 determinant
        /// </summary>
        /// <param name="v1">triangle point1</param>
        /// <param name="v2">triangle point2</param>
        /// <param name="v3">triangle point3</param>
        /// <returns>triangle area</returns>
        public static float CalcTriArea(PointFT v1, PointFT v2, PointFT v3)
        {
            return Math.Abs((v1.X * (v2.Y - v3.Y) + v2.X * (v3.Y - v1.Y) + v3.X * (v1.Y - v2.Y)) / 2f);
        }
    }
}