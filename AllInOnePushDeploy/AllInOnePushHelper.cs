using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;

namespace AllInOnePushDeploy
{
    static class AllInOnePushHelper
    {
        static bool targetIsSet = false;

        /// <summary>
        /// Count how meny walls in the spell area 
        /// </summary>
        /// <param name="eqPoint">pointFT of spell </param>
        /// <param name="eqRadius">float radius of spell </param>
        /// <returns>Count of walls found</returns>
        public static int GetWallsInsideSpell(PointFT eqPoint, float eqRadius = 3.5f)
        {
            var walls = Wall.Find()?.Where(w => w.Location.X >= eqPoint.X - eqRadius && w.Location.X <= eqPoint.X + eqRadius && w.Location.Y >= eqPoint.Y - eqRadius && w.Location.Y <= eqPoint.Y + eqRadius && w.Location.GetCenter().DistanceSq(eqPoint) <= eqRadius * eqRadius);
            var eqWalls = walls?.Count();

            return (int)eqWalls;
        }

        /// <summary>
        /// Get The first wall form deploy point to deploy jump spell on
        /// </summary>
        /// <param name="PointOfDeployXorY">The deploy point X or Y</param>
        /// <param name="DirctionOfWalls">What axis to use X or Y</param>
        /// <returns>the first wall</returns>
        public static PointFT GetFirstWallForJump(float PointOfDeployXorY, string DirctionOfWalls)
        {
            IEnumerable<Wall> firstWall;
            if (DirctionOfWalls == "Y")
            {
                firstWall = Wall.Find()?.Where(w => ((int)w.Location.GetCenter().Y == (int)PointOfDeployXorY) || ((int)w.Location.GetCenter().Y + 1 == (int)PointOfDeployXorY) || ((int)w.Location.GetCenter().Y - 1 == (int)PointOfDeployXorY) || ((int)w.Location.GetCenter().Y - 2 == (int)PointOfDeployXorY) || ((int)w.Location.GetCenter().Y + 2 == (int)PointOfDeployXorY));
            }
            else
            {
                firstWall = Wall.Find()?.Where(w => ((int)w.Location.GetCenter().X == (int)PointOfDeployXorY) || ((int)w.Location.GetCenter().X + 1 == (int)PointOfDeployXorY) || ((int)w.Location.GetCenter().X - 1 == (int)PointOfDeployXorY) || ((int)w.Location.GetCenter().X - 2 == (int)PointOfDeployXorY) || ((int)w.Location.GetCenter().X + 2 == (int)PointOfDeployXorY));                
            }
            return firstWall == null ? AllInOnePushDeploy.Origin : firstWall.OrderBy(w => w.Location.GetCenter().DistanceSq(AllInOnePushDeploy.Origin)).First().Location.GetCenter();
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
            AllInOnePushDeploy.Core = border.GetCenter();
        }

        /// <summary>
        /// Searching for target according to user input
        /// </summary>
        /// <param name="settingsTarget">Advanced Settings target select</param>
        /// <returns></returns>
        public static IEnumerable<int> SeTarget(int settingsTarget)
        {
            if (settingsTarget == 2)
            {
                // Eagle Artillerty search
                foreach (var f in SearchTarget(EagleArtillery.Find(CacheBehavior.ForceScan)))
                    yield return f;

                // If not found switch to thownhall
                if (targetIsSet == false)
                {
                    foreach (var f in SearchTarget(TownHall.Find(CacheBehavior.ForceScan)))
                        yield return f;
                }
            }
            else if (settingsTarget == 0)
            {
                // Townhall search
                foreach (var f in SearchTarget(TownHall.Find(CacheBehavior.ForceScan)))
                    yield return f;

                // If not found switch to the core of the base 
                if (targetIsSet == false)
                {
                    AllInOnePushDeploy.Target = AllInOnePushDeploy.Core;
                    targetIsSet = true;
                }
            }
            else if (settingsTarget == 1)
            {
                // Dark Elixir Storage search
                foreach (var f in SearchTarget(DarkElixirStorage.Find(CacheBehavior.ForceScan)?.FirstOrDefault()))
                    yield return f;

                // If not found switch to townhall
                if (targetIsSet == false)
                {
                    foreach (var f in SearchTarget(TownHall.Find(CacheBehavior.ForceScan)))
                        yield return f;
                }
            }

            // If not found both switch to the core of the base
            if (targetIsSet == false)
            {
                AllInOnePushDeploy.Target = AllInOnePushDeploy.Core;
            }
        }

