using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using CoC_Bot;
using CoC_Bot.API;
using CoC_Bot.Internals;
using CoC_Bot.Internals.ProfileSettings;
using CoC_Bot.Modules.AttackAlgorithms;
using CoC_Bot.Modules.Helpers;
using Shared;


[assembly: Addon("OneSideDeploy Addon", "Contains the OneSide deploy algorithm", "BoostBotTeam")]

namespace OneSideDeploy
{
    [AttackAlgorithm("OneSideDeploy", "Deploys units on one side of the field while moving the deploy points from the outer edge as close as possible to the red zone")]
    class OneSideDeploy : BaseAttack
    {
        public OneSideDeploy(Opponent opponent)
            : base(opponent)
        {
        }

        public override string ToString()
        {
            return "One Side Deploy";
        }

        public override double ShouldAccept()
        {
            if (!Opponent.MeetsRequirements(BaseRequirements.All))
                return 0;
            return .7;
        }

        public override IEnumerable<int> AttackRoutine()
        {
            var allDeployElements = Deploy.GetTroops();
            
            List<Point> redPoints = new List<Point>();
            foreach (var s in AttackHelper.FindRedPoints(redPoints))
                yield return s;
            
            var leftCorner = DeployHelper.DeployPointALeft;
            var topCorner = DeployHelper.DeployPointATop;

            double tankPaddingFraction = 0.35;
            var tankDeployA = leftCorner.Lerp(topCorner, tankPaddingFraction);
            var tankDeployB = leftCorner.Lerp(topCorner, 1 - tankPaddingFraction);

            double wallBreakerFraction = 0.43;
            var wallBreakDeployA = leftCorner.Lerp(topCorner, wallBreakerFraction);
            var wallBreakDeployB = leftCorner.Lerp(topCorner, 1 - wallBreakerFraction);

            double damageFraction = 0.15;
            var damageDeployPointA = leftCorner.Lerp(topCorner, damageFraction);
            var damageDeployPointB = leftCorner.Lerp(topCorner, 1 - damageFraction);
            
            // We want to drag our deploy points to the closest redline point
            // For that we get the vector from left to top, then we get its orthogonal vector (the one of both that points more towards the center)
            // then we can order the redline points. we want the one whos direction is closest to the orthogonal vector.
            var vecLeftTop = topCorner.Subtract(leftCorner).Normalize();
            var ortho = GetOrthogonalDirection(tankDeployA, new Point((int)(vecLeftTop.Item1 * 30), (int)(vecLeftTop.Item2 * 30)), new PointF(0.5f, 0.5f).ToAbsolute());
            tankDeployA = GetBestRedlinePoint(redPoints, tankDeployA, ortho);
            tankDeployB = GetBestRedlinePoint(redPoints, tankDeployB, ortho);

            wallBreakDeployA = GetBestRedlinePoint(redPoints, wallBreakDeployA, ortho);
            wallBreakDeployB = GetBestRedlinePoint(redPoints, wallBreakDeployB, ortho);

            
            var attackLine = DeployHelper.GetPointsForLine(damageDeployPointA, damageDeployPointB, 30).ToArray();
            for (int i = 0; i < attackLine.Length; i++)
                attackLine[i] = GetBestRedlinePoint(redPoints, attackLine[i], ortho);
            attackLine = attackLine.Distinct().ToArray();
            

            var validUnits = allDeployElements.Where(u => u.UnitData != null).ToArray();
            var clanTroops = allDeployElements.FirstOrDefault(u => u.ElementType == DeployElementType.ClanTroops);
            //Func<DeployElement, bool> isAir = e => e.UnitData.UnitType == UnitType.Air;
            Func<DeployElement, bool> isBalloon = e => e.UnitData.NameSimple.Contains("balloon");
            Func<DeployElement, bool> isMinion = e => e.UnitData.NameSimple.Contains("minion");

            var tankUnits = validUnits.Where(u => u.UnitData.AttackType == AttackType.Tank).ToArray();
            var wallBreakerUnits = validUnits.Where(u => u.UnitData.AttackType == AttackType.Wallbreak).ToArray();
            var attackUnits = validUnits.Where(u => u.UnitData.AttackType == AttackType.Damage && !isMinion(u)).ToArray();
            if (clanTroops != null && UserSettings.UseClanTroops)
                attackUnits = attackUnits.Concat(new[]{clanTroops}).ToArray();
            var healUnits = validUnits.Where(u => u.UnitData.AttackType == AttackType.Heal).ToArray();
            var balloonUnits = validUnits.Where(isBalloon).ToArray();
            var minionUnits = validUnits.Where(isMinion).ToArray();
            
            // Deploy tank units
            if (tankUnits.Any())
            {
                Logger.Debug($"{tankUnits.Length} tanking element{(tankUnits.Length > 1 ? "s" : "")} available to deploy.");
                foreach (var s in DeployUnits(tankUnits, new[] { tankDeployA, tankDeployB }, 20))
                    yield return s;
                yield return 2000;
            }

            // Deploy wallbreakers
            if (tankUnits.Any())
            {
                Logger.Debug("Wallbreakers available to deploy.");
                foreach (var s in DeployUnits(wallBreakerUnits, new[] { wallBreakDeployA, wallBreakDeployB }, 40))
                    yield return s;
                yield return 1000;
            }
            
            // Check whether we got an air troopset and decide to perform an air attack or not
            var balloonCount = balloonUnits.Sum(i => i.Count);
            if (balloonCount > 10)
            {
                // Ok, we have an air troopset, so we will deploy the air units first according to different deploy rules.
                attackUnits = attackUnits.Where(u => !isBalloon(u)).ToArray();

                int spotCount = (int)Math.Ceiling(balloonCount / 4.0);
                // We want to make x spots where balloons are deployed
                var airPoints = DeployHelper.GetPointsForLine(damageDeployPointA, damageDeployPointB, spotCount);
                for (int i = 0; i < airPoints.Count; i++)
                    airPoints[i] = GetBestRedlinePoint(redPoints, airPoints[i], ortho);
                airPoints = airPoints.Distinct().ToList();

                // Deploy those air units
                foreach (var s in DeployUnits(balloonUnits, airPoints.ToArray(), firstCycleYield: 1000))
                    yield return s;
            }
            
            // Deploy atackers
            if (attackUnits.Any())
            {
                Logger.Debug($"{attackUnits.Length} attacking element{(attackUnits.Length > 1 ? "s" : "")} available to deploy.");
                foreach (var s in DeployUnits(attackUnits, attackLine, 0, 5, 2500))
                    yield return s;
                yield return 500;
            }

            // Minions
            if (minionUnits.Any())
                foreach (var s in DeployUnits(minionUnits, attackLine))
                    yield return s;

            // Deploy healers
            foreach (var s in DeployUnits(healUnits, attackLine))
                yield return s;

            // Deploy heroes
            var heroes = allDeployElements
                    .Where(u => (UserSettings.UseKing && u.ElementType == DeployElementType.HeroKing)
                        || (UserSettings.UseQueen && u.ElementType == DeployElementType.HeroQueen)
                        || (UserSettings.UseWarden && u.ElementType == DeployElementType.HeroWarden))
                    .ToList();

            if (heroes.Count > 0)
                foreach (var y in DeployHeroes(heroes, attackLine))
                    yield return y;
        }

