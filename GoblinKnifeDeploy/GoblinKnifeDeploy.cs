using System;
using System.Collections.Generic;
using System.Linq;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using CoC_Bot.Modules.Helpers;

[assembly: Addon("GoblinKnifeDeploy", "get dark elixir with low army cost", "Cobratst")]
namespace GoblinKnifeDeploy
{
    [AttackAlgorithm("Goblin Knife Deploy", "Use goblin knife to get dark elixir")]
    public class GoblinKnifeDeploy : BaseAttack
    {
        #region variables to store attack points
        private RectangleT _border;
        private PointFT _core;
        private Container<PointFT> _orgin;
        private Tuple<PointFT, PointFT> _attackLine;
        private PointFT _earthQuakePoint;
        private PointFT _healPoint;
        private PointFT _ragePoint;
        private PointFT _target;
        #endregion

        public GoblinKnifeDeploy(Opponent opponent) : base(opponent)
        {
        }

        #region shoudAccept
        public override double ShouldAccept()
        {
            if (Opponent.MeetsRequirements(BaseRequirements.All))
            {
                return 1;
            }

            return 0;
        }
        #endregion

        #region Attack Name in toStriong Method
        public override string ToString()
        {
            return "Goblin Knife Deploy";
        }
        #endregion

        #region Calculate Deploy Points
        private void CreateDeployPoints()
        {
            var target = DarkElixirStorage.Find()?.FirstOrDefault().Location.GetCenter() ??
                            TownHall.Find()?.Location.GetCenter() ??
                            new PointFT(0, 0);
            _target = target;
            // don't include corners in case build huts are there
            var maxRedPointX = GameGrid.RedPoints.Where(p => -18 < p.Y && p.Y < 18)?.Max(point => point.X) + 1 ?? GameGrid.RedZoneExtents.MaxX;
            var minRedPointX = GameGrid.RedPoints.Where(p => -18 < p.Y && p.Y < 18)?.Min(point => point.X) - 1 ?? GameGrid.RedZoneExtents.MinX;
            var maxRedPointY = GameGrid.RedPoints.Where(p => -18 < p.X && p.X < 18)?.Max(point => point.Y) + 1 ?? GameGrid.RedZoneExtents.MaxY;
            var minRedPointY = GameGrid.RedPoints.Where(p => -18 < p.X && p.X < 18)?.Min(point => point.Y) - 1 ?? GameGrid.RedZoneExtents.MinY;

            // build a box around the base
            var left = new PointFT(minRedPointX, maxRedPointY);
            var top = new PointFT(maxRedPointX, maxRedPointY);
            var right = new PointFT(maxRedPointX, minRedPointY);
            var bottom = new PointFT(minRedPointX, minRedPointY);

            // border around the base
            _border = new RectangleT((int)minRedPointX, (int)maxRedPointY, (int)(maxRedPointX - minRedPointX), (int)(minRedPointY - maxRedPointY));

            // core is center of the box
            _core = _border.GetCenter();

            var orginPoints = new[]
            {
                new PointFT(maxRedPointX, _core.Y),
                new PointFT(minRedPointX, _core.Y),
                new PointFT(_core.X, maxRedPointY),
                new PointFT(_core.X, minRedPointY)
            };

            _orgin = new Container<PointFT> { Item = orginPoints.OrderBy(point => point.DistanceSq(target)).First() };
            #region top right
            if (_orgin.Item.X > _core.X)
            {
                Log.Info("[GoblinKnife] Attacking from the top right");

                var redLinePoint = GameGrid.RedPoints
                    .Where(point => point.Y > -10 && point.Y < 10)?
                    .Max(point => point.X) ?? GameGrid.RedZoneExtents.MaxX;

                _attackLine = new Tuple<PointFT, PointFT>(top, right);

                _earthQuakePoint = new PointFT(target.X + 5.5f, _core.Y);
                _healPoint = new PointFT(redLinePoint - 13, _core.Y);
                _ragePoint = new PointFT(redLinePoint - 9, _core.Y);
            }
            #endregion

            #region bottom left
            else if (_orgin.Item.X < _core.X)
            {
                Log.Info("[GoblinKnife] Attacking from the bottom left");

                var redLinePoint = GameGrid.RedPoints
                    .Where(point => point.Y > -10 && point.Y < 10)?
                    .Min(point => point.X) ?? GameGrid.RedZoneExtents.MinX;

                _attackLine = new Tuple<PointFT, PointFT>(bottom, left);

                _earthQuakePoint = new PointFT(target.X - 5.5f, _core.Y);
                _healPoint = new PointFT(redLinePoint + 13, _core.Y);
                _ragePoint = new PointFT(redLinePoint + 9, _core.Y);
            }
            #endregion

            #region top left
            else if (_orgin.Item.Y > _core.Y)
            {
                Log.Info("[GoblinKnife] Attacking from the top left");

                var redLinePoint = GameGrid.RedPoints
                    .Where(point => point.X > -10 && point.X < 10)?
                    .Max(point => point.Y) ?? GameGrid.RedZoneExtents.MaxY;

                _attackLine = new Tuple<PointFT, PointFT>(left, top);

                _earthQuakePoint = new PointFT(_core.X, target.Y + 5.5f);
                _healPoint = new PointFT(_core.X, redLinePoint - 13);
                _ragePoint = new PointFT(_core.X, redLinePoint - 9);
            }
            #endregion

            #region bottom right
            else // (orgin.Y < core.Y)
            {
                Log.Info("[GoblinOnife] Attacking from the bottom right");

                var redLinePoint = GameGrid.RedPoints
                    .Where(point => point.X > -10 && point.X < 10)?
                    .Min(point => point.Y) ?? GameGrid.RedZoneExtents.MinY;

                
                _attackLine = new Tuple<PointFT, PointFT>(right, bottom);

                _earthQuakePoint = new PointFT(_core.X, target.Y - 5.5f);
                _healPoint = new PointFT(_core.X, redLinePoint + 13);
                _ragePoint = new PointFT(_core.X, redLinePoint + 9);
            }
            #endregion

        }
        #endregion

