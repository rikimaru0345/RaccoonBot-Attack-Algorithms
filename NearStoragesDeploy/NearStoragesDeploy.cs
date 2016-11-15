using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using CoC_Bot;
using CoC_Bot.API;
using CoC_Bot.Modules.Helpers;
using Shared;

[assembly: Addon("NearStoragesDeploy", "Deploy troops near the storages", "BoostBot")]
namespace NearStoragesDeploy
{
    [AttackAlgorithm("Near Storage Deploy", "Deploy troops near the storages")]
    class NearStoragesDeploy : BaseAttack
    {
        public NearStoragesDeploy(Opponent opponent)
            : base(opponent)
        {
        }
        public override string ToString()
        {
            return "Near Storages Deploy";
        }

        bool surrenderOnFirstStar = false;
        private Point[] _deployPoints;
        public override double ShouldAccept()
        {
            if (!Opponent.MeetsRequirements(BaseRequirements.All))
                return 0;
            return .7;
        }


        public override IEnumerable<int> AttackRoutine()
        {
            List<Point> deployPoints = new List<Point>();
            var combatNormalDeployPoints = GetStorageAttackPoints(RedPoints);
            deployPoints.AddRange(combatNormalDeployPoints);

            _deployPoints = deployPoints.ToArray();


            if (_deployPoints == null)
                throw new ArgumentNullException(nameof(_deployPoints));
            if (_deployPoints.Length == 0)
                throw new ArgumentException(nameof(_deployPoints) + " must contain at least one point");

            // Get outer border to move ranges towards there if possible
            List<Point> outerBorderPoints = DeployHelper.GetRectPoints(15);
            
            if (surrenderOnFirstStar)
                Logger.Info("[Deploy] Bot will surrender as soon as the first star is reached to save troops (Trophy Push Mode)");


            var allDeployElements = Deploy.GetTroops();

            var heroes = allDeployElements
                .Where(u => (UserSettings.UseKing && u.ElementType == DeployElementType.HeroKing)
                    || (UserSettings.UseQueen && u.ElementType == DeployElementType.HeroQueen)
                    || (UserSettings.UseWarden && u.ElementType == DeployElementType.HeroWarden))
                .ToList();

            int deployPointCounter = 0;
            int waveLimit = UserSettings.WaveSize;
            double waitTimeSeconds = UserSettings.WaveDelay;
            Random rng = new Random();

            while (true)
            {
                Logger.Debug("Scan Troops");
                // Scan available troops
                var units = Deploy.GetTroops()
                    .Where(u => u.ElementType == DeployElementType.NormalUnit
                        || (UserSettings.UseKing && u.ElementType == DeployElementType.HeroKing)
                        || (UserSettings.UseQueen && u.ElementType == DeployElementType.HeroQueen)
                        || (UserSettings.UseWarden && u.ElementType == DeployElementType.HeroWarden)
                        || (UserSettings.UseClanTroops && u.ElementType == DeployElementType.ClanTroops))
                    .ToList();

                // Remove king/queen
                ExtractHeroes(units, heroes);

                Logger.DebugDev("Deployable Troops: " + ToUnitString(units));
                // Break if we don't have any left
                if (units.Count == 0 || units.All(u => u.Count == 0))
                    break;


                // Order units by priority
                // Tank > Wallbreaker > Heal > Damage > Heroes
                Logger.Debug("OrderTroops");
                units.OrderForDeploy();
                int waveCounter = 0;
                foreach (var u in units)
                {
                    if (u == null)
                    {
                        Logger.Info("Wave #{0} complete, short deploy delay now...", waveCounter);
                        yield return rng.Next(900, 2000);
                        continue;
                    }

                    if (u.Count == 0)
                        continue;

                    // Select unit
                    Logger.Debug("[Deploy] Deploying '{0}'", u);
                    u.Select();

                    // Deploy them
                    while (true)
                    {
                        int unitCount = u.Count;
                        int totalDeployedThisWave = 0;
                        while (unitCount > 0)
                        {
                            //var line = deployLines[lineCounter++ % deployLines.Length];
                            //
                            //DeployHelper.ClickAlongLine(line.Item1, line.Item2, deployCount, 10);

                            var deployCount = Math.Min(u.Count, 4);
                            Logger.Debug("Deploy Start");
                            for (int i = 0; i < deployCount; i++)
                            {
                                if (surrenderOnFirstStar)
                                    if (SurrenderIfWeHaveAStar())
                                    {
                                        yield return 500;
                                        yield break;
                                    }

                                if (deployPointCounter >= _deployPoints.Length)
                                    deployPointCounter = 0;

                                Logger.Debug("deploy at point index {0} of {1}", deployPointCounter, _deployPoints.Length);
                                var point = _deployPoints[deployPointCounter++];


                                // If this unit is ranged, we deploy further back
                                if (u.IsRanged)
                                {
                                    var borderPoint = outerBorderPoints.OrderBy(p => p.DistanceSq(point)).First();
                                    var distance = (int)Math.Sqrt(borderPoint.DistanceSq(point));
                                    if (distance > 10)
                                    {
                                        var maxMove = Math.Min(u.UnitData.Range * 16, distance);
                                        var dir = borderPoint.Normalize();
                                        // Clamp the distance to the max move distance so we dont deploy too far behind
                                        borderPoint = new Point((int)(dir.Item1 * maxMove) + point.X, (int)(dir.Item2 * maxMove) + point.Y);
                                        var t = (float)rng.Range(0.85, 1.05);
                                        point = point.Lerp(borderPoint, t);
                                    }
                                }

                                // Modify this point a bit so its not too ovbious
                                point.X += rng.Next(-12, 12);
                                point.Y += rng.Next(-12, 12);

                                Input.Click(point);
                                totalDeployedThisWave++;
                                if (totalDeployedThisWave >= waveLimit)
                                {
                                    Logger.Info("WaveLimit {0} reached. Wait {1:0.0} sec.", waveLimit, waitTimeSeconds);
                                    yield return (int)(waitTimeSeconds * 1000);
                                    totalDeployedThisWave = 0;
                                }

                                Thread.Sleep(10);
                                Thread.Sleep(5);
                            }
                            Logger.Debug("Deploy End");

                            unitCount -= deployCount;
                        }

                        // Refresh unit count, if its really 0, break.
                        Logger.Debug("RecountA");
                        int countA = u.Count;
                        u.Recount();
                        int countB = u.Count;
                        Logger.Debug("RecountB");

                        if (countA != countB)
                            Logger.Info("Recount of '{0}'. {1}->{2}", u.PrettyName, countA, countB);

                        if (u.Count <= 0)
                        {
                            Logger.Info("Unit '{0}' depleted. Break.", u.PrettyName);
                            yield return 500;
                            break;
                        }
                    }


                    waveCounter++;
                }
                yield return 50;
            }

            if (heroes.Count > 0)
            {
                foreach (var y in DeployHeroes(heroes, _deployPoints))
                {
                    if (surrenderOnFirstStar)
                        if (SurrenderIfWeHaveAStar())
                            break;

                    yield return y;
                }
            }

            Logger.Info("[Deploy] Deploy done.");
        }

    }
}
