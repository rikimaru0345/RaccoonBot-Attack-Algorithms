using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using CoC_Bot.Internals;
using LavaLoonDeploy.Visuals;
using CoC_Bot;

namespace LavaLoonDeploy
{
	[AttackAlgorithm("BabyLoon", "Power DE farming with Baby Dragons and Balloons")]
	public class BabyLoonDeploy : BaseAttack
	{
		public BabyLoonDeploy(Opponent opponent) : base(opponent)
		{
		}

		public override string ToString()
		{
			return "BabyLoon Deploy";
		}

		bool debugMode = false;

		public override IEnumerable<int> AttackRoutine()
		{
#if DEBUG
            debugMode = true;
#endif
			var visuals = new List<VisualObject>();
			// Call it once in order to cache the RedPoints in the beginning
			// If this will be called after spell deployment the redline will disappear!! - Do not remove this!
			var redPoints = GameGrid.RedPoints;

			// get a list of all deployable units
			var deployElements = Deploy.GetTroops();
			Log.Info("[BabyLoon] Deployable Troops: " + ToUnitString(deployElements));
			if (!HasNeededTroops(deployElements))
			{
				Log.Error("[Smart Air] Couldn't find a known troop composition. Consider using one of the known troop compositions. Check our forums to learn more about " +
							"the Smart Air Deploy in order to achieve the best possible results.");
				Surrender();
				yield break;
			}

			// extract special units into their own list under respect of usersettings
			var heroes = deployElements
				.Extract(u => (UserSettings.UseKing && u.ElementType == DeployElementType.HeroKing)
					|| (UserSettings.UseQueen && u.ElementType == DeployElementType.HeroQueen)
					|| (UserSettings.UseWarden && u.ElementType == DeployElementType.HeroWarden))
				.ToList();

			var clanCastle = deployElements.ExtractOne(u => u.ElementType == DeployElementType.ClanTroops && UserSettings.UseClanTroops);

			// remove left special units so we won't use them accidentially
			deployElements.
				RemoveAll(u => u.ElementType == DeployElementType.ClanTroops
					|| u.ElementType == DeployElementType.HeroKing
					|| u.ElementType == DeployElementType.HeroQueen
					|| u.ElementType == DeployElementType.HeroWarden);

			// extract units into their own lists in order to use them for more specific behaviours
			var lightningSpell = deployElements.ExtractOne(x => x.Id == DeployId.Lightning);
			var earthQuakeSpell = deployElements.ExtractOne(x => x.Id == DeployId.Earthquake);
			var rageSpell = deployElements.ExtractOne(x => x.Id == DeployId.Rage);
			var freezeSpels = deployElements.ExtractOne(x => x.Id == DeployId.Freeze);
			var tanks = deployElements.Extract(AttackType.Tank).ToArray();
			var balloon = deployElements.ExtractOne(x => x.Id == DeployId.Balloon);
			var babyDragon = deployElements.ExtractOne(x => x.Id == DeployId.BabyDragon);
			var wallBreakers = deployElements.ExtractOne(x => x.Id == DeployId.WallBreaker);
			var damageDealers = deployElements.Extract(AttackType.Damage).OrderByDescending(x => x.UnitData.HP).ToArray();

			// 1. Find the redpoint which is the closest to the TownHall
			var townHall = TownHall.Find();
			var townHallPosition = townHall == null ? new PointFT(0, 0) : townHall.Location.GetCenter();
			var closestThRedPoint = GameGrid.RedPoints.OrderBy(x => x.DistanceSq(townHallPosition)).First();
			visuals.Add(new PointsObject("Closest TH Point", Color.FromArgb(200, Color.Cyan), new PointFT[] {closestThRedPoint}));

			// 2. Prepare DeployPoint Calculation
			var babyDragonSpread = babyDragon?.Count*0.031 ?? 0;
			var spreadFactor = 0.7 + babyDragonSpread;
			double minAngle = closestThRedPoint.Angle - spreadFactor;
			double maxAngle = closestThRedPoint.Angle + spreadFactor;
			double twoPi = 2 * Math.PI;
			double circleStart = -Math.PI;
			double circleEnd = Math.PI;
			if (minAngle < -Math.PI)
			{
				circleEnd = minAngle + twoPi;
				minAngle = -Math.PI;
			}
			else if (maxAngle > Math.PI)
			{
				circleStart = maxAngle - twoPi;
				maxAngle = Math.PI;
			}
			
			// 1. Condition: All Points between minAngle and MaxAngle
			// OR
			// 2. Condition: All Points between 0 and circleStart
			// OR
			// 3. Condition: All Points between circleEnd and twoPi
			var potentialDeployPoints = GameGrid.RedPoints.Where(x => x.Angle >= minAngle && x.Angle <= maxAngle
				|| x.Angle >= -Math.PI && x.Angle < circleStart
				|| x.Angle > circleEnd && x.Angle <= Math.PI).ToArray();
			potentialDeployPoints = OrderDeployPoints(potentialDeployPoints);
			visuals.Add(new PointsObject("Potential DeployPoints", Color.FromArgb(100, Color.Red), potentialDeployPoints, 1));

			// 3. If possible - zapquake 2nd closest AirDefense
			if (earthQuakeSpell?.Count >= 1 && lightningSpell?.Count >= 2)
			{
				var target = FindZapQuakeTarget(closestThRedPoint, visuals);
				if(target != null)
				{
					Log.Info("[BabyLoon] Going to ZapQuake second closest AirDefense from deployside");
					foreach (var t in ZapQuakeTarget(target.Location.GetCenter(), earthQuakeSpell, lightningSpell))
						yield return t;
				}
				else
					Log.Info("[BabyLoon] Couldn't find more than 1 AirDefense - hence skipping ZapQuake");
			}
			else
				Log.Debug("[BabyLoon] Not enough spells for ZapQuake available");


			// 4. Calculate all deployPoints
			var babyDragonDeployPoints = CalculateBabyDragonDeployPoints(babyDragon, potentialDeployPoints, visuals);
			var balloonDeployPoints = CalculateBalloonDeployPoints(balloon, potentialDeployPoints, visuals);
			var rageDeployPoints = CalculateRageDeployPoints(rageSpell, potentialDeployPoints, visuals);
			var tankDeployPoints = CalculateTankDeployPoints(tanks, potentialDeployPoints, visuals);
			// Deploy Clancastle 2 tiles away from the closestThRedPoint
			var clanCastleDeployPoint = closestThRedPoint.TransformPositionAlongAxis(2).Constrain();

			// 5. Deploy Tanks and ClanCastle in the center of the DeploySide
			if (tanks.Length > 0)
			{
				foreach (var tank in tanks)
				{
					Log.Info($"[BabyLoon] Deploying {tank.PrettyName} x{tank.Count}");
				}

				// If there is just one tank use closestThRedPoint as deploypoint
				if(tanks.Sum(x => x.Count) == 1)
				{
					while (tanks.Sum(x => x.Count) > 0)
					{
						var initialTroopCount = tanks.Sum(x => x.Count);
						foreach (var t in Deploy.AtPoint(tanks, closestThRedPoint))
							yield return t;

						// Prevent an infinite loop if deploy point is inside of the redzone
						if (tanks.Sum(x => x.Count) != initialTroopCount) continue;

						foreach (var tank in tanks)
							Log.Warning($"[BabyLoon] Couldn't deploy x{tank.Count} {tank.PrettyName}");
						break;
					}
				}

				// If there is more than one tank to deploy, deploy them along the attacking side
				while (tanks.Sum(x => x.Count) > 0)
				{
					var initialTroopCount = tanks.Sum(x => x.Count);
					foreach (var t in Deploy.AtPoints(tanks, tankDeployPoints))
						yield return t;

					// Prevent an infinite loop if deploy point is inside of the redzone
					if (tanks.Sum(x => x.Count) != initialTroopCount) continue;

					foreach (var tank in tanks)
						Log.Warning($"[BabyLoon] Couldn't deploy x{tank.Count} {tank.PrettyName}");
					break;
				}

			}

			// 6. Deploy BabyDragons
			if (babyDragon?.Count > 0)
			{
				Log.Info($"[BabyLoon] Deploying {babyDragon.PrettyName} x{babyDragon.Count}");

				while (babyDragon.Count > 0)
				{
					var initialBabyDragonCount = babyDragon.Count;
					foreach (var t in Deploy.AtPoints(babyDragon, babyDragonDeployPoints, 1, 50))
						yield return t;

					// Prevent an infinite loop if deploy points are inside of the redzone
					if (babyDragon.Count != initialBabyDragonCount)
						continue;

					Log.Warning($"[BabyLoon] Couldn't deploy {babyDragon.PrettyName}");
					break;
				}

				// Humanizing pause
				yield return 500;
			}

			// 7. Deploy balloons
			if (balloon?.Count > 0)
			{
				Log.Debug($"[BabyLoon] Wait 2 seconds before deploying {balloon.PrettyName} x{balloon.Count}!");
				yield return 2000;
				Log.Info($"[BabyLoon] Deploying {balloon.PrettyName} x{balloon.Count}");

				while (balloon.Count > 0)
				{
					var initialBalloonCount = balloon.Count;
					foreach (var t in Deploy.AtPoints(balloon, balloonDeployPoints, 1, 50))
						yield return t;

					// Prevent an infinite loop if deploy points are inside of the redzone
					if (balloon.Count != initialBalloonCount)
						continue;

					Log.Warning($"[BabyLoon] Couldn't deploy {balloon.PrettyName}");
					break;
				}
			}

			// 8. Deploy Ragespells
			if(rageSpell?.Count > 0)
			{
				Log.Info($"[BabyLoon] Deploying {rageSpell.PrettyName} x{rageSpell.Count}");

				while (rageSpell.Count > 0)
				{
					var initialRageSpellCount = rageSpell.Count;
					foreach (var t in Deploy.AtPoints(rageSpell, rageDeployPoints, 1, 50))
						yield return t;

					// Prevent an infinite loop if deploy points are inside of the redzone
					if (balloon.Count != initialRageSpellCount)
						continue;

					Log.Warning($"[BabyLoon] Couldn't deploy {rageSpell.PrettyName}");
					break;
				}
			}

			// 9. Deploy all other troops
			if(damageDealers.Length > 0)
			{
				foreach (var damagedealer in damageDealers)
				{
					Log.Info($"[BabyLoon] Deploying {damagedealer.PrettyName} x{damagedealer.Count}");
				}
				while (damageDealers.Sum(x => x.Count) > 0)
				{
					var initialTroopCount = damageDealers.Sum(x => x.Count);
					foreach (var t in Deploy.AtPoint(damageDealers, closestThRedPoint))
						yield return t;

					// Prevent an infinite loop if deploy point is inside of the redzone
					if (tanks.Sum(x => x.Count) != initialTroopCount) continue;

					foreach (var damagedealer in damageDealers)
						Log.Warning($"[BabyLoon] Couldn't deploy x{damagedealer.Count} {damagedealer.PrettyName}");
					break;
				}
			}

			// 10. Deploy Heroes and watch them to activate abilities
			if(heroes.Any())
			{
				var heroDeployPoints = GameGrid.RedPoints.OrderBy(x => x.DistanceSq(closestThRedPoint)).Take(10).ToArray();
				heroDeployPoints = heroDeployPoints.Select(x => x.TransformPositionAlongAxis(4).Constrain()).ToArray();

				foreach (var hero in heroes.Where(u => u.Count > 0))
				{
					Log.Info($"[BabyLoon] Deploying {hero.PrettyName}");
					foreach (var t in Deploy.AtPoints(hero, heroDeployPoints))
						yield return t;
				}

				Deploy.WatchHeroes(heroes, 7000);
			}

			// 11. Deploy Wallbreakers in 3 unit stacks near heroes
			while (wallBreakers?.Count > 0)
			{
				// Shift Heroes' deploypoint by 1 tile in both directions to prevent deployment on the same spot (wallbreakers may die due to splashdamage against heroes)
				var wallbreakerDeployPoints = GameGrid.RedPoints.OrderBy(x => x.DistanceSq(closestThRedPoint)).Take(15).ToArray();
				var count = wallBreakers.Count;

				Log.Info($"[BabyLoon] Deploying {wallBreakers.PrettyName} x3");
				foreach (var t in Deploy.AtPoints(wallBreakers, wallbreakerDeployPoints, 3))
					yield return t;

				// prevent infinite loop if deploy point is on red
				if (wallBreakers.Count != count) continue;

				Log.Warning($"[BabyLoon] Couldn't deploy {wallBreakers.PrettyName}");
				break;
			}

			if (clanCastle?.Count > 0)
			{
				Log.Info($"[BabyLoon] Deploying {clanCastle.PrettyName}");
				foreach (var t in Deploy.AtPoint(clanCastle, clanCastleDeployPoint))
					yield return t;
			}


			if (debugMode)
				VisualizeDeployment(visuals);
		}

