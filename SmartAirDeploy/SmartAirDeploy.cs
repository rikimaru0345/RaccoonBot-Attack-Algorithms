using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoC_Bot;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using System.Drawing;
using CoC_Bot.Internals;
using SmartAirDeploy.Visuals;
using Shared;
using System.Windows;

using Point = System.Drawing.Point;

[assembly: Addon("SmartAirDeploy", "Addon for Air Attacks", "Inkredible")]

namespace SmartAirDeploy
{
    [AttackAlgorithm("ZapQuake AirAttack", "ZapQuake Airdefenses then start an AirAttack")]
    public class SmartAirDeploy : BaseAttack
    {
        public SmartAirDeploy(Opponent opponent) : base(opponent)
        {
        }

        public override string ToString()
        {
            return "Smart Air Deploy";
        }

        public override double ShouldAccept()
        {
            if (Opponent.MeetsRequirements(BaseRequirements.All))
            {
                if (!SmartAirDeployHelpers.AllAirDefensesFound(TownHall.Find() == null ? TownHallLimits.Limits.Length - 1 : TownHall.Find().Level))
                    return 0;

                return 1;
            }
            return 0;
        }

        bool debugMode = false;

        public override IEnumerable<int> AttackRoutine()
        {
#if DEBUG
            debugMode = true;
#endif
            var visuals = new List<VisualObject>();

            // get a list of all deployable units
            var deployElements = Deploy.GetTroops();
            Log.Debug("[Debug] Deployable Troops: " + ToUnitString(deployElements));
            if (!HasNeededTroops(deployElements))
            {
                Log.Error("[Smart Air] Couldn't find a known troop composition. Consider using one of the known troop compositions. Check our forums to learn more about " +
                            "the Smart Air Deploy in order to achieve the best possible results.");
                Surrender();
                yield break;
            }

            // extract heores into their own list
            var heroes = deployElements.Extract(u => u.IsHero);

            // extract clanCastle into its own list
            var clanCastle = deployElements.ExtractOne(u => u.ElementType == DeployElementType.ClanTroops);

            // extract spells into their own list
            var lightningSpells = deployElements.ExtractOne(x => x.Id == DeployId.Lightning);
            var earthQuakeSpells = deployElements.ExtractOne(x => x.Id == DeployId.Earthquake);
            var rageSpells = deployElements.ExtractOne(x => x.Id == DeployId.Rage);
            var freezeSpells = deployElements.ExtractOne(x => x.Id == DeployId.Freeze);


            #region ZapQuake AirDefenses if possible and rescan with a CheckForDestroyed
            // ZapQuake AirDefenses if required spells exist
            List<AirDefense> zapQuakeTargets = null;
            if (earthQuakeSpells?.Count > 0 && lightningSpells?.Count >= 2)
            {
                zapQuakeTargets = FindAirDefenseTargets(earthQuakeSpells, lightningSpells, visuals);

                if (!debugMode)
                {
                    if (zapQuakeTargets?.Count > 0)
                        foreach (var t in ZapQuakeAirDefenses(zapQuakeTargets, earthQuakeSpells, lightningSpells))
                            yield return t;
                    else
                        Log.Warning("[Smart Air] Couldn't find AirDefense targets for ZapQuaking!");
                }
            }
            else
                Log.Info("[Smart Air] Could not find enough spells (at least 1 Earthquake and 2 Lightning spells) to zapquake AirDefenses");

            // If we have zapquaked something we should rescan to check for the remaining AirDefenses
            if (zapQuakeTargets != null)
            {
                Log.Info("[Smart Air] Rescanning AirDefenses to check for remaining AirDefenses");
                AirDefense.Find(CacheBehavior.CheckForDestroyed);
            }
            #endregion

            var airDefenses = AirDefense.Find();
            var lavaLoonDeployPoints = CreateLavaHoundDeployPoints(airDefenses, visuals);

            if (debugMode)
                VisualizeDeployment(visuals);
        }