        IEnumerable<int> DeployUnits(DeployElement[] units, Point[] deployPoints, int clickYield = 0, int waveCount = 1, int waveYield = 0, int firstCycleYield = 0)
        {
            int totalDeployCounter = 0;
            int unitsDeployedThisWave = 0;
            int currentWave = 1;
            int waveSize = units.Sum(u => u.Count) / waveCount;

            foreach (var element in units)
                while (true)
                {
                    if (element.Count <= 0)
                        break;
                    
                    Logger.Debug($"[One Side] Deploying {element.Count} {element.PrettyName}s");

                    // Select unit from selection bar at the bottom
                    for (int i = 0; i < 5; i++)
                        Input.Click(element.Rect.GetCenter());
                    yield return 50;

                    // Deploy at given points
                    int pointIndex = 0;
                    Func<Point> nextPoint = () => deployPoints[(++pointIndex % deployPoints.Length)];
                    for (int i = 0; i < element.Count; i++)
                    {
                        var p = nextPoint();
                        Input.Click(p);

                        bool firstCycleEnded = pointIndex == deployPoints.Length;

                        if (firstCycleYield > 0 && firstCycleEnded)
                            yield return firstCycleYield;

                        if (clickYield <= 0)
                        {
                            if (i % 5 == 0) yield return 20;
                        }
                        else
                            yield return clickYield;

                        totalDeployCounter++;
                        unitsDeployedThisWave++;
                        if (waveCount > 1 && currentWave <= waveCount)
                        {
                            if (unitsDeployedThisWave >= waveSize)
                            {
                                unitsDeployedThisWave = 0;
                                currentWave++;
                                yield return waveYield;
                            }
                        }
                    }

                    yield return 200;

                    var logCount = element.Count;

                    element.Recount();

                    Logger.Debug($"{element.PrettyName}s deployed: {logCount}->{element.Count}");
                }
        }

        Point GetBestRedlinePoint(IEnumerable<Point> redlinePoints, Point origin, Point direction)
        {
            var potentialPoints = redlinePoints.Where(p => p.DistanceSq(origin) < 180 * 180).ToArray();

            if (potentialPoints.Length == 0)
                // no potential points??
                redlinePoints.OrderBy(p => p.DistanceSq(origin)).First();

            var refDir = direction.Normalize();

            return potentialPoints.OrderByDescending(p =>
            {
                var dir = p.Subtract(origin).Normalize();
                var scalar = refDir.Item1 * dir.Item1 + refDir.Item2 * dir.Item2;
                return scalar;
            }).First();
        }

        Point GetOrthogonalDirection(Point origin, Point direction, Point closestRefernce)
        {
            var orthA = new Point(-direction.Y, direction.X);

            var potentialA = new Point(origin.X + orthA.X, origin.Y + orthA.Y);
            var potentialB = new Point(origin.X - orthA.X, origin.Y - orthA.Y);

            double distA = closestRefernce.DistanceSq(potentialA);
            double distB = closestRefernce.DistanceSq(potentialB);

            if (distA < distB)
                return potentialA;

            return potentialB;
        }
    }
}