		/// <summary>
		/// Sort DeployPoints by angle, but take care of the special case that the range might be between 
		/// -Math.PI and +Math.PI (e.g. Points have angles between -2.9 and +2.8)
		/// </summary>
		/// <param name="potentialDeployPoints">The list of all unordered deployPoints</param>
		/// <returns>An ordered array</returns>
		PointFT[] OrderDeployPoints(PointFT[] potentialDeployPoints)
		{
			// 1. Order all points by angle
			var orderDeployPoints = potentialDeployPoints.OrderBy(x => x.Angle).ToList();

			// 2. Process through points one by one and append them to a new list until there is a point whoms angle differs by at least Math.PI
			// to the previous point. Then prepend all other points to the given list
			int i;
			for(i=1; i < orderDeployPoints.Count; i++)
			{
				if (Math.Abs(orderDeployPoints[i].Angle - orderDeployPoints[i - 1].Angle) > Math.PI)
					break; 
			}

			if(i < potentialDeployPoints.Length)
			{
				var slice = orderDeployPoints.GetRange(0, i);
				orderDeployPoints.RemoveRange(0, i);
				orderDeployPoints.AddRange(slice);
			}
			return orderDeployPoints.ToArray();
		}

		PointFT[] CalculateTankDeployPoints(DeployElement[] tanks, PointFT[] potentialDeployPoints, List<VisualObject> visuals)
		{
			if (tanks.Length == 0)
				return null;

			var potentialDeployPointsCount = potentialDeployPoints.Count();
			var stepSize = potentialDeployPointsCount/(tanks.Sum(x => x.Count) + 1);
			var tankDeployPoints = new List<PointFT>();

			for (var i = 0; i < potentialDeployPointsCount; i += stepSize)
				tankDeployPoints.Add(potentialDeployPoints.ElementAt(i));

			// Remove first and last element (in order to get the centered points)
			tankDeployPoints.RemoveAt(0);
			tankDeployPoints.RemoveAt(tankDeployPoints.Count - 1);
			tankDeployPoints = tankDeployPoints.ToList();

			visuals.Add(new PointsObject("tanks", Color.FromArgb(200, Color.Yellow), tankDeployPoints));
			return tankDeployPoints.ToArray();
		}

