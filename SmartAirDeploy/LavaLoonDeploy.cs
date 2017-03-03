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
using Shared;
using System.Windows;
using LavaLoonDeploy.Visuals;
using Point = System.Drawing.Point;

[assembly: Addon("LavaLoonDeploy", "Addon for LavaLoon attacks", "Seation")]

namespace LavaLoonDeploy
{
    [AttackAlgorithm("Lavaloon", "ZapQuake Airdefenses then start an AirAttack")]
    public class LavaLoonDeploy : BaseAttack
    {
        public LavaLoonDeploy(Opponent opponent) : base(opponent)
        {
        }

        public override string ToString()
        {
            return "LavaLoon Deploy";
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
            // user's wave delay setting
            var waveDelay = (int)(UserSettings.WaveDelay * 1000);

			// Call it once in order to cache the RedPoints in the beginning
			// If this will be called after spell deployment the redline will disappear!! - Do not remove this!
			var redPoints = GameGrid.RedPoints;

			// get a list of all deployable units
			var deployElements = Deploy.GetTroops();
            Log.Debug("[Debug] Deployable Troops: " + ToUnitString(deployElements));
            if (!HasNeededTroops(deployElements))
            {
                Log.Error("[LavaLoon] Couldn't find a known troop composition. Consider using one of the known troop compositions. Check our forums to learn more about " +
                            "the LavaLoon Deploy in order to achieve the best possible results.");
                Surrender();
                yield break;
            }

			// extract heores into their own list
			var heroes = deployElements
				.Extract(u => (UserSettings.UseKing && u.ElementType == DeployElementType.HeroKing)
					|| (UserSettings.UseQueen && u.ElementType == DeployElementType.HeroQueen)
					|| (UserSettings.UseWarden && u.ElementType == DeployElementType.HeroWarden))
				.ToList();

			// extract clanCastle into its own list
			var clanCastle = deployElements.ExtractOne(u => u.ElementType == DeployElementType.ClanTroops && UserSettings.UseClanTroops);

            // extract spells into their own list
            var lightningSpells = deployElements.ExtractOne(x => x.Id == DeployId.Lightning);
            var earthQuakeSpells = deployElements.ExtractOne(x => x.Id == DeployId.Earthquake);
            var rageSpells = deployElements.ExtractOne(x => x.Id == DeployId.Rage);
            var freezeSpells = deployElements.ExtractOne(x => x.Id == DeployId.Freeze);

            // extract tank units into their own list
            var tanks = deployElements.Extract(AttackType.Tank).ToArray();

            // extract balloons into their own list
            var balloon = deployElements.ExtractOne(x => x.Id == DeployId.Balloon);

			// extract the attack units into their own list
	        var damageDealer = deployElements.Extract(AttackType.Damage).OrderByDescending(x => x.UnitData.HP).ToArray();

			// extract wallbreakers into their own list
			var wallBreakers = deployElements.ExtractOne(x => x.Id == DeployId.WallBreaker);

            #region ZapQuake AirDefenses if possible and rescan with a CheckForDestroyed
            // ZapQuake AirDefenses if required spells exist
            List<AirDefense> zapQuakeTargets = null;
            if (earthQuakeSpells?.Count > 0 && lightningSpells?.Count >= 2)
            {
                zapQuakeTargets = FindAirDefenseTargets(earthQuakeSpells, lightningSpells, visuals);

                if (zapQuakeTargets?.Count > 0)
                    foreach (var t in ZapQuakeAirDefenses(zapQuakeTargets, earthQuakeSpells, lightningSpells))
                        yield return t;
                else
                    Log.Warning("[LavaLoon] Couldn't find AirDefense targets for ZapQuaking!");
            }
            else
                Log.Info("[LavaLoon] Could not find enough spells (at least 1 Earthquake and 2 Lightning spells) to zapquake AirDefenses");

            // If we have zapquaked something we should rescan to check for the remaining AirDefenses
            if (zapQuakeTargets != null)
            {
                Log.Info("[LavaLoon] Rescanning AirDefenses to check for remaining AirDefenses");
                AirDefense.Find(CacheBehavior.CheckForDestroyed);
            }
            #endregion

            var airDefenses = AirDefense.Find();
            var deployInfo = PrepareDeployCalculation(airDefenses, visuals);
            var tankDeployPoints = CreateLavaHoundDeployPoints(deployInfo, visuals).ToArray();
	        var balloonDeployPoints = CreateBalloonDeployPoints(deployInfo, balloon, visuals).ToArray();
            var attackWaveDeployPoints = CreateAttackWaveDeployPoints(deployInfo, visuals).ToArray();
            var funnelCreatorDeployPoints = CreateFunnelCreatorDeployPoints(deployInfo, visuals).ToArray();
            var intersectionDeployPoint = CreateIntersectionDeployPoint(deployInfo, visuals).First();

            // deploy all tanks if available
            if (tanks != null)
            {
                foreach(var tank in tanks)
                {
                    var deployCount = tank.Count;
                    Log.Info($"[Breakthrough] Deploying {tank.PrettyName} x{deployCount}");

                    // Deploy all Tanks alternating to each tank deploypoint (e. g.: left - right, left - right, left)
                    while (tank?.Count > 0)
                    {
						var initialTankCount = tank.Count;

						foreach (var deployPoint in tankDeployPoints)
                            foreach (var t in Deploy.AtPoint(tank, deployPoint, 1))
                                yield return t;

						// Prevent an infinite loop if deploy point is inside of the redzone
						if (tank.Count != initialTankCount) continue;

						Log.Warning($"[LavaLoon] Couldn't deploy {tank.PrettyName}");
						break;
					}
                }
            }

            if(balloon != null)
            {
				Log.Info($"[LavaLoon] Deploying {balloon.PrettyName} x{balloon.Count}");

				while (balloon?.Count > 0)
	            {
		            var initialWallBreakersCount = balloon?.Count;
					foreach (var t in Deploy.AtPoints(balloon, balloonDeployPoints, 3, 50))
						yield return t;

					// Prevent an infinite loop if deploy point is inside of the redzone
					if (balloon.Count != initialWallBreakersCount)
					{
						yield return 1200;
						continue;
					}

					Log.Warning($"[LavaLoon] Couldn't deploy {wallBreakers.PrettyName}");
					break;
				}
            }

            if (clanCastle?.Count > 0)
            {
                Log.Info($"[LavaLoon] Deploying {clanCastle.PrettyName}");
                foreach (var t in Deploy.AtPoint(clanCastle, intersectionDeployPoint, waveDelay: waveDelay))
                    yield return t;
            }

			if(damageDealer.Any())
			{

				Log.Debug("[LavaLoon] 1500ms Delay before deploying the attack wave");
				yield return 1500;

				foreach(var troop in damageDealer)
				{
					Log.Info($"[LavaLoon] Deploying {troop.PrettyName} x{troop.Count}");
				}
				while (damageDealer.Sum(x => x.Count) > 0)
				{
					var initialTroopCount = damageDealer.Sum(x => x.Count);
					foreach (var t in Deploy.AtPoints(damageDealer, funnelCreatorDeployPoints))
						yield return t;

					// Prevent an infinite loop if deploy point is inside of the redzone
					if (damageDealer.Sum(x => x.Count) != initialTroopCount) continue;

					var remainingTroops = damageDealer.Where(x => x.Count > 0).ToList();
					foreach(var troop in remainingTroops)
						Log.Warning($"[LavaLoon] Couldn't deploy x{troop.Count} {troop.PrettyName}");
					break;
				}
			}

            if (heroes.Any())
            {
				Log.Debug("[LavaLoon] 1500ms Delay before deploying heroes");
				yield return 1500;

				var heroDeployPoint = intersectionDeployPoint.TransformPositionAlongAxis(4).Constrain();

                foreach (var hero in heroes.Where(u => u.Count > 0))
                {
                    Log.Info($"[LavaLoon] Deploying {hero.PrettyName}");
                    foreach (var t in Deploy.AtPoint(hero, heroDeployPoint))
                        yield return t;
                }

                Deploy.WatchHeroes(heroes, 7000);
            }

			if(wallBreakers?.Count > 0)
			{
				var wallBreakersDeployPoint = intersectionDeployPoint.TransformPositionAlongAxis(4).Constrain();

				Log.Info($"[LavaLoon] Deploying {wallBreakers.PrettyName} x{wallBreakers.Count}");

				while (wallBreakers?.Count > 0)
				{
					var initialWallBreakersCount = wallBreakers?.Count;

					// Deploy Wallbreakers in 3 unit stacks (which is enough to crush walls) and deploy further stacks with 1.2s delay
					// in order to avoid that all of them get destroyed by splash damages
					foreach (var t in Deploy.AtPoint(wallBreakers, wallBreakersDeployPoint, 3, waveDelay: waveDelay))
						yield return t;

					// Prevent an infinite loop if deploy point is inside of the redzone
					if (wallBreakers.Count != initialWallBreakersCount)
					{
						yield return 1200;
						continue;
					}

					Log.Warning($"[LavaLoon] Couldn't deploy {wallBreakers.PrettyName}");
					break;
				}
			}

            if (debugMode)
                VisualizeDeployment(visuals);
        }