        /// <summary>
        /// Search for target 
        /// </summary>
        /// <param name="building">The target building</param>
        /// <returns></returns>
        public static IEnumerable<int> SearchTarget(Building building)
        {
            var target = building?.Location.GetCenter();

            // If didn't find the target search for it every 1 sec for more  times.
            if (target == null)
            {
                for (var i = 2; i <= 4; i++)
                {
                    Log.Warning($"[{AllInOnePushDeploy.AttackName}] Bot didn't find the target .. we will attemp search NO. {i}");

                    yield return 1000;
                    target = building?.Location.GetCenter();
                    if (target != null)
                    {
                        Log.Warning($"[{AllInOnePushDeploy.AttackName}] Found the target after {i} retries");
                        AllInOnePushDeploy.Target = (PointFT)target;
                        targetIsSet = true;
                        break;
                    }
                }
            }
            else
            {
                Log.Info($"[{AllInOnePushDeploy.AttackName}] Successful located the target");

                AllInOnePushDeploy.Target = (PointFT)target;
                targetIsSet = true;
            }
        }

        /// <summary>
        /// Set deploy points for troops and spells
        /// </summary>
        public static void SetDeployPoints()
        {
            // Top right side
            var topRight = new PointFT((float)GameGrid.DeployExtents.MaxX, (float)GameGrid.MaxY - 2);
            var rightTop = new PointFT((float)GameGrid.DeployExtents.MaxX, (float)GameGrid.MinY + 2);

            // Bottom right side
            var rightBottom = new PointFT((float)GameGrid.MaxX - 5, (float)GameGrid.DeployExtents.MinY);
            var bottomRight = new PointFT((float)GameGrid.MinX + 10, (float)GameGrid.DeployExtents.MinY);

            // Bottom left side
            // Move 8 tiles from bottom corner due to unitsbar.
            var bottomLeft = new PointFT((float)GameGrid.DeployExtents.MinX, (float)GameGrid.MinY + 8);
            var leftBottom = new PointFT((float)GameGrid.DeployExtents.MinX, (float)GameGrid.MaxY - 2);

            // Top Left side
            var leftTop = new PointFT((float)GameGrid.MinX + 2, (float)GameGrid.DeployExtents.MaxY);
            var topLeft = new PointFT((float)GameGrid.MaxX - 2, (float)GameGrid.DeployExtents.MaxY);

            var isJumpSpell = Deploy.GetTroops().ExtractOne(DeployId.Jump)?.Count > 0 ? true : false;
            

            if (AllInOnePushDeploy.Origin.X > AllInOnePushDeploy.Core.X)
            {
                Log.Info($"[{AllInOnePushDeploy.AttackName}] Attacking from the top right");

                AllInOnePushDeploy.AttackLine = new Tuple<PointFT, PointFT>(topRight, rightTop);

                var distance = Math.Abs(AllInOnePushDeploy.Origin.X) - Math.Abs(AllInOnePushDeploy.Target.X);
                var target = distance >= AllInOnePushDeploy.MinDistace ? AllInOnePushDeploy.Target : AllInOnePushDeploy.Core;

                var firstWall = GetFirstWallForJump(AllInOnePushDeploy.Origin.Y, "Y");
                AllInOnePushDeploy.FirstJumpPoint = new PointFT(firstWall.X - 2.75f, AllInOnePushDeploy.Core.Y);

                var maxX = isJumpSpell ? AllInOnePushDeploy.FirstJumpPoint.X - 5f : firstWall.X - 1.5f;
                var start = target.X + 4f;

                var earthQuakePoints = new List<PointFT> { new PointFT(AllInOnePushDeploy.Target.X + 6f, AllInOnePushDeploy.Core.Y) };
                var jumpPoints = new List<PointFT> { new PointFT(AllInOnePushDeploy.Target.X + 5.5f, AllInOnePushDeploy.Core.Y) };

                if (GetWallsInsideSpell(earthQuakePoints[0], 4f) < 8)
                {
                    while (maxX > start)
                    {
                        earthQuakePoints.Add(new PointFT(start, AllInOnePushDeploy.Core.Y));
                        jumpPoints.Add(new PointFT(start - 0.5f, AllInOnePushDeploy.Core.Y));
                        start += 0.25f;
                    }
                }

                AllInOnePushDeploy.EqPoint = earthQuakePoints.OrderByDescending(e => GetWallsInsideSpell(e)).FirstOrDefault();

                // Prevent overlaping EQ with jump
                if (isJumpSpell && AllInOnePushDeploy.FirstJumpPoint.X - AllInOnePushDeploy.EqPoint.X < 7f)
                {
                    AllInOnePushDeploy.EqPoint = new PointFT(AllInOnePushDeploy.FirstJumpPoint.X - 7f, AllInOnePushDeploy.FirstJumpPoint.Y);
                }
                AllInOnePushDeploy.SecondJumpPoint = new PointFT(AllInOnePushDeploy.EqPoint.X - 0.5f, AllInOnePushDeploy.EqPoint.Y);

                var shiftSpells = AllInOnePushDeploy.ShiftSpells;
                AllInOnePushDeploy.FirstRagePoint = new PointFT(AllInOnePushDeploy.Origin.X - 11f - shiftSpells, AllInOnePushDeploy.Core.Y);
                AllInOnePushDeploy.FirstHealPoint = new PointFT(AllInOnePushDeploy.Origin.X - 17f - shiftSpells, AllInOnePushDeploy.Core.Y);
                AllInOnePushDeploy.SecondRagePoint = new PointFT(AllInOnePushDeploy.Origin.X - 22f - shiftSpells, AllInOnePushDeploy.Core.Y);
                AllInOnePushDeploy.FirstHastePoint = new PointFT(AllInOnePushDeploy.Origin.X - 26f - shiftSpells, AllInOnePushDeploy.Core.Y);

                //try to find better funneling points
                var frac = 0.65f;

                AllInOnePushDeploy.FirstFunnellingPoint = new PointFT(AllInOnePushDeploy.Origin.X + frac *
                    (AllInOnePushDeploy.AttackLine.Item1.X - AllInOnePushDeploy.Origin.X),
                    AllInOnePushDeploy.Origin.Y + frac *
                    (AllInOnePushDeploy.AttackLine.Item1.Y - AllInOnePushDeploy.Origin.Y));

                AllInOnePushDeploy.SecondFunnellingPoint = new PointFT(AllInOnePushDeploy.Origin.X + frac *
                    (AllInOnePushDeploy.AttackLine.Item2.X - AllInOnePushDeploy.Origin.X),
                    AllInOnePushDeploy.Origin.Y + frac *
                    (AllInOnePushDeploy.AttackLine.Item2.Y - AllInOnePushDeploy.Origin.Y));

                AllInOnePushDeploy.FirstHasteLine = new Tuple<PointFT, PointFT>
                (
                    new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X - 11f - shiftSpells, AllInOnePushDeploy.FirstFunnellingPoint.Y),
                    new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X - 11f - shiftSpells, AllInOnePushDeploy.SecondFunnellingPoint.Y)
                );
                AllInOnePushDeploy.FirstRageLine = new Tuple<PointFT, PointFT>
                (
                    new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X - 19f - shiftSpells, AllInOnePushDeploy.FirstFunnellingPoint.Y),
                    new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X - 19f - shiftSpells, AllInOnePushDeploy.SecondFunnellingPoint.Y)
                );

                AllInOnePushDeploy.SecondHasteLine = new Tuple<PointFT, PointFT>
                (
                    new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X - 24f - shiftSpells, AllInOnePushDeploy.FirstFunnellingPoint.Y),
                    new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X - 24f - shiftSpells, AllInOnePushDeploy.SecondFunnellingPoint.Y)
                );
                AllInOnePushDeploy.SecondRageLine = new Tuple<PointFT, PointFT>
                (
                    new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X - 24f - shiftSpells, AllInOnePushDeploy.FirstFunnellingPoint.Y),
                    new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X - 24f - shiftSpells, AllInOnePushDeploy.SecondFunnellingPoint.Y)
                );

                AllInOnePushDeploy.QWHealer = new PointFT(GameGrid.DeployExtents.MaxX, AllInOnePushDeploy.FirstFunnellingPoint.Y);
                AllInOnePushDeploy.QWRagePoint = new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X - 3, AllInOnePushDeploy.FirstFunnellingPoint.Y - 1);
                AllInOnePushDeploy.SecondFunnellingRagePoint = new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X - 3, AllInOnePushDeploy.SecondFunnellingPoint.Y + 1);
            }

            else if (AllInOnePushDeploy.Origin.X < AllInOnePushDeploy.Core.X)
            {
                Log.Info($"[{AllInOnePushDeploy.AttackName}] Attacking from the bottom left");

                AllInOnePushDeploy.AttackLine = new Tuple<PointFT, PointFT>(leftBottom, bottomLeft);

                var distance = Math.Abs(AllInOnePushDeploy.Origin.X) - Math.Abs(AllInOnePushDeploy.Target.X);
                var target = distance >= AllInOnePushDeploy.MinDistace ? AllInOnePushDeploy.Target : AllInOnePushDeploy.Core;

                var firstWall = GetFirstWallForJump(AllInOnePushDeploy.Origin.Y, "Y");
                AllInOnePushDeploy.FirstJumpPoint = new PointFT(firstWall.X + 2.75f, AllInOnePushDeploy.Core.Y);

                var maxX = isJumpSpell ? AllInOnePushDeploy.FirstJumpPoint.X + 5f : firstWall.X + 1.5f ;
                var start = target.X - 4f;

                var earthQuakePoints = new List<PointFT> { new PointFT(AllInOnePushDeploy.Target.X - 6f, AllInOnePushDeploy.Core.Y) };
                var jumpPoints = new List<PointFT> { new PointFT(AllInOnePushDeploy.Target.X - 5.5f, AllInOnePushDeploy.Core.Y) };

                if (GetWallsInsideSpell(earthQuakePoints[0], 4f) < 8)
                {
                    while (maxX < start)
                    {
                        earthQuakePoints.Add(new PointFT(start, AllInOnePushDeploy.Core.Y));
                        jumpPoints.Add(new PointFT(start + 0.5f, AllInOnePushDeploy.Core.Y));
                        start -= 0.25f;
                    }
                }

                AllInOnePushDeploy.EqPoint = earthQuakePoints.OrderByDescending(e => GetWallsInsideSpell(e)).FirstOrDefault();

                // Prevent overlaping EQ with jump
                if (isJumpSpell && Math.Abs(AllInOnePushDeploy.FirstJumpPoint.X - AllInOnePushDeploy.EqPoint.X) < 7f) 
                {
                    AllInOnePushDeploy.EqPoint = new PointFT(AllInOnePushDeploy.FirstJumpPoint.X + 7f, AllInOnePushDeploy.FirstJumpPoint.Y);
                }

                AllInOnePushDeploy.SecondJumpPoint = new PointFT(AllInOnePushDeploy.EqPoint.X + 0.5f, AllInOnePushDeploy.EqPoint.Y);

                var shiftSpells = AllInOnePushDeploy.ShiftSpells;
                AllInOnePushDeploy.FirstRagePoint = new PointFT(AllInOnePushDeploy.Origin.X + 11f + shiftSpells, AllInOnePushDeploy.Core.Y);
                AllInOnePushDeploy.FirstHealPoint = new PointFT(AllInOnePushDeploy.Origin.X + 17f + shiftSpells, AllInOnePushDeploy.Core.Y);
                AllInOnePushDeploy.SecondRagePoint = new PointFT(AllInOnePushDeploy.Origin.X + 22f + shiftSpells, AllInOnePushDeploy.Core.Y);
                AllInOnePushDeploy.FirstHastePoint = new PointFT(AllInOnePushDeploy.Origin.X + 26f + shiftSpells, AllInOnePushDeploy.Core.Y);

                //try to find better funneling points
                var frac = 0.65f;

                AllInOnePushDeploy.FirstFunnellingPoint = new PointFT(AllInOnePushDeploy.Origin.X + frac *
                    (AllInOnePushDeploy.AttackLine.Item1.X - AllInOnePushDeploy.Origin.X),
                    AllInOnePushDeploy.Origin.Y + frac *
                    (AllInOnePushDeploy.AttackLine.Item1.Y - AllInOnePushDeploy.Origin.Y));

                AllInOnePushDeploy.SecondFunnellingPoint = bottomLeft;

                AllInOnePushDeploy.FirstHasteLine = new Tuple<PointFT, PointFT>
                (
                    new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X + 11f + shiftSpells, AllInOnePushDeploy.FirstFunnellingPoint.Y),
                    new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X + 11f + shiftSpells, AllInOnePushDeploy.SecondFunnellingPoint.Y)
                );

                AllInOnePushDeploy.FirstRageLine = new Tuple<PointFT, PointFT>
                (
                    new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X + 19f + shiftSpells, AllInOnePushDeploy.FirstFunnellingPoint.Y),
                    new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X + 19f + shiftSpells, AllInOnePushDeploy.SecondFunnellingPoint.Y)
                );

                AllInOnePushDeploy.SecondHasteLine = new Tuple<PointFT, PointFT>
                (
                    new PointFT (AllInOnePushDeploy.FirstFunnellingPoint.X + 24f + shiftSpells, AllInOnePushDeploy.FirstFunnellingPoint.Y),
                    new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X + 24f + shiftSpells, AllInOnePushDeploy.SecondFunnellingPoint.Y)
                );

                AllInOnePushDeploy.SecondRageLine = new Tuple<PointFT, PointFT>
                (
                    new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X + 24f + shiftSpells, AllInOnePushDeploy.FirstFunnellingPoint.Y),
                    new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X + 24f + shiftSpells, AllInOnePushDeploy.SecondFunnellingPoint.Y)
                );

                AllInOnePushDeploy.QWHealer = new PointFT(GameGrid.DeployExtents.MinX, AllInOnePushDeploy.FirstFunnellingPoint.Y);
                AllInOnePushDeploy.QWRagePoint = new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X + 3, AllInOnePushDeploy.FirstFunnellingPoint.Y - 1);
                AllInOnePushDeploy.SecondFunnellingRagePoint = new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X + 3, AllInOnePushDeploy.SecondFunnellingPoint.Y + 1);
            }

            else if (AllInOnePushDeploy.Origin.Y > AllInOnePushDeploy.Core.Y)
            {
                Log.Info($"[{AllInOnePushDeploy.AttackName}] Attacking from the top left");

                AllInOnePushDeploy.AttackLine = new Tuple<PointFT, PointFT>(leftTop, topLeft);

                var distance = Math.Abs(AllInOnePushDeploy.Origin.Y) - Math.Abs(AllInOnePushDeploy.Target.Y);
                var target = distance >= AllInOnePushDeploy.MinDistace ? AllInOnePushDeploy.Target : AllInOnePushDeploy.Core;

                var firstWall = GetFirstWallForJump(AllInOnePushDeploy.Origin.X, "X");
                AllInOnePushDeploy.FirstJumpPoint = new PointFT(AllInOnePushDeploy.Core.X, firstWall.Y - 2.75f);

                var maxX = isJumpSpell ? AllInOnePushDeploy.FirstJumpPoint.Y - 5f : firstWall.Y - 1.5f;
                var start = target.Y + 4f;

                var earthQuakePoints = new List<PointFT> { new PointFT(AllInOnePushDeploy.Core.X, AllInOnePushDeploy.Target.Y + 6f) };
                var jumpPoints = new List<PointFT> { new PointFT(AllInOnePushDeploy.Core.X, AllInOnePushDeploy.Target.Y + 5.5f) };

                if (GetWallsInsideSpell(earthQuakePoints[0], 4f) < 8)
                {
                    while (maxX > start)
                    {
                        earthQuakePoints.Add(new PointFT(AllInOnePushDeploy.Core.X, start));
                        jumpPoints.Add(new PointFT(AllInOnePushDeploy.Core.X, start - 0.5f));
                        start += 0.25f;
                    }
                }

                AllInOnePushDeploy.EqPoint = earthQuakePoints.OrderByDescending(e => GetWallsInsideSpell(e)).FirstOrDefault();

                // Prevent overlaping EQ with jump
                if (isJumpSpell && AllInOnePushDeploy.FirstJumpPoint.Y - AllInOnePushDeploy.EqPoint.Y < 7f)
                {
                    AllInOnePushDeploy.EqPoint = new PointFT(AllInOnePushDeploy.FirstJumpPoint.X, AllInOnePushDeploy.FirstJumpPoint.Y - 7f);
                }
                AllInOnePushDeploy.SecondJumpPoint = new PointFT(AllInOnePushDeploy.Core.X, AllInOnePushDeploy.EqPoint.Y - 0.5f);

                var shiftSpells = AllInOnePushDeploy.ShiftSpells;
                AllInOnePushDeploy.FirstRagePoint = new PointFT(AllInOnePushDeploy.Core.X, AllInOnePushDeploy.Origin.Y - 11f - shiftSpells);
                AllInOnePushDeploy.FirstHealPoint = new PointFT(AllInOnePushDeploy.Core.X, AllInOnePushDeploy.Origin.Y - 17f - shiftSpells);
                AllInOnePushDeploy.SecondRagePoint = new PointFT(AllInOnePushDeploy.Core.X, AllInOnePushDeploy.Origin.Y - 22f - shiftSpells);
                AllInOnePushDeploy.FirstHastePoint = new PointFT(AllInOnePushDeploy.Core.X, AllInOnePushDeploy.Origin.Y - 26f - shiftSpells);

                //try to find better funneling points
                var frac = 0.65f;

                AllInOnePushDeploy.FirstFunnellingPoint = new PointFT(AllInOnePushDeploy.Origin.X + frac *
                    (AllInOnePushDeploy.AttackLine.Item1.X - AllInOnePushDeploy.Origin.X),
                    AllInOnePushDeploy.Origin.Y + frac *
                    (AllInOnePushDeploy.AttackLine.Item1.Y - AllInOnePushDeploy.Origin.Y));

                AllInOnePushDeploy.SecondFunnellingPoint = new PointFT(AllInOnePushDeploy.Origin.X + frac *
                    (AllInOnePushDeploy.AttackLine.Item2.X - AllInOnePushDeploy.Origin.X),
                    AllInOnePushDeploy.Origin.Y + frac *
                    (AllInOnePushDeploy.AttackLine.Item2.Y - AllInOnePushDeploy.Origin.Y));

                AllInOnePushDeploy.FirstHasteLine = new Tuple<PointFT, PointFT>
                (
                    new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X, AllInOnePushDeploy.FirstFunnellingPoint.Y - 11f - shiftSpells),
                    new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X, AllInOnePushDeploy.SecondFunnellingPoint.Y - 11f - shiftSpells)
                );

                AllInOnePushDeploy.FirstRageLine = new Tuple<PointFT, PointFT>
                (
                    new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X, AllInOnePushDeploy.FirstFunnellingPoint.Y - 19f - shiftSpells),
                    new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X, AllInOnePushDeploy.SecondFunnellingPoint.Y - 19f - shiftSpells)
                );

                AllInOnePushDeploy.SecondHasteLine = new Tuple<PointFT, PointFT>
                (
                    new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X, AllInOnePushDeploy.FirstFunnellingPoint.Y - 24f - shiftSpells),
                    new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X, AllInOnePushDeploy.SecondFunnellingPoint.Y - 24f - shiftSpells)
                );

                AllInOnePushDeploy.SecondRageLine = new Tuple<PointFT, PointFT>
                (
                    new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X, AllInOnePushDeploy.FirstFunnellingPoint.Y - 24f - shiftSpells),
                    new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X, AllInOnePushDeploy.SecondFunnellingPoint.Y - 24f - shiftSpells)
                );

                AllInOnePushDeploy.QWHealer = new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X, GameGrid.DeployExtents.MaxY);
                AllInOnePushDeploy.QWRagePoint = new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X + 1, AllInOnePushDeploy.FirstFunnellingPoint.Y - 3);
                AllInOnePushDeploy.SecondFunnellingRagePoint = new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X - 1, AllInOnePushDeploy.SecondFunnellingPoint.Y - 3);
            }

            else // (orgin.Y < core.Y)
            {
                Log.Info($"[{AllInOnePushDeploy.AttackName}] Attacking from the bottom right");

                // Avoid bottom right side until fix zoom out on attack progress issue
                var avoidBottomRight = true;
                if (avoidBottomRight)
                {
                    var originPoints = new[]
                    {
                        new PointFT(GameGrid.DeployExtents.MaxX, AllInOnePushDeploy.Core.Y),
                        new PointFT(GameGrid.DeployExtents.MinX, AllInOnePushDeploy.Core.Y),
                        new PointFT(AllInOnePushDeploy.Core.X, GameGrid.DeployExtents.MaxY),
                        new PointFT(AllInOnePushDeploy.Core.X, GameGrid.DeployExtents.MinY)
                    };
                    AllInOnePushDeploy.Origin = originPoints.OrderBy(point => point.DistanceSq(AllInOnePushDeploy.Target)).ElementAt(1);
                    Log.Warning($"Avoid bottom right side set to true, We will attack from next closest side to the target");
                    
                    SetDeployPoints();
                }
                else
                {
                    AllInOnePushDeploy.AttackLine = new Tuple<PointFT, PointFT>(rightBottom, bottomRight);

                    var distance = Math.Abs(AllInOnePushDeploy.Origin.Y) - Math.Abs(AllInOnePushDeploy.Target.Y);
                    var target = distance >= AllInOnePushDeploy.MinDistace ? AllInOnePushDeploy.Target : AllInOnePushDeploy.Core;

                    var firstWall = GetFirstWallForJump(AllInOnePushDeploy.Origin.X, "X");
                    AllInOnePushDeploy.FirstJumpPoint = new PointFT(AllInOnePushDeploy.Core.X, firstWall.Y + 2.75f);

                    var maxX = isJumpSpell ? AllInOnePushDeploy.FirstJumpPoint.Y + 5f : firstWall.Y + 1.5f;
                    var start = target.Y - 4f;

                    var earthQuakePoints = new List<PointFT> { new PointFT(AllInOnePushDeploy.Core.X, AllInOnePushDeploy.Target.Y - 6f) };
                    var jumpPoints = new List<PointFT> { new PointFT(AllInOnePushDeploy.Core.X, AllInOnePushDeploy.Target.Y - 5.5f) };

                    if (GetWallsInsideSpell(earthQuakePoints[0], 4f) < 8)
                    {
                        while (maxX < start)
                        {
                            earthQuakePoints.Add(new PointFT(AllInOnePushDeploy.Core.X, start));
                            jumpPoints.Add(new PointFT(AllInOnePushDeploy.Core.X, start + 0.5f));
                            start -= 0.25f;
                        }
                    }

                    AllInOnePushDeploy.EqPoint = earthQuakePoints.OrderByDescending(e => GetWallsInsideSpell(e)).FirstOrDefault();

                    // Prevent overlaping EQ with jump
                    if (isJumpSpell && Math.Abs(AllInOnePushDeploy.FirstJumpPoint.Y - AllInOnePushDeploy.EqPoint.Y) < 7f)
                    {
                        AllInOnePushDeploy.EqPoint = new PointFT(AllInOnePushDeploy.FirstJumpPoint.X, AllInOnePushDeploy.FirstJumpPoint.Y + 7f);
                    }
                    AllInOnePushDeploy.SecondJumpPoint = new PointFT(AllInOnePushDeploy.EqPoint.X, AllInOnePushDeploy.EqPoint.Y + 0.5f);

                    var shiftSpells = AllInOnePushDeploy.ShiftSpells;
                    AllInOnePushDeploy.FirstRagePoint = new PointFT(AllInOnePushDeploy.Core.X, AllInOnePushDeploy.Origin.Y + 11f + shiftSpells);
                    AllInOnePushDeploy.FirstHealPoint = new PointFT(AllInOnePushDeploy.Core.X, AllInOnePushDeploy.Origin.Y + 17f + shiftSpells);
                    AllInOnePushDeploy.SecondRagePoint = new PointFT(AllInOnePushDeploy.Core.X, AllInOnePushDeploy.Origin.Y + 22f + shiftSpells);
                    AllInOnePushDeploy.FirstHastePoint = new PointFT(AllInOnePushDeploy.Core.X, AllInOnePushDeploy.Origin.Y + 26f + shiftSpells);

                    //try to find better funneling points
                    var frac = 0.65f;

                    AllInOnePushDeploy.FirstFunnellingPoint = new PointFT(AllInOnePushDeploy.Origin.X + frac *
                        (AllInOnePushDeploy.AttackLine.Item1.X - AllInOnePushDeploy.Origin.X),
                        AllInOnePushDeploy.Origin.Y + frac *
                        (AllInOnePushDeploy.AttackLine.Item1.Y - AllInOnePushDeploy.Origin.Y));

                    AllInOnePushDeploy.SecondFunnellingPoint = bottomRight;

                    AllInOnePushDeploy.FirstHasteLine = new Tuple<PointFT, PointFT>
                    (
                        new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X, AllInOnePushDeploy.FirstFunnellingPoint.Y + 11f + shiftSpells),
                        new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X, AllInOnePushDeploy.SecondFunnellingPoint.Y + 11f + shiftSpells)
                    );

                    AllInOnePushDeploy.FirstRageLine = new Tuple<PointFT, PointFT>
                    (
                        new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X, AllInOnePushDeploy.FirstFunnellingPoint.Y + 19f + shiftSpells),
                        new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X, AllInOnePushDeploy.SecondFunnellingPoint.Y + 19f + shiftSpells)
                    );

                    AllInOnePushDeploy.SecondHasteLine = new Tuple<PointFT, PointFT>
                    (
                        new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X, AllInOnePushDeploy.FirstFunnellingPoint.Y + 24f + shiftSpells),
                        new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X, AllInOnePushDeploy.SecondFunnellingPoint.Y + 24f + shiftSpells)
                    );

                    AllInOnePushDeploy.SecondRageLine = new Tuple<PointFT, PointFT>
                    (
                        new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X, AllInOnePushDeploy.FirstFunnellingPoint.Y + 24f + shiftSpells),
                        new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X, AllInOnePushDeploy.SecondFunnellingPoint.Y + 24f + shiftSpells)
                    );

                    AllInOnePushDeploy.QWHealer = new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X, GameGrid.DeployExtents.MinY);
                    AllInOnePushDeploy.QWRagePoint = new PointFT(AllInOnePushDeploy.FirstFunnellingPoint.X - 1, AllInOnePushDeploy.FirstFunnellingPoint.Y + 3);
                    AllInOnePushDeploy.SecondFunnellingRagePoint = new PointFT(AllInOnePushDeploy.SecondFunnellingPoint.X + 1, AllInOnePushDeploy.SecondFunnellingPoint.Y + 3);
                }
            }
            AllInOnePushDeploy.SecondJumpPoint = AllInOnePushDeploy.EqPoint;
        }

        /// <summary>
        /// Read troops to check if it's air attack or not
        /// </summary>
        public static void IsAirAttack()
        {
            var deployElements = Deploy.GetTroops();

            var dragon = deployElements.ExtractOne(DeployId.Dragon);
            var babyDragon = deployElements.ExtractOne(DeployId.BabyDragon);
            var balloon = deployElements.ExtractOne(DeployId.Balloon);
            var Lava = deployElements.ExtractOne(DeployId.LavaHound);
            var minion = deployElements.ExtractOne(DeployId.Minion);

            AllInOnePushDeploy.IsAirAttack = (Lava?.Count > 0 || balloon?.Count > 5 || dragon?.Count > 5 || babyDragon?.Count > 5 || minion?.Count >= 10) ? true : false;
        }

        public static void DebugEQpells()
        {
            using (var bmp = Screenshot.Capture())
            {
                using (var g = Graphics.FromImage(bmp))
                {

                    Visualize.RectangleT(bmp, new RectangleT((int)AllInOnePushDeploy.FirstFunnellingPoint.X, (int)AllInOnePushDeploy.FirstFunnellingPoint.Y, 1, 1), new Pen(Color.Blue));
                    Visualize.RectangleT(bmp, new RectangleT((int)AllInOnePushDeploy.SecondFunnellingPoint.X, (int)AllInOnePushDeploy.SecondFunnellingPoint.Y, 1, 1), new Pen(Color.Blue));

                    Visualize.RectangleT(bmp, new RectangleT((int)AllInOnePushDeploy.Origin.X, (int)AllInOnePushDeploy.Origin.Y, 1, 1), new Pen(Color.Red));


                    //draw rectangle around the target
                    Visualize.RectangleT(bmp, new RectangleT((int)AllInOnePushDeploy.Target.X, (int)AllInOnePushDeploy.Target.Y, 3, 3), new Pen(Color.Blue));

                    Visualize.CircleT(bmp, AllInOnePushDeploy.FirstJumpPoint, 3.5f, Color.DarkGreen, 100, 0);


                    Visualize.CircleT(bmp, AllInOnePushDeploy.EqPoint, 4, Color.SandyBrown, 100, 0);
                }
                var d = DateTime.UtcNow;
                Screenshot.Save(bmp, $"{AllInOnePushDeploy.AttackName} EQ Spells {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}");
            }
        }

        public static void DebugSpells()
        {
            using (var bmp = Screenshot.Capture())
            {
                using (var g = Graphics.FromImage(bmp))
                {

                    g.DrawLine(new Pen(Color.FromArgb(192, Color.Orange)), AllInOnePushDeploy.FirstHasteLine.Item1.ToScreenAbsolute(), AllInOnePushDeploy.FirstHasteLine.Item2.ToScreenAbsolute());
                    g.DrawLine(new Pen(Color.FromArgb(192, Color.Orange)), AllInOnePushDeploy.FirstRageLine.Item1.ToScreenAbsolute(), AllInOnePushDeploy.FirstRageLine.Item2.ToScreenAbsolute());
                    g.DrawLine(new Pen(Color.FromArgb(192, Color.Orange)), AllInOnePushDeploy.SecondHasteLine.Item1.ToScreenAbsolute(), AllInOnePushDeploy.SecondHasteLine.Item2.ToScreenAbsolute());

              
                    Visualize.CircleT(bmp, AllInOnePushDeploy.FirstRagePoint, 5, Color.Magenta, 64, 0);
                    Visualize.CircleT(bmp, AllInOnePushDeploy.SecondRagePoint, 5, Color.Magenta, 64, 0);
                    Visualize.CircleT(bmp, AllInOnePushDeploy.FirstHealPoint, 5, Color.Yellow, 64, 0);
                    Visualize.CircleT(bmp, AllInOnePushDeploy.FirstHastePoint, 5, Color.OrangeRed, 64, 0);
                }
                var d = DateTime.UtcNow;
                Screenshot.Save(bmp, $"{AllInOnePushDeploy.AttackName} Spells {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}");
            }
        }
        
        public static void DebugBottomRightSidePoints()
        {
            using (var bmp = Screenshot.Capture())
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    var y = GameGrid.DeployExtents.MinY;
                    var x = 18;
                    while (x >= -15)
                    {
                        Visualize.RectangleT(bmp, new RectangleT(x, y, 1, 1), new Pen(Color.Blue));
                        x--;
                    }
                }
                var d = DateTime.UtcNow;
                Screenshot.Save(bmp, $"BottomRightPoints {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}");
            }
        }
    }
}