		PointFT[] CalculateRageDeployPoints(DeployElement rageSpell, PointFT[] potentialDeployPoints, List<VisualObject> visuals)
		{
			if (rageSpell == null)
				return null;

			// PotentialdeployPoints[] is already ordered by angle ascending
			var minAnglePoint = potentialDeployPoints.First();
			var maxAnglePoint = potentialDeployPoints.Last();
			var line = new Line(minAnglePoint, maxAnglePoint);
			visuals.Add(new LinesObject("RageSpellDeployPointLine", Color.FromArgb(200, Color.White), new[] {line}));

			// rageSpell.Count +1 because chopLine would start on the very first point of the line otherwise
			var spellDeployPoints = SmartAirDeployHelpers.ChopLine(line.Start, line.End, rageSpell.Count + 1);
			//visuals.Add(new PointsObject("BabyDragon DeployPoints", Color.FromArgb(250, Color.Tomato), spellDeployPoints, 3));
			var rageSpellDeployPoints = spellDeployPoints.Skip(1).Select(x => new PointFT((int)x.X, (int)x.Y)).ToArray();

			visuals.Add(new PointsObject("BabyDragon DeployPoints", Color.FromArgb(140, Color.Purple), rageSpellDeployPoints, 10));

			return rageSpellDeployPoints;
		}