		List<PointFT> CreateBalloonDeployPoints(DeployCalculationInformation deployInfo, DeployElement balloon, List<VisualObject> visuals)
		{
			var intersectionPointFt = deployInfo.IntersectionPointFT;
			var redlinePoints = GameGrid.RedPoints.Where(x => x.DistanceSq(intersectionPointFt) < 460).ToList();
			redlinePoints = redlinePoints.OrderBy(x => x.Angle).ToList();

			var deployCount = balloon.Count;
			var unitsPerStack = 3;
			var stackCount = deployCount / unitsPerStack; // We want to deploy balloons in 3 unit stacks
			Log.Debug($"[LavaLoon] We need to find {stackCount} deploypoints");

			var stepSize = redlinePoints.Count / stackCount;
			var stackDeployPoints = redlinePoints.Where((x, i) => i % stepSize == 0).ToList();
			var stackDeployPointsVisual = new PointsObject("balloons", Color.FromArgb(200, Color.NavajoWhite), stackDeployPoints, 1);
			visuals.Add(stackDeployPointsVisual);
			return stackDeployPoints;
		}

		/// <summary>
		/// Prepares some stuff (baseBorder, orth from AirDefenses, intersectionPoint) which helps to calculate the actual deploypoints.
		/// </summary>
		/// <param name="airDefenses">Found AirDefenses after possible ZapQuake</param>
		/// <param name="visuals">VisualObjects for drawing information (deploypoints, orth and so on) into bmp</param>
		/// <returns></returns>
		DeployCalculationInformation PrepareDeployCalculation(AirDefense[] airDefenses, List<VisualObject> visuals)
		{
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
            var airDefensesOrderedByDeployPointDistance = airDefenses.OrderByDescending(x => SmartAirDeployHelpers.DistanceSqToClosestDeploypoint(x.Location.GetCenter()));
            var closestAirDefense = airDefensesOrderedByDeployPointDistance.FirstOrDefault();
            var remainingAirDefenses = airDefensesOrderedByDeployPointDistance.Skip(1).ToList();
            var orderedList = OrderByDistance(closestAirDefense, remainingAirDefenses).Take(2).ToList();

            PointF intersectionPointF;
            PointFT intersectionPointFt;

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
            var baseBorderSides = new List<Tuple<Vector, Vector>>();
            baseBorderSides.Add(Tuple.Create(vTopPoint, vLeftPoint)); // Topleft
            baseBorderSides.Add(Tuple.Create(vTopPoint, vRightPoint)); // Topright
            baseBorderSides.Add(Tuple.Create(vLeftPoint, vBottomPoint)); // BottomLeft
            baseBorderSides.Add(Tuple.Create(vRightPoint, vBottomPoint)); // BottomRight

            if (orderedList?.Count() == 2)
            {
                var start = orderedList.First().Location.GetCenter();
                var end = orderedList.Last().Location.GetCenter();
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
                visuals.Add(new LinesObject("AirDefenseOrth", new Pen(Color.OrangeRed, 3), new[] { orthLine }));

                // Now we want to find the intersection of our line, with the outer rectangle.
                // We know that our line starts inside the rect, and ends outside of it, that means there must be exactly one intersection point



                // 3. use those for the following intersection calculation:
                Vector vOrthLineStartPoint = vOrthMidPoint;
                Vector vOrthLineEndPoint = vAirDefOrthLineEnd;
                Vector vOrth = vOrthLineStartPoint + vOrthLineEndPoint;

                // 4. test this line for intersections against all 4 sides, only (and exactly) one of them will have an intersection
                Vector vInterSectionPoint = default(Vector);
                foreach(var side in baseBorderSides)
                {
                    if(SmartAirDeployHelpers.LineSegementsIntersect(side.Item1, side.Item2, vOrthLineStartPoint, vOrthLineEndPoint, out vInterSectionPoint))
                        break;
                }
                intersectionPointF = new PointF((float)vInterSectionPoint.X, (float)vInterSectionPoint.Y);
                intersectionPointFt = new PointFT((int)intersectionPointF.X, (int)intersectionPointF.Y);
            }
            else if(orderedList?.Count == 1)
            {
                // It's not necessary to build an orth between two AirDefenses in that case, so just find the closest deploy point to the only left AirDefense
                var start = orderedList.First().Location.GetCenter();
                var deployPointsOrderedByDistance = GameGrid.RedPoints.OrderBy(x => x.DistanceSq(start));
                intersectionPointFt = deployPointsOrderedByDistance.First();
                intersectionPointF = intersectionPointFt.ToScreenAbsolute();
            }
            else
            {
                // No Airdefense left anymore? Then just use a random Redpoint
                intersectionPointFt = GameGrid.RedPoints.First();
                intersectionPointF = intersectionPointFt.ToScreenAbsolute();
            }

            // 5. add that point to "visuals"
            visuals.Add(new PointsObject("IntersectionPoint", Color.FromArgb(200, Color.Cyan), new[] { intersectionPointF }));

            // 6. Chop baseborder line into points so we can determine proper deploypoints
            var baseBorderPoints = new List<PointFT>();
            foreach(var side in baseBorderSides)
                baseBorderPoints.AddRange(SmartAirDeployHelpers.ChopLine(side.Item1, side.Item2));
            

            // deployPoints = all Points within radius x
            var deployPoints = baseBorderPoints.Where(x => x.DistanceSq(intersectionPointFt) < 450).ToList();
            deployPoints = deployPoints.Select(x => x.TransformPositionAlongAxis(1)).ToList();
            deployPoints = deployPoints.Select(x => x.Constrain()).ToList();

            return new DeployCalculationInformation(intersectionPointFt, intersectionPointF, deployPoints);
        }