        #region Attack Routine
        public override IEnumerable<int> AttackRoutine()
        {
            Log.Info("[GoblinKnife] Deploy start");

            CreateDeployPoints();

            #region get used Troops
            var deployElements = Deploy.GetTroops();

            var earthQuakeSpell = deployElements.ExtractOne(DeployId.Earthquake);
            var giant = deployElements.ExtractOne(DeployId.Giant);
            var goblin = deployElements.ExtractOne(DeployId.Goblin);
            var wizard = deployElements.ExtractOne(DeployId.Wizard);
            var wallbreaker = deployElements.ExtractOne(DeployId.WallBreaker);
            var ragespell = deployElements.ExtractOne(DeployId.Rage);
            var healspell = deployElements.ExtractOne(DeployId.Heal);
            var heroes = deployElements
                .Extract(u => (UserSettings.UseKing && u.ElementType == DeployElementType.HeroKing)
                    || (UserSettings.UseQueen && u.ElementType == DeployElementType.HeroQueen)
                    || (UserSettings.UseWarden && u.ElementType == DeployElementType.HeroWarden))
                .ToList();
            var clanCastle = deployElements.ExtractOne(u => u.ElementType == DeployElementType.ClanTroops && UserSettings.UseClanTroops);
            #endregion

            #region Deploy EarthQuake Spells
            //open near to dark elixer with 4 earthquakes
            if (earthQuakeSpell?.Count >=4)
            {
                foreach (var t in Deploy.AtPoint(earthQuakeSpell, _earthQuakePoint, 4))
                {
                    yield return t;
                }
               
            }
            #endregion

            #region Deploy 4 giants in a line to tank the funnel
            foreach (var t in Deploy.AlongLine(giant, _attackLine.Item1, _attackLine.Item2, 4, 4))
            {
                yield return t;
            }
            #endregion

            #region Deploy Wizards after giant to funnel
            foreach (var t in Deploy.AlongLine(wizard, _attackLine.Item1, _attackLine.Item2, 8, 4))
            {
                yield return t;
            }
            #endregion

            yield return 3000;

            #region Deploy rage spell for giants and wall-breakers
            foreach (var t in Deploy.AtPoint(ragespell, _ragePoint))
                yield return t;
            #endregion

            #region Deploy wall-breakers to open the first one or two wall layers
            while (wallbreaker?.Count > 0)
            {
                var count = wallbreaker.Count;

                Log.Info($"[GoblinKnife] Deploying {wallbreaker.PrettyName} x3");
                foreach (var t in Deploy.AtPoint(wallbreaker, _orgin, 3))
                    yield return t;

                // prevent infinite loop if deploy point is on red
                if (wallbreaker.Count != count) continue;

                Log.Warning($"[GoblinKnife] Couldn't deploy {wallbreaker.PrettyName}");
                break;
            }
            #endregion

            #region Deploy rest of giants in one spot
            while (giant?.Count > 0)
            {
                var count = giant.Count;

                Log.Info($"[GoblinKnife] Deploying {giant.PrettyName} x10");
                foreach (var t in Deploy.AtPoint(giant, _orgin, 10))
                    yield return t;

                // prevent infinite loop if deploy point is on red
                if (giant.Count != count) continue;

                Log.Warning($"[GoblinKnife] Couldn't deploy {giant.PrettyName}");
                break;
            }
            #endregion

            yield return 1000;

            #region Deploy rest of Wizards 
            while (wizard?.Count > 0)
            {
                var count = giant.Count;

                Log.Info($"[GoblinKnife] Deploying {wizard.PrettyName} x10");
                foreach (var t in Deploy.AlongLine(wizard, _attackLine.Item1, _attackLine.Item2, 8, 4))
                    yield return t;

                // prevent infinite loop if deploy point is on red
                if (wizard.Count != count) continue;

                Log.Warning($"[GoblinKnife] Couldn't deploy {wizard.PrettyName}");
                break;
            }
            #endregion

            yield return 2000;

            #region deploy heal spell to our tanks and wizards
            foreach (var t in Deploy.AtPoint(healspell, _healPoint))
                yield return t;
            #endregion

            #region deploy clanCastle Troops
            if (clanCastle?.Count > 0)
            {
                Log.Info($"[GoblinKnife] Deploying {clanCastle.PrettyName}");
                foreach (var t in Deploy.AtPoint(clanCastle, _orgin))
                    yield return t;
            }
            #endregion

            #region Deploy Heroes
            if (heroes.Any())
            {
                foreach (var hero in heroes.Where(u => u.Count > 0))
                {
                    foreach (var t in Deploy.AtPoint(hero, _orgin))
                        yield return t;
                }
                Deploy.WatchHeroes(heroes, 7000);
            }
            #endregion

            yield return 2000;

            #region Deploy all Goblins
            while (goblin?.Count > 0)
            {
                var count = goblin.Count;

                Log.Info($"[GoblinKnife] Deploying {goblin.PrettyName} x20");
                foreach (var t in Deploy.AtPoint(goblin, _orgin, 20))
                    yield return t;

                // prevent infinite loop if deploy point is on red
                if (goblin.Count != count) continue;

                Log.Warning($"[GoblinKnife] Couldn't deploy {goblin.PrettyName}");
                break;
            }
            #endregion

            yield return 1500;

            #region Deploy heal spell on the target
            foreach (var t in Deploy.AtPoint(healspell, _target))
                yield return t;
            #endregion

            yield break;
        }
        #endregion
    }
}