		/// <summary>
		/// Check if required troops for our attack algorithm exist
		/// </summary>
		/// <param name="deployElements">All available units for attacking</param>
		/// <returns>True: We've got the required troops, False: We haven't got the required troops</returns>
		bool HasNeededTroops(List<DeployElement> deployElements)
		{
			return true;
		}

		/// <summary>
		/// ZapQuakes (2 lightningspells + 1 Earthquake) one Target (AirDefense)
		/// </summary>
		/// <param name="target">Target to Zapquake</param>
		/// <param name="earthQuakeSpell">Earthquake deployelement</param>
		/// <param name="lightningSpell">Lightningspell deployelement</param>
		/// <returns></returns>
		IEnumerable<int> ZapQuakeTarget(PointFT target, DeployElement earthQuakeSpell, DeployElement lightningSpell)
		{
			if (earthQuakeSpell?.Count < 1 || lightningSpell?.Count < 2)
			{
				Log.Debug("[BabyLoon] Not enough spells for ZapQuake available");
				yield break;
			}

			// Drop one earthquake spell and two lightningspells onto target
			Log.Info($"[BabyLoon] Deploying Earthquake and Lightning spells");
			foreach (var t in Deploy.AtPoint(earthQuakeSpell, target))
				yield return t;

			foreach (var t in Deploy.AtPoint(lightningSpell, target, 2))
				yield return t;
		}