        List<PointFT> CreateIntersectionDeployPoint(DeployCalculationInformation deployInfo, List<VisualObject> visuals)
        {
            var deployPoints = deployInfo.AllDeployPoints;
            var intersectionPointFt = deployInfo.IntersectionPointFT;
            return new List<PointFT> {intersectionPointFt};
        }

        List<PointFT> CreateFunnelCreatorDeployPoints(DeployCalculationInformation deployInfo, List<VisualObject> visuals)
        {
			var intersectionPointFt = deployInfo.IntersectionPointFT;
			var deployPoints = deployInfo.AllDeployPoints;
	        deployPoints = deployPoints.Where(x => x.DistanceSq(intersectionPointFt) < 240).ToList();
            var extremePoints = GetMostDistantPoints(deployPoints, intersectionPointFt);
			extremePoints = extremePoints.Select(x => x.TransformPositionAlongAxis(3).Constrain()).ToArray();
	        return extremePoints.ToList();
        }

		/// <summary>
		/// Calculates the most distant points inside of given List of PointFTs. Passing the midpoint of all points is required
		/// </summary>
		/// <param name="deployPoints">All allowed deploy points</param>
		/// <param name="midPoint">Midpoint of all deploypoints</param>
		/// <returns>The two most distant PointFTs</returns>
        PointFT[] GetMostDistantPoints(List<PointFT> deployPoints, PointFT midPoint)
        {
            var furthestPoint1 = deployPoints.OrderByDescending(x => x.DistanceSq(midPoint)).First();
            var furthestPoint2 = deployPoints.OrderByDescending(x => x.DistanceSq(furthestPoint1)).First();
	        return new PointFT[] {furthestPoint1, furthestPoint2};
        }

