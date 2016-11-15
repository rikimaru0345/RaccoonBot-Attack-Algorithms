using System;
using System.Collections.Generic;
using System.Linq;
using CoC_Bot;
using CoC_Bot.Internals;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using CoC_Bot.Modules.Helpers;
using JetBrains.Annotations;
using TargetType = CoC_Bot.TargetType;

[assembly: Addon("SnipeTownHallDeploy Addon", "Contains the SnipeTownHall deploy algorithm", "BoostBotTeam")]

namespace SnipeDeploy
{
    [UsedImplicitly]
    [AttackAlgorithm("SnipeTownHallDeploy", "Attacks vulnerable town halls with a minimal amount of troops to gain trophies")]
    class SnipeTownHallDeploy : BaseAttack
    {
        public SnipeTownHallDeploy(Opponent opponent)
            : base(opponent)
        {
        }
        public override string ToString()
        {
            return "Snipe Town Hall Deploy";
        }

        public override double ShouldAccept()
        {
            var th = TownHall.Find();
            if (th == null)
            {
                Log.Warning("[Snipe Deploy] Townhall not found.");
                return 0;
            }

            // for debugging
            // VisualizeTownhall();

            if (th.Location.Top > GameGrid.MaxY - UserSettings.SnipeThDistanceLimit) return .1;
            if (th.Location.Right > GameGrid.MaxX - UserSettings.SnipeThDistanceLimit) return .1;
            if (th.Location.Bottom < GameGrid.MinY + UserSettings.SnipeThDistanceLimit) return .1;
            if (th.Location.Left < GameGrid.MinX + UserSettings.SnipeThDistanceLimit) return .1;

            Log.Debug("[Snipe Deploy] Algorithm failed because the townhall isn't near the edge.");
            Opponent.FailedReason = "The Town Hall isn't near the edge.";
            return 0;
        }

        public override IEnumerable<int> AttackRoutine()
        {
            Log.Debug("[Snipe Deploy] Getting the best townhall deploy point.");
            var snipePoint = new Container<PointFT> {Item = GetTownHallDeployPoint()};

            Log.Debug($"[Snipe Deploy] Deploy point is ({snipePoint.Item})");

            var troops = Deploy.GetTroops();

            var hero = troops.GetQueen() ?? troops.GetKing() ?? troops.GetWarden();

            if (hero != null)
            {
                Log.Info("[Snipe Deploy] Deploying the " + hero.PrettyName + " to snipe.");
                foreach (var t in Deploy.AtPoint(hero, snipePoint))
                    yield return t;

                hero.Recount();

                if (hero.Count > 0)
                {
                    var th = TownHall.Find();

                    using (var bmp = Screenshot.Capture(true))
                    {
                        Visualize.RectangleT(bmp, th.Location);
                        Visualize.RectangleT(bmp, new RectangleT((int) snipePoint.Item.X, (int) snipePoint.Item.Y, 1, 1));
                        var d = DateTime.UtcNow;
                        Screenshot.Save(bmp, $"Snipe Deploy_{d.Year}-{d.Month}-{d.Day}_{d.Hour}-{d.Minute}-{d.Second}");
                    }
                }

                yield return 5000;

                Deploy.WatchHeroes(new List<DeployElement> {hero});

                var countdown = new Countdown(15.0);
                
                if(hero.Count < 1)
                    while (countdown.IsRunning)
                        if (Attack.SurrenderIfWeHaveAStar()) yield break;
                        else yield return 200;
                else Log.Info("[Snipe Deploy] Hero failed to deploy; trying to use troops");
            }

            var snipeTroops =
                troops.GetByAttackType(AttackType.Damage)
                    .GetByType(DeployElementType.NormalUnit)
                    .Where(u => u.UnitData?.TargetType == TargetType.Loot || u.UnitData?.TargetType == TargetType.None)
                    .ToArray();

            if (snipeTroops.Length > 0)
            {
                Log.Info("[Snipe Deploy] Deploying troops to snipe.");

                var countdown = new Countdown(15.0, true);
                var pt = new Container<PointFT> {Item = GetTownHallDeployPoint()};
                while (true)
                {
                    if (countdown.IsFinished)
                    {
                        foreach (var t in Deploy.AtPoint(snipeTroops, snipePoint))
                            yield return t;

                        snipeTroops.Recount();

                        if (snipeTroops.All(u => u.Count < 1))
                            yield break;

                        countdown.Restart();
                    }

                    if (Attack.SurrenderIfWeHaveAStar())
                        yield break;
                }
            }
        }

