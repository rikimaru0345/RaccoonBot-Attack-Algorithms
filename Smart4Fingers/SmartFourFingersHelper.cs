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
                    target = (PointFT)TownHall.Find()?.Location.GetCenter();
                    break;
                case 2:
                    target = (PointFT)DarkElixirStorage.Find()?.FirstOrDefault()?.Location.GetCenter();
                    break;
            }
            return target;
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

            var collectors = ElixirCollector.Find().Where(c => c.Location.GetCenter()
                .DistanceSq(redPoints.OrderBy(p => p.DistanceSq(c.Location.GetCenter()))
                .FirstOrDefault()) <= distance);

            var mines = GoldMine.Find().Where(c => c.Location.GetCenter()
                .DistanceSq(redPoints.OrderBy(p => p.DistanceSq(c.Location.GetCenter()))
                .FirstOrDefault()) <= distance);

            int collectorsCount = collectors != null ? collectors.Count() : 0;
            int minesCount = mines != null ? mines.Count() : 0;

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
                            Visualize.RectangleT(bmp, new RectangleT((int)point.X, (int)point.Y, 2, 2), new Pen(Color.Blue));
                        }


                        foreach (var c in mines)
                        {
                            var point = c.Location.GetCenter();
                            Visualize.RectangleT(bmp, new RectangleT((int)point.X, (int)point.Y, 2, 2), new Pen(Color.White));
                        }
                    }
                    var d = DateTime.UtcNow;
                    Screenshot.Save(bmp, "Collectors and Mines {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}");
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
    }
}
