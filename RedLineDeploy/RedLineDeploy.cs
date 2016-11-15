using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CoC_Bot;
using CoC_Bot.API;
using CoC_Bot.Modules.Helpers;

[assembly: Addon("RedLineDeploy", "Deploy troops along the red line", "Todd Skelton (Kloc)")]
namespace RedLineDeploy
{
    [AttackAlgorithm("Red Line Deploy", "Deploys troops along the red line")]
    public class RedLineDeploy : BaseAttack
    {
        public RedLineDeploy(Opponent opponent) : base(opponent)
        {
            // Default behavior
        }

        public override double ShouldAccept()
        {
            // For debugging
            // VisualizeDeployPoints();
            
            // check if the base meets the user's requirements
            if (!Opponent.MeetsRequirements(BaseRequirements.All))
            {
                Log.Debug("[Red Line] Skipping this base because it doesn't meet the requirements");
                return 0;
            }
            return 0.7;
        }

        public override IEnumerable<int> AttackRoutine()
        {
            Log.Info("[Red Line] Attack start");

            var deployElements = Deploy.GetTroops();
            deployElements.Extract(DeployElementType.Spell);
            var heroes = deployElements.Extract(u => u.IsHero);
            var cc = deployElements.ExtractOne(DeployId.ClanCastle);
            var ranged = deployElements.Extract(u => u.IsRanged);
            var units = deployElements.OrderBy(u => u.UnitData?.AttackType != AttackType.Tank).ToList();

            var clockwisePoints = GameGrid.RedPoints
                .Where(
                    point =>
                        !(point.X > 18 && point.Y > 18 || point.X > 18 && point.Y < -18 || point.X < -18 && point.Y > 18 ||
                        point.X < -18 && point.Y < -18))
                .OrderBy(point => Math.Atan2(point.X, point.Y)).ToList();

            var inflatedPoints = clockwisePoints
                .Select(
                    point =>
                        new PointFT((float) (point.X + (point.X/Math.Sqrt(point.DistanceSq(new PointFT(0, 0))))*4),
                            (float) (point.Y + (point.Y/Math.Sqrt(point.DistanceSq(new PointFT(0, 0))))*4)))
                .ToList();

            while (units.Count > 0)
            {
                Log.Info("[Red Line] Deploying melee and tanking troops");
                foreach (var element in units)
                {
                    foreach (
                        var t in
                            Deploy.AtPoints(element,
                                clockwisePoints.GetNth(GameGrid.RedPoints.Length/(double) element.Count).ToArray()))
                        yield return t;
                }
                units.Recount();
                units.RemoveAll(unit => unit.Count < 1);
            }

            while (ranged.Count > 0)
            {
                Log.Info("[Red Line] Deploying ranged troops");
                foreach (var element in ranged)
                {
                    foreach (
                        var t in
                            Deploy.AtPoints(element,
                                inflatedPoints.GetNth(GameGrid.RedPoints.Length/(double) element.Count).ToArray()))
                        yield return t;
                }
                ranged.Recount();
                ranged.RemoveAll(unit => unit.Count < 1);
            }

            var pt = new Container<PointFT> {Item = new PointFT((float) GameGrid.MinX, GameGrid.MinY)};
            if (cc?.Count > 0 && UserSettings.UseClanTroops)
            {
                Log.Info($"[Red Line] Deploying {cc.PrettyName}");
                foreach (var y in Deploy.AtPoint(cc, pt))
                    yield return y;
            }

            if (heroes.Any())
            {
                foreach (var hero in heroes.Where(u => u.Count > 0))
                {
                    Log.Info($"[Red Line] Deploying {hero.PrettyName}");
                    foreach (var t in Deploy.AtPoint(hero, pt))
                        yield return t;
                }
            }

            Deploy.WatchHeroes(heroes);

            Log.Info("[Red Line] Deploy done");
        }

        public override string ToString()
        {
            return "Red Line Deploy";
        }

        private void VisualizeDeployPoints()
        {
            var clockwisePoints = GameGrid.RedPoints.OrderBy(point => Math.Atan2(point.X, point.Y)).ToList();
            var inflatedPoints =
                clockwisePoints.Select(
                    point =>
                        new PointFT((float)(point.X + (point.X / Math.Sqrt(point.DistanceSq(new PointFT(0, 0)))) * 4),
                            (float)(point.Y + (point.Y / Math.Sqrt(point.DistanceSq(new PointFT(0, 0)))) * 4)))
                    .ToList();

            using (Bitmap bmp = Screenshot.Capture())
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    foreach (PointFT redPoint in clockwisePoints)
                        Visualize.RectangleT(bmp, new RectangleT((int)redPoint.X, (int)redPoint.Y, 1, 1), new Pen(Color.FromArgb(128, Color.DarkRed)));

                    foreach (PointFT redPoint in inflatedPoints)
                        Visualize.RectangleT(bmp, new RectangleT((int)redPoint.X, (int)redPoint.Y, 1, 1), new Pen(Color.FromArgb(128, Color.DarkBlue)));
                }
                var d = DateTime.UtcNow;
                Screenshot.Save(bmp,
                    $"RedLineDeploy {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}");
            }
        }
    }

    public static class ListExtension
    {
        public static IEnumerable<T> GetNth<T>(this List<T> list, double n)
        {
            for (double i = 0; i < list.Count; i += n)
                yield return list[(int)i];
        }
    }
}