        private static PointFT GetTownHallDeployPoint()
        {
            var location = TownHall.Find().Location;

            var distanceLeft = Math.Abs(location.Left - GameGrid.MinX);
            var distanceRight = Math.Abs(location.Right - GameGrid.MaxX);
            var distanceTop = Math.Abs(location.Top - GameGrid.MaxY);
            var distanceBottom = Math.Abs(location.Bottom - GameGrid.MinY);

            var list = new[] { distanceLeft, distanceRight, distanceTop, distanceBottom };

            var min = list.Min();

            if (min == distanceLeft)
            {

                if (distanceLeft == distanceTop)
                {
                    Log.Debug("[Snipe Deploy] Townhall is closest to the west.");
                    return new PointFT(PointFT.MinGameGridX - 3, PointFT.MaxGameGridY + 3);
                }
                if (distanceLeft == distanceBottom)
                {
                    Log.Debug("[Snipe Deploy] Townhall is closest to the south.");
                    return new PointFT(PointFT.MinGameGridX - 3, PointFT.MinGameGridY - 3);
                }
                Log.Debug("[Snipe Deploy] Townhall is closest to the southwest.");
                return new PointFT(PointFT.MinGameGridX - 3, location.GetCenter().Y);
            }
            else if (min == distanceRight)
            {
                if (distanceRight == distanceTop)
                {
                    Log.Debug("[Snipe Deploy] Townhall is closest to the north.");
                    return new PointFT(PointFT.MaxGameGridX + 3, PointFT.MaxGameGridY + 3);
                }
                if (distanceRight == distanceBottom)
                {
                    Log.Debug("[Snipe Deploy] Townhall is closest to the east.");
                    return new PointFT(PointFT.MaxGameGridX + 3, PointFT.MinGameGridY - 3);
                }
                Log.Debug("[Snipe Deploy] Townhall is closest to the northeast.");
                return new PointFT(PointFT.MaxGameGridX + 3, location.GetCenter().Y);
            }
            else if (min == distanceTop)
            {
                if (distanceTop == distanceLeft)
                {
                    Log.Debug("[Snipe Deploy] Townhall is closest to the west.");
                    return new PointFT(PointFT.MinGameGridX - 3, PointFT.MaxGameGridY + 3);
                }
                if (distanceTop == distanceRight)
                {
                    Log.Debug("[Snipe Deploy] Townhall is closest to the north.");
                    return new PointFT(PointFT.MaxGameGridX + 3, PointFT.MaxGameGridY + 3);
                }
                Log.Debug("[Snipe Deploy] Townhall is closest to the northwest.");
                return new PointFT(location.GetCenter().X, PointFT.MaxGameGridY + 3);
            }
            else //if (min == distanceBottom)
            {
                if (distanceBottom == distanceLeft)
                {
                    Log.Debug("[Snipe Deploy] Townhall is closest to the south.");
                    return new PointFT(PointFT.MinGameGridX - 3, PointFT.MinGameGridY - 3);
                }
                if (distanceBottom == distanceRight)
                {
                    Log.Debug("[Snipe Deploy] Townhall is closest to the east.");
                    return new PointFT(PointFT.MaxGameGridX + 3, PointFT.MinGameGridY - 3);
                }
                Log.Debug("[Snipe Deploy] Townhall is closest to the southeast.");
                return new PointFT(location.GetCenter().X, PointFT.MinGameGridY - 3);
            }
        }

        private static void VisualizeTownhall()
        {
            var th = TownHall.Find();

            using (var bmp = Screenshot.Capture())
            {
                Visualize.Grid(bmp);
                Visualize.Axes(bmp);
                Visualize.RectangleT(bmp, th.Location);
                Screenshot.Show(bmp);
            }
        }
    }
}