        List<PointFT> CreateLavaHoundDeployPoints(DeployCalculationInformation deployInfo, List<VisualObject> visuals)
        {
            var deployPoints = deployInfo.AllDeployPoints;
            var intersectionPointFt = deployInfo.IntersectionPointFT;

			// Don't use the most outer points for lavahounds, instead use a smaller radius
			deployPoints = deployPoints.Where(x => x.DistanceSq(intersectionPointFt) < 300).ToList();

			// Furthest Deploypoints are supposed to be the deployspots for any tanks
			var mostDistantPoints = GetMostDistantPoints(deployPoints, intersectionPointFt);
            var furthestDeployPoint1 = mostDistantPoints.First();
            var furthestDeployPoint2 = mostDistantPoints.Last();

            var lavaHoundDeployPoints = new List<PointFT>();
            lavaHoundDeployPoints.Add(furthestDeployPoint1);
            lavaHoundDeployPoints.Add(furthestDeployPoint2);

            visuals.Add(new PointsObject("LavaHoundDeployPoints", Color.FromArgb(200, Color.Pink), lavaHoundDeployPoints));

            return lavaHoundDeployPoints;
        }

        List<PointFT> CreateAttackWaveDeployPoints(DeployCalculationInformation deployInfo, List<VisualObject> visuals)
        {
            var deployPoints = deployInfo.AllDeployPoints;
            visuals.Add(new PointsObject("AttackUnitDeployPoints", Color.FromArgb(110, Color.Black), deployPoints));

            return deployPoints;
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
                Screenshot.Save(bmp, $"LavaLoon Deploy {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}");
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
                Log.Info($"[LavaLoon] Going to destroy Air Defense {i + 1}");

                // Drop one earthquake spell and two lightningspells each Air Defense
                Log.Info($"[LavaLoon] Deploying Earthquake and Lightning spells for AirDefense {i + 1}");
                foreach (var t in Deploy.AtPoint(earthQuakeSpells, targets[i].Location.GetCenter()))
                    yield return t;

                foreach (var t in Deploy.AtPoint(lightningSpells, targets[i].Location.GetCenter(), 2))
                    yield return t;
            }
            // This should be prevent a CheckForDestroyed rescan while the animation is still going on (For example: http://i.imgur.com/SDPU5EG.jpg )
            if (waitUntilSpellAnimationIsOver)
            {
                Log.Debug("[LavaLoon] Waiting for 10 seconds until the zapquake animation is completely gone");
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
			// Also remove Spells (Normal Units only)
            var compositionHousingSpace = availableTroops.Where(x => x.UnitData != null && x.ElementType == DeployElementType.NormalUnit).Sum(x => x.Count * x.UnitData.HousingSpace);

            // Count Lavahounds in Housing Space
            var airTroopsHousingSpace = availableTroops.Where(x => x.UnitData != null && x.UnitData?.UnitType == UnitType.Air).Sum(x => x.Count * x.UnitData.HousingSpace);

            if ((float)airTroopsHousingSpace / compositionHousingSpace >= 0.65)
                return true;

            // Else we should report what we have recognized so the user knows why it has failed
            Log.Debug($"[ZapQuake LavaLoon] Composition Housingspace: {compositionHousingSpace}");
            Log.Debug($"[ZapQuake LavaLoon] Air Troops Housingspace: {airTroopsHousingSpace}");

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
            Log.Info($"[LavaLoon] We've got {lightningSpells.Count} Lightning Spells and {earthQuakeSpells.Count}, which is enough to destroy {destroyableAirDefenses} AirDefenses.");


            var allAirDefenses = AirDefense.Find();
            try
            {
                var targetsToFindCount = Math.Min(destroyableAirDefenses, allAirDefenses.Count());
                if (targetsToFindCount == 0)
                {
                    Log.Error("[LavaLoon] FindAirDefenseTargets has been called even though it shouldn't have been called!");
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
                Log.Error("[LavaLoon] Exception occured during 'ZapQuakeAirDefenses'. More information can be found inside of the debug log.");
                Log.Debug("[LavaLoon] Exception details: " + ex);
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

	        var path = new List<AirDefense> {start};

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