		/// <summary>
		/// 1. Check for Opponent requirements (Loot & TH, Weakbase and so on)
		/// 2. Check if we can find all AirDefenses
		/// 3. Check again for AirDefenses in case we couldn't find all AirDefenses
		/// </summary>
		/// <returns>1: We want to attack that base, 0: We don't want to attack that base</returns>
		public override double ShouldAccept()
		{
			if (Opponent.MeetsRequirements(BaseRequirements.All))
			{
				// Check if all AirDefenses can be found
				/*var thLevel = TownHall.Find().Level;
				if (!thLevel.HasValue)
					thLevel = 11;

				var foundAirDefenses = AirDefense.Find().Count();
				var maxPossibleAirDefenses = AirDefense.MaxQuantity((int)thLevel);
				if (foundAirDefenses >= maxPossibleAirDefenses)
					return 1;
				
				// Repeat AirDefense check (maybe heroes were walking near the airdefenses)
				Log.Info($"[BabyLoon] Found {foundAirDefenses} / {maxPossibleAirDefenses} AirDefenses. Rescan AirDefenses in 2s!");
				Thread.Sleep(2000);

				foundAirDefenses = AirDefense.Find(CacheBehavior.ForceScan).Count();
				maxPossibleAirDefenses = AirDefense.MaxQuantity((int)thLevel);
				if (foundAirDefenses >= maxPossibleAirDefenses)
					return 1;

				Log.Info($"[BabyLoon] Found {foundAirDefenses} / {maxPossibleAirDefenses} AirDefenses. Reject base!");
				return 0;*/

				return 1;
			}
			return 0;
		}