        List<PointFT> CreateLavaHoundDeployPoints(AirDefense[] airDefenses, List<VisualObject> visuals)
        {
            // don't include corners in case build huts are there
            var maxRedPointX = GameGrid.RedPoints.Where(p => -18 < p.Y && p.Y < 18)?.Max(point => point.X) + 1 ?? GameGrid.RedZoneExtents.MaxX;
            var minRedPointX = GameGrid.RedPoints.Where(p => -18 < p.Y && p.Y < 18)?.Min(point => point.X) - 1 ?? GameGrid.RedZoneExtents.MinX;
            var maxRedPointY = GameGrid.RedPoints.Where(p => -18 < p.X && p.X < 18)?.Max(point => point.Y) + 1 ?? GameGrid.RedZoneExtents.MaxY;
            var minRedPointY = GameGrid.RedPoints.Where(p => -18 < p.X && p.X < 18)?.Min(point => point.Y) - 1 ?? GameGrid.RedZoneExtents.MinY;

            // border around the base
            var baseBorder = new List<RectangleT>();
            baseBorder.Add(new RectangleT((int)minRedPointX, (int)maxRedPointY, (int)(maxRedPointX - minRedPointX), (int)(minRedPointY - maxRedPointY)));

            // Add baseborder as visual
            visuals.Add(new RectangleObject("BaseBorder", Color.FromArgb(100, Color.Maroon), baseBorder, new Pen(Color.Maroon)));

            // Find nearest deploy points for two airdefenses
            List<PointFT> resultPoints = new List<PointFT>();
            var closestAirDefenses = airDefenses.OrderBy(x => SmartAirDeployHelpers.DistanceSqToClosestDeploypoint(x.Location.GetCenter())).Take(2);

            if (closestAirDefenses?.Count() == 2)
            {
                var start = closestAirDefenses.First().Location.GetCenter();
                var end = closestAirDefenses.Last().Location.GetCenter();
                var airDefenseConnection = new Line(start, end);
                visuals.Add(new LinesObject("AirDefenseConnection", Color.FromArgb(180, Color.OrangeRed), new[] { airDefenseConnection }));


                Point screenStart = start.ToScreenAbsolute();
                Point screenEnd = end.ToScreenAbsolute();

                Vector vStart = new Vector(screenStart.X, screenStart.Y);
                Vector vEnd = new Vector(screenEnd.X, screenEnd.Y);

                Vector vStartEnd = vEnd - vStart;
                Vector vDir = vStartEnd;
                vDir.Normalize();

                Vector vOrthDir = new Vector(vDir.Y, -vDir.X);

                Vector vOrthMidPoint = vStart + vStartEnd * 0.5;

                Point midPoint = new Point((int)vOrthMidPoint.X, (int)vOrthMidPoint.Y);

                // We have two lines (mid -> airDef1) and (mid -> airDef2)
                // We want to know what line points "away" from the townhall (center of the map)
                var mapCenterAbsolute = new PointFT(0f, 0f).ToScreenAbsolute();
                Vector vMapCenter = new Vector(mapCenterAbsolute.X, mapCenterAbsolute.Y);


                Vector dirTest1 = vOrthMidPoint + vOrthDir * +0.1;
                Vector dirTest2 = vOrthMidPoint + vOrthDir * -0.1;

                Vector dirAwayFromCenter = (vMapCenter - dirTest1).LengthSquared < (vMapCenter - dirTest2).LengthSquared
                    ? -vOrthDir
                    : vOrthDir;

                Vector vAirDefOrthLineEnd = vOrthMidPoint + dirAwayFromCenter * 3000;
                Point airDefLineEndCorrect = new Point((int)vAirDefOrthLineEnd.X, (int)vAirDefOrthLineEnd.Y);

                var orthLine = new Line(new PointF((float)vOrthMidPoint.X, (float)vOrthMidPoint.Y), new PointF(airDefLineEndCorrect.X, airDefLineEndCorrect.Y));
                Log.Error($"[Smart Air] Start: ({orthLine.Start.X} , {orthLine.Start.Y}), End ({orthLine.End.X}, {orthLine.End.Y})");
                visuals.Add(new LinesObject("AirDefenseOrth", new Pen(Color.OrangeRed, 3), new[] { orthLine }));

                // Now we want to find the intersection of our line, with the outer rectangle.
                // We know that our line starts inside the rect, and ends outside of it, that means there must be exactly one intersection point
                
                // 1. convert all corners into vectors
                var left = new PointFT(minRedPointX, maxRedPointY).ToScreenAbsolute();
                var top = new PointFT(maxRedPointX, maxRedPointY).ToScreenAbsolute();
                var right = new PointFT(maxRedPointX, minRedPointY).ToScreenAbsolute();
                var bottom = new PointFT(minRedPointX, minRedPointY).ToScreenAbsolute();
                Vector vLeftPoint = new Vector(left.X, left.Y);
                Vector vTopPoint = new Vector(top.X, top.Y);
                Vector vRightPoint = new Vector(right.X, right.Y);
                Vector vBottomPoint = new Vector(bottom.X, bottom.Y);

                // 2. create the 4 combinations of the 4 points to get all 4 edges (topleft, topright, ...)
                var sidesToCheckForIntersections = new List<Tuple<Vector, Vector>>();
                sidesToCheckForIntersections.Add(Tuple.Create(vTopPoint, vLeftPoint)); // Topleft
                sidesToCheckForIntersections.Add(Tuple.Create(vTopPoint, vRightPoint)); // Topright
                sidesToCheckForIntersections.Add(Tuple.Create(vLeftPoint, vBottomPoint)); // BottomLeft
                sidesToCheckForIntersections.Add(Tuple.Create(vRightPoint, vBottomPoint)); // BottomRight

                // 3. use those for the following intersection calculation:
                Vector vOrthLineStartPoint = vOrthMidPoint;
                Vector vOrthLineEndPoint = vAirDefOrthLineEnd;
                Vector vOrth = vOrthLineStartPoint + vOrthLineEndPoint;

                // 4. test this line for intersections against all 4 sides, only (and exactly) one of them will have an intersection
                Vector vInterSectionPoint = default(Vector);
                foreach(var side in sidesToCheckForIntersections)
                {
                    if(SmartAirDeployHelpers.LineSegementsIntersect(side.Item1, side.Item2, vOrthLineStartPoint, vOrthLineEndPoint, out vInterSectionPoint))
                        break;
                }
                var intersectionPointF = new PointF((float)vInterSectionPoint.X, (float)vInterSectionPoint.Y);
                var intersectionPointFt = new PointFT((int)intersectionPointF.X, (int)intersectionPointF.Y);


                // 5. add that point to "visuals"
                visuals.Add(new PointsObject("IntersectionPoint", Color.FromArgb(200, Color.Cyan), new[] { intersectionPointF }));

                // deployPoints = all Points within radius x
                var deployPoints = GameGrid.RedPoints.Where(x => x.DistanceSq(intersectionPointFt) < 220).ToList();
                deployPoints = deployPoints.Select(x => x.TransformPositionAlongAxis(5)).ToList();

                // FurthestDeployPoint 1 = Furthest Point to intersectionPoint which is still inside of the radius
                var furthestDeployPoint1 = deployPoints.OrderByDescending(x => x.DistanceSq(intersectionPointFt)).First();
                // FurthestDeployPoint 2 = Furthest Point to intersectionPoint which is still inside of the radius
                var furthestDeployPoint2 = deployPoints.OrderByDescending(x => x.DistanceSq(furthestDeployPoint1)).First();

                var lavaHoundDeployPoints = new List<PointFT>();
                lavaHoundDeployPoints.Add(furthestDeployPoint1);
                lavaHoundDeployPoints.Add(furthestDeployPoint2);

                visuals.Add(new PointsObject("LavaHoundDeployPoints", Color.FromArgb(200, Color.Pink), lavaHoundDeployPoints));
                visuals.Add(new PointsObject("AttackUnitDeployPoints", Color.FromArgb(110, Color.Black), deployPoints));
            }

            return resultPoints;
        }