		/// <summary>
		/// Get second closest AirDefense to deployPoints for ZapQuake
		/// </summary>
		/// <param name="closestThRedPoint"></param>
		/// <returns></returns>
		AirDefense FindZapQuakeTarget(PointFT closestThRedPoint, List<VisualObject> visuals)
		{
			var airDefenses = AirDefense.Find();
			var secondClosestAirDefense = airDefenses.OrderBy(x => x.Location.GetCenter().DistanceSq(closestThRedPoint)).ElementAtOrDefault(1);

			// Draw crosshair to visualize ZapQuakeTarget
			if (secondClosestAirDefense == null)
				return airDefenses.FirstOrDefault();

			var zapQuakeTarget = new PointFT[] {secondClosestAirDefense.Location.GetCenter()};
			visuals.Add(new CrosshairObject(zapQuakeTarget, Color.Maroon));

			return secondClosestAirDefense;
		}

		/// <summary>
		/// Distribute Babydragons with equal space to each other along all potentialDeployPoints and move them 1 tile along the axis
		/// </summary>
		/// <param name="babyDragon"></param>
		/// <param name="potentialDeployPoints"></param>
		/// <param name="visuals"></param>
		/// <returns>The PointFT[] with all BabyDragon deploypoints</returns>
		PointFT[] CalculateBabyDragonDeployPoints(DeployElement babyDragon, PointFT[] potentialDeployPoints, List<VisualObject> visuals)
		{
			if (babyDragon == null)
				return null;

			var stepSize = potentialDeployPoints.Count() / babyDragon.Count + 1;
			var babyDragonDeployPoints = new List<PointFT>();

			for (var i = 0; i < potentialDeployPoints.Count(); i += stepSize)
				babyDragonDeployPoints.Add(potentialDeployPoints.ElementAt(i));

			babyDragonDeployPoints = babyDragonDeployPoints.Select(x => x.TransformPositionAlongAxis(2).Constrain()).ToList();

			visuals.Add(new PointsObject("BabyDragon DeployPoints", Color.FromArgb(200, Color.ForestGreen), babyDragonDeployPoints, 2));
			return babyDragonDeployPoints.ToArray();
		}

		/// <summary>
		/// Distribute Balloons with equal space to each other along all potentialDeployPoints
		/// </summary>
		/// <param name="balloon"></param>
		/// <param name="potentialDeployPoints"></param>
		/// <param name="visuals"></param>
		/// <returns></returns>
		PointFT[] CalculateBalloonDeployPoints(DeployElement balloon, PointFT[] potentialDeployPoints, List<VisualObject> visuals)
		{
			if (balloon == null)
				return null;

			var potentialDeployPointsCount = potentialDeployPoints.Count();
			var stepSize = potentialDeployPointsCount / balloon.Count + 1; // +1 becuase we would otherwise get 11 balloonDeployPoints for 10 balloons
			var balloonDeployPoints = new List<PointFT>();

			for (var i = 0; i < potentialDeployPointsCount; i += stepSize)
				balloonDeployPoints.Add(potentialDeployPoints.ElementAt(i));

			visuals.Add(new PointsObject("BabyDragon DeployPoints", Color.FromArgb(120, Color.Black), balloonDeployPoints, 2));
			return balloonDeployPoints.ToArray();
		}



		/// <summary>
		/// Draws all our visual objects (for example beforehand added deployPoints)
		/// </summary>
		/// <param name="visuals">List of visual objects to draw</param>
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
				Screenshot.Save(bmp, $"BabyLoonDeploy {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}");
			}
		}
	}
}