        void VisualizeDeployment(List<VisualObject> visuals)
        {
            using (var bmp = Screenshot.Capture())
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    foreach (var visual in visuals)
                        visual.Draw(g);
                }
                var d = DateTime.UtcNow;
                Screenshot.Save(bmp, $"Smart Air Deploy {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}");
            }
        }

        /// <summary>
        /// ZapQuake routine for destroying AirDefenses
        /// </summary>
        /// <param name="targets">AirDefense targets to destroy</param>
        /// <param name="earthQuakeSpells">Extracted Earthquake deployelement</param>
        /// <param name="lightningSpells">Extracted Lightning deployelement</param>
        /// <param name="waitUntilSpellAnimationIsOver">Bool if we should wait until the spell effect animation is completely gone</param>
        /// <returns></returns>
        IEnumerable<int> ZapQuakeAirDefenses(List<AirDefense> targets, DeployElement earthQuakeSpells, DeployElement lightningSpells, bool waitUntilSpellAnimationIsOver = true)
        {
            // Find targets until we have no more targets available
            for (var i = 0; i < targets.Count; i++)
            {
                Log.Info($"[Smart Air] Going to destroy Air Defense {i + 1}");

                // Drop one earthquake spell and two lightningspells each Air Defense
                Log.Info($"[Smart Air] Deploying Earthquake and Lightning spells for AirDefense {i + 1}");
                foreach (var t in Deploy.AtPoint(earthQuakeSpells, targets[i].Location.GetCenter()))
                    yield return t;

                foreach (var t in Deploy.AtPoint(lightningSpells, targets[i].Location.GetCenter(), 2))
                    yield return t;
            }
            // This should be prevent a CheckForDestroyed rescan while the animation is still going on (For example: http://i.imgur.com/SDPU5EG.jpg )
            if (waitUntilSpellAnimationIsOver)
            {
                Log.Debug("[Smart Air] Waiting for 10 seconds until the zapquake animation is completely gone");
                yield return 10 * 1000;
            }
        }

        /// <summary>
        /// Checks if the required algorithm troops have been built
        /// </summary>
        /// <param name="availableTroops">A list of all DeployElements</param>
        /// <returns>True: Required troops are available, False: Troopset doesn't match the expectations</returns>
        bool HasNeededTroops(List<DeployElement> availableTroops)
        {
            // Heroes (and maybe other special deployelements such as Clancastle) don't have unitdata
            var compositionHousingSpace = availableTroops.Where(x => x.UnitData != null).Sum(x => x.Count * x.UnitData.HousingSpace);

            // Count Lavahounds in Housing Space
            var lavahoundsHousingSpace = availableTroops.Extract(x => x.Id == DeployId.LavaHound).Sum(x => x.Count * x.UnitData.HousingSpace);

            // Count Balloons in Housing Space
            var balloonsHousingSpace = availableTroops.Extract(x => x.Id == DeployId.Balloon).Sum(x => x.Count * x.UnitData.HousingSpace);

            // Summed housing spaces for "LavaLoon" Composition and "LavaLoonion" Composition which are both popular compositions
            var lavaloonHousingSpace = lavahoundsHousingSpace + balloonsHousingSpace;

            if ((float)lavaloonHousingSpace / compositionHousingSpace >= 0.8)
                return true;

            // Else we should report what we have recognized so the user knows why it has failed
            Log.Debug($"[ZapQuake LavaLoon] Composition Housingspace: {compositionHousingSpace}");
            Log.Debug($"[ZapQuake LavaLoon] LavahoundsHousingSpace Housingspace: {lavahoundsHousingSpace}");
            Log.Debug($"[ZapQuake LavaLoon] BalloonsHousingSpace Housingspace: {balloonsHousingSpace}");
            Log.Debug($"[ZapQuake LavaLoon] LavaloonHousingSpace Housingspace: {balloonsHousingSpace}");

            return false;
        }

        /// <summary>
        /// Finds AirDefenses which can and should be ZapQuaked.
        /// The length of the list is dependent on the available spells (2 lightnings, 1 earthquake = 1 AirDefense target)
        /// </summary>
        /// <param name="earthQuakeSpells">The available earthquake spells</param>
        /// <param name="lightningSpells">The available lightning spells</param>
        /// <returns>A list of AirDefenses which can and should be ZapQuaked</returns>
        List<AirDefense> FindAirDefenseTargets(DeployElement earthQuakeSpells, DeployElement lightningSpells, List<VisualObject> visuals)
        {
            var lightningsToDestroyAirDefenses = (int)Math.Floor((double)lightningSpells.Count / 2);
            var destroyableAirDefenses = Math.Min(lightningsToDestroyAirDefenses, earthQuakeSpells.Count);
            Log.Info($"[Smart Air] We've got {lightningSpells.Count} Lightning Spells and {earthQuakeSpells.Count}, which is enough to destroy {destroyableAirDefenses} AirDefenses.");


            var allAirDefenses = AirDefense.Find();
            try
            {
                var targetsToFindCount = Math.Min(destroyableAirDefenses, allAirDefenses.Count());
                if (targetsToFindCount == 0)
                {
                    Log.Error("[Smart Air] FindAirDefenseTargets has been called even though it shouldn't have been called!");
                    return null;
                }

                // If we need to find 2 or more AirDefense targets we want to find the closest AirDefenses
                if (targetsToFindCount > 1)
                {
                    var airDefensesOrderedByDeployPointDistance = allAirDefenses.OrderByDescending(x => SmartAirDeployHelpers.DistanceSqToClosestDeploypoint(x.Location.GetCenter()));
                    // furthestAirDefense = The airdefense which is the furthest away from deployzone
                    var furthestAirDefense = airDefensesOrderedByDeployPointDistance.First();
                    var remainingAirDefenses = airDefensesOrderedByDeployPointDistance.Skip(1).ToList();
                    var orderedList = OrderByDistance(furthestAirDefense, remainingAirDefenses).Take(targetsToFindCount).ToList();

                    // Add visuals
                    var orderedListCenters = orderedList.Select(x => x.Location.GetCenter());
                    visuals.Add(new PointsObject("AirDefenseTargets", Color.FromArgb(200, Color.CornflowerBlue), orderedListCenters));

                    return orderedList;
                }
                var targetList = allAirDefenses.Take(1).ToList();

                // Add visuals
                var targetListCenters = targetList.Select(x => x.Location.GetCenter());
                visuals.Add(new PointsObject("AirDefenseTargets", Color.FromArgb(200, Color.CornflowerBlue), targetListCenters));

                return targetList;
            }
            catch (Exception ex)
            {
                Log.Error("[Smart Air] Exception occured during 'ZapQuakeAirDefenses'. More information can be found inside of the debug log.");
                Log.Debug("[Smart Air] Exception details: " + ex);
                return null;
            }
        }

        /// <summary>
        /// Returns an ordered list of AirDefenses sorted by distance ascending
        /// </summary>
        /// <param name="start">Initial AirDefense, fixed point for finding the nearest AirDefenses</param>
        /// <param name="remainingAirDefenses">List of the remaining AirDefenses</param>
        /// <returns></returns>
        List<AirDefense> OrderByDistance(AirDefense start, List<AirDefense> remainingAirDefenses)
        {
            var current = start;
            var remaining = remainingAirDefenses.ToList();
            ;
            var path = new List<AirDefense>();
            path.Add(start);

            while (remaining.Count != 0)
            {
                var next = Closest(current, remaining);
                path.Add(next);
                remaining.Remove(next);
                current = next;
            }
            return path;
        }

        /// <summary>
        /// Finds the closest AirDefense of a given source and given List of AirDefenses to check for
        /// </summary>
        /// <param name="current">Source AirDefense</param>
        /// <param name="remaining">List of AirDefenses to check shortest distance to</param>
        /// <returns>The AirDefense which is the closest to the source AirDefense</returns>
        AirDefense Closest(AirDefense current, List<AirDefense> remaining)
        {
            var closestAirDefense = remaining.FirstOrDefault();
            var lowestDistance = float.MaxValue;

            foreach (var airDefense in remaining)
            {
                var distanceSq = current.Location.GetCenter().DistanceSq(airDefense.Location.GetCenter());
                if (distanceSq < lowestDistance)
                {
                    closestAirDefense = airDefense;
                    lowestDistance = distanceSq;
                }
            }

            return closestAirDefense;
        }
    }
}
