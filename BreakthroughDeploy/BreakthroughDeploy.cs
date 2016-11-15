using CoC_Bot.API;
using System;
using System.Collections.Generic;
using System.Linq;
using CoC_Bot;
using CoC_Bot.API.Buildings;
using System.Drawing;
using System.Threading;
using CoC_Bot.Modules.Helpers;

[assembly: Addon("Breakthrough Deploy", "One side deployment for TH10 and TH11 farming.", "Todd Skelton (Kloc)")]
namespace BreakthroughDeploy
{
    [AttackAlgorithm("Breakthrough Deploy", "One side deployment for TH10 and TH11 farming.")]
    public class BreakthroughDeploy : BaseAttack
    {
        public BreakthroughDeploy(Opponent opponent) : base(opponent)
        {
            // Default behavior
        }

        public override string ToString()
        {
            return "Breakthrough Deploy";
        }

        public override double ShouldAccept()
        {
            if (Opponent.MeetsRequirements(BaseRequirements.All))
            {
#if DEBUG
                CreateDeployPoints(true);
                VisualizeDeployment();
#endif
                return 1;
            }
            return 0;
        }

        private Container<PointFT> _orgin;

        private PointFT _core, _healPoint, _ragePoint, _healerPoint, _qwPoint, _queenRagePoint;

        private RectangleT _border;

        private Tuple<PointFT, PointFT> _attackLine;

        private DeployElement _freezeSpell;

        private void DropFreeze(object sender, EventArgs a)
        {
            var inferno = (InfernoTower)sender;

            foreach (var t in Deploy.AtPoint(_freezeSpell, inferno.Location.GetCenter()))
                Thread.Sleep(t);

            inferno.StopWatching();
        }

        public override IEnumerable<int> AttackRoutine()
        {
            Log.Info("[Breakthrough] Deploy start");

            var funnelIds = new[] { DeployId.Archer, DeployId.Barbarian, DeployId.Minion, DeployId.Wizard };
            var byLineIds = new[] { DeployId.Archer, DeployId.Barbarian, DeployId.Minion, DeployId.Wizard, DeployId.Balloon, DeployId.Dragon, DeployId.BabyDragon, DeployId.Miner };
            var byPointIds = new[] { DeployId.Valkyrie, DeployId.Pekka, DeployId.Witch, DeployId.Goblin, DeployId.Bowler };

            // get a list of all deployable units
            var deployElements = Deploy.GetTroops();

            // extract spells into their own list
            var spells = deployElements.Extract(DeployElementType.Spell);

            // extract heores into their own list
            var heroes = deployElements.Extract(u => u.IsHero);

            // extract clanCastle into its own list
            var clanCastle = deployElements.ExtractOne(u => u.ElementType == DeployElementType.ClanTroops);

            // get tanks
            var tanks = deployElements.Extract(AttackType.Tank).ToArray();

            // get wallbreakers
            var wallBreakers = deployElements.ExtractOne(DeployId.WallBreaker);

            // get healers
            var healers = deployElements.ExtractOne(DeployId.Healer);

            // get funnel troops
            var funnel = funnelIds.Select(id => deployElements.FirstOrDefault(u => u.Id == id)).Where(u => u != null).ToArray();

            // get deploy all in a line
            var byLine = deployElements.Extract(byLineIds).ToArray();

            // get deploy all by point
            var byPoint = deployElements.Extract(byPointIds).ToArray();

            // get hogs
            var hogs = deployElements.ExtractOne(u => u.Id == DeployId.HogRider);

            // get heal spells
            var healSpells = spells.ExtractOne(u => u.Id == DeployId.Heal);

            // get rage spells
            var rageSpells = spells.ExtractOne(u => u.Id == DeployId.Rage);

            // user's wave delay setting
            var waveDelay = (int)(UserSettings.WaveDelay * 1000);

            // check if queen walk is an option
            if (heroes.Any(u => u.Id == DeployId.Queen) && healers?.Count >= 4)
            {
                var queen = heroes.ExtractOne(u => u.Id == DeployId.Queen);

                // get deploy points with queen walk
                CreateDeployPoints(true);

                // deploy queen walk
                Log.Info("[Breakthrough] Queen walk available.");
                Log.Info($"[Breakthrough] Deploying {queen.PrettyName}");
                foreach (var t in Deploy.AtPoint(queen, _qwPoint, waveDelay: waveDelay))
                    yield return t;

                var healerCount = Math.Min(healers.Count, 4);
                Log.Info($"[Breakthrough] Deploying {healers.PrettyName} x{healerCount}");
                foreach (var t in Deploy.AtPoint(healers, _healerPoint, healerCount, waveDelay: waveDelay))
                    yield return t;

                // watch queen
                Deploy.WatchHeroes(new List<DeployElement> { queen });

                if (rageSpells?.Count > 1)
                {
                    Log.Info($"[Breakthrough] Deploying {rageSpells.PrettyName} x1");
                    foreach (var t in Deploy.AtPoint(rageSpells, _queenRagePoint, waveDelay: waveDelay))
                        yield return t;
                }

                // wait 15 seconds
                yield return 15000;
            }
            else
            {
                // get deploy points without queen walk
                CreateDeployPoints(false);
            }

            var funnelTank = tanks.FirstOrDefault(u => u.Id == DeployId.Giant) ?? tanks.FirstOrDefault();

            // deploy four tanks if available
            if(funnelTank != null)
            {
                var deployCount = Math.Min(funnelTank.Count, 4);

                Log.Info($"[Breakthrough] Deploying {funnelTank.PrettyName} x{deployCount}");
                foreach (var t in Deploy.AlongLine(funnelTank, _attackLine.Item1, _attackLine.Item2, deployCount, 
                    deployCount, waveDelay: waveDelay))
                    yield return t;
            }

            // deploy funnel
            foreach (var unit in funnel.Where(u => u.Count > 0))
            {
                var deployElementCount = Math.Min(unit.Count, UserSettings.WaveSize / unit.UnitData.HousingSpace);

                Log.Info($"[Breakthrough] Deploying {unit.PrettyName} x{deployElementCount}");
                foreach (
                    var t in
                        Deploy.AlongLine(unit, _attackLine.Item1, _attackLine.Item2, deployElementCount, 4,
                            waveDelay: waveDelay))
                    yield return t;
            }

            // deploy Wallbreakers
            while (wallBreakers?.Count > 0)
            {
                var count = wallBreakers.Count;

                Log.Info($"[Breakthrough] Deploying {wallBreakers.PrettyName} x3");
                foreach (var t in Deploy.AtPoint(wallBreakers, _orgin, 3))
                    yield return t;

                // prevent infinite loop if deploy point is on red
                if (wallBreakers.Count != count) continue;

                Log.Warning($"[Breakthrough] Couldn't deploy {wallBreakers.PrettyName}");
                break;
            }
            

            // deploy the rest of the tanks
            while (tanks.Any(u => u.Count > 0))
            {
                var deployError = false;

                foreach (var unit in tanks.Where(u => u.Count > 0))
                {
                    var count = unit.Count;

                    Log.Info($"[Breakthrough] Deploying {unit.PrettyName} x{unit.Count}");
                    foreach (var t in Deploy.AtPoint(unit, _orgin, unit.Count, waveDelay: waveDelay))
                        yield return t;

                    // prevent infinite loop if deploy point is on red
                    if (unit.Count != count) continue;

                    Log.Warning($"[Breakthrough] Couldn't deploy {unit.PrettyName}");
                    deployError = true;
                    break;
                }
                if (deployError) break;
            }

            if (rageSpells?.Count > 0)
            {
                Log.Info($"[Breakthrough] Deploying {rageSpells.PrettyName} x1");
                foreach (var t in Deploy.AtPoint(rageSpells, _ragePoint, waveDelay: waveDelay))
                    yield return t;
            }

            if (healSpells?.Count > 0)
            {
                Log.Info($"[Breakthrough] Deploying {healSpells.PrettyName} x1");
                foreach (var t in Deploy.AtPoint(healSpells, _healPoint, waveDelay: waveDelay))
                    yield return t;
            }

            while (byLine.Any(u => u.Count > 0))
            {
                foreach (var unit in byLine.Where(u => u.Count > 0))
                {
                    Log.Info($"[Breakthrough] Deploying {unit.PrettyName} x{unit.Count}");
                    foreach (
                        var t in
                            Deploy.AlongLine(unit, _attackLine.Item1, _attackLine.Item2, unit.Count, 4,
                                waveDelay: waveDelay))
                        yield return t;
                }
            }

            while (byPoint.Any(u => u.Count > 0))
            {
                var deployError = false;

                foreach (var unit in byPoint.Where(u => u.Count > 0))
                {
                    var count = unit.Count;

                    Log.Info($"[Breakthrough] Deploying {unit.PrettyName} x{unit.Count}");
                    foreach (var t in Deploy.AtPoint(unit, _orgin, unit.Count, waveDelay: waveDelay))
                        yield return t;

                    // prevent infinite loop if deploy point is on red
                    if (unit.Count != count) continue;

                    Log.Warning($"[Breakthrough] Couldn't deploy {unit.PrettyName}");
                    deployError = true;
                    break;
                }
                if (deployError) break;
            }

            if (clanCastle?.Count > 0)
            {
                Log.Info($"[Breakthrough] Deploying {clanCastle.PrettyName}");
                foreach (var t in Deploy.AtPoint(clanCastle, _orgin, waveDelay: waveDelay))
                    yield return t;
            }

            if (heroes.Any())
            {
                foreach (var hero in heroes.Where(u => u.Count > 0))
                {
                    Log.Info($"[Breakthrough] Deploying {hero.PrettyName}");
                    foreach (var t in Deploy.AtPoint(hero, _orgin, waveDelay: waveDelay))
                        yield return t;
                }
            }

            if (healers?.Count > 0)
            {
                Log.Info($"[Breakthrough] Deploying {healers.PrettyName} x{healers.Count}");
                foreach (var t in Deploy.AtPoint(healers, _healerPoint, healers.Count, waveDelay: waveDelay))
                    yield return t;
            }

            if (hogs?.Count > 0)
            {
                Log.Info($"[Breakthrough] Deploying {hogs.PrettyName} x{hogs.Count}");
                foreach (var t in Deploy.AtPoint(hogs, _orgin, hogs.Count, waveDelay: waveDelay))
                    yield return t;
            }

            Deploy.WatchHeroes(heroes);

            // get freeze spells
            _freezeSpell = spells.ExtractOne(u => u.Id == DeployId.Freeze);

            // no freeze spells so end deployment
            if (!(_freezeSpell?.Count > 0)) yield break;

            // find and watch inferno towers
            var infernos = InfernoTower.Find();

            foreach (var inferno in infernos)
            {
                inferno.FirstActivated += DropFreeze;

                inferno.StartWatching();
            }
        }

        private void CreateDeployPoints(bool qw)
        {
            var target =    TownHall.Find()?.Location.GetCenter() ?? 
                            DarkElixirStorage.Find().FirstOrDefault()?.Location.GetCenter() ??
                            new PointFT(0, 0);

            // don't include corners in case build huts are there
            var maxRedPointX = GameGrid.RedPoints.Where(p => -18 < p.Y && p.Y < 18)?.Max(point => point.X) + 1 ?? GameGrid.RedZoneExtents.MaxX;
            var minRedPointX = GameGrid.RedPoints.Where(p => -18 < p.Y && p.Y < 18)?.Min(point => point.X) - 1 ?? GameGrid.RedZoneExtents.MinX;
            var maxRedPointY = GameGrid.RedPoints.Where(p => -18 < p.X && p.X < 18)?.Max(point => point.Y) + 1 ?? GameGrid.RedZoneExtents.MaxY;
            var minRedPointY = GameGrid.RedPoints.Where(p => -18 < p.X && p.X < 18)?.Min(point => point.Y) - 1 ?? GameGrid.RedZoneExtents.MinY;

            // build a box around the base
            var left =      new PointFT(minRedPointX, maxRedPointY);
            var top =       new PointFT(maxRedPointX, maxRedPointY);
            var right =     new PointFT(maxRedPointX, minRedPointY);
            var bottom =    new PointFT(minRedPointX, minRedPointY);

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

            _orgin = new Container<PointFT> {Item = orginPoints.OrderBy(point => point.DistanceSq(target)).First()};

            if (_orgin.Item.X > _core.X)
            {
                Log.Info("[Breakthrough] Attacking from the top right");

                var redLinePoint = GameGrid.RedPoints
                    .Where(point => point.Y > -10 && point.Y < 10)?
                    .Max(point => point.X) ?? GameGrid.RedZoneExtents.MaxX;

                if (qw)
                {
                    _qwPoint = right;
                    _queenRagePoint = new PointFT(right.X - 5, right.Y + 5);
                    _healerPoint = new PointFT(24f, -24f);
                    _attackLine = new Tuple<PointFT, PointFT>(top, _orgin.Item.Midpoint(right));
                }
                else
                {
                    _attackLine = new Tuple<PointFT, PointFT>(top, right);
                    _healerPoint = new PointFT(24f, _core.Y);
                }
                _healPoint = new PointFT(redLinePoint - 12f, _core.Y);
                _ragePoint = new PointFT(redLinePoint - 9f, _core.Y);
            }
            else if (_orgin.Item.X < _core.X)
            {
                Log.Info("[Breakthrough] Attacking from the bottom left");

                var redLinePoint = GameGrid.RedPoints
                    .Where(point => point.Y > -10 && point.Y < 10)?
                    .Min(point => point.X) ?? GameGrid.RedZoneExtents.MinX;

                if (qw)
                {
                    _qwPoint = left;
                    _queenRagePoint = new PointFT(left.X + 5, left.Y - 5);
                    _healerPoint = new PointFT(-24f, 24f);
                    _attackLine = new Tuple<PointFT, PointFT>(bottom, _orgin.Item.Midpoint(left));
                }
                else
                {
                    _healerPoint = new PointFT(-24f, _core.Y);
                    _attackLine = new Tuple<PointFT, PointFT>(bottom, left);
                }
                _healPoint = new PointFT(redLinePoint + 12, _core.Y);
                _ragePoint = new PointFT(redLinePoint + 9, _core.Y);
            }
            else if (_orgin.Item.Y > _core.Y)
            {
                Log.Info("[Breakthrough] Attacking from the top left");

                var redLinePoint = GameGrid.RedPoints
                    .Where(point => point.X > -10 && point.X < 10)?
                    .Max(point => point.Y) ?? GameGrid.RedZoneExtents.MaxY;

                if (qw)
                {
                    _qwPoint = left;
                    _queenRagePoint = new PointFT(left.X + 5f, left.Y - 5f);
                    _healerPoint = new PointFT(-24f, 24f);
                    _attackLine = new Tuple<PointFT, PointFT>(top, _orgin.Item.Midpoint(left));
                }
                else
                {
                    _healerPoint = new PointFT(_core.X, 24f);
                    _attackLine = new Tuple<PointFT, PointFT>(left, top);
                }
                _healPoint = new PointFT(_core.X, redLinePoint - 12f);
                _ragePoint = new PointFT(_core.X, redLinePoint - 9f);
            }
            else // (orgin.Y < core.Y)
            {
                Log.Info("[Breakthrough] Attacking from the bottom right");

                var redLinePoint = GameGrid.RedPoints
                    .Where(point => point.X > -10 && point.X < 10)?
                    .Min(point => point.Y) ?? GameGrid.RedZoneExtents.MinY;

                if (qw)
                {
                    _qwPoint = right;
                    _queenRagePoint = new PointFT(right.X - 5, right.Y + 5);
                    _healerPoint = new PointFT(24f, -24f);
                    _attackLine = new Tuple<PointFT, PointFT>(bottom, _orgin.Item.Midpoint(right));
                }
                else
                {
                    _healerPoint = new PointFT(_core.X, -24f);
                    _attackLine = new Tuple<PointFT, PointFT>(right, bottom);
                }
                _healPoint = new PointFT(_core.X, redLinePoint + 12);
                _ragePoint = new PointFT(_core.X, redLinePoint + 9);
            }
        }

        private void VisualizeDeployment()
        {
            using (var bmp = Screenshot.Capture())
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    // find the radius of 5 tiles
                    var p1 = new PointFT(0f, 0f).ToScreenAbsolute();
                    var p2 = new PointFT(0f, 5f).ToScreenAbsolute();
                    var distance = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

                    Visualize.RectangleT(bmp, _border, new Pen(Color.FromArgb(128, Color.Red)));

                    g.DrawLine(new Pen(Color.FromArgb(192, Color.Red)), _attackLine.Item1.ToScreenAbsolute(), _attackLine.Item2.ToScreenAbsolute());

                    //foreach (PointFT point in _tankPoints)
                    //    Visualize.RectangleT(bmp, new RectangleT((int)point.X, (int)point.Y, 1, 1), new Pen(Color.Orange));

                    Visualize.RectangleT(bmp, new RectangleT((int)_qwPoint.X, (int)_qwPoint.Y, 1, 1), new Pen(Color.Blue));

                    Visualize.RectangleT(bmp, new RectangleT((int)_core.X, (int)_core.Y, 1, 1), new Pen(Color.Purple));

                    g.FillEllipse(new SolidBrush(Color.FromArgb(128, Color.Gold)),
                        _healPoint.ToScreenAbsolute().ToRectangle((int)distance, (int)distance));

                    g.FillEllipse(new SolidBrush(Color.FromArgb(128, Color.Magenta)),
                        _ragePoint.ToScreenAbsolute().ToRectangle((int)distance, (int)distance));

                    g.FillEllipse(new SolidBrush(Color.FromArgb(128, Color.Magenta)),
                        _queenRagePoint.ToScreenAbsolute().ToRectangle((int)distance, (int)distance));
                }
                var d = DateTime.UtcNow;
                Screenshot.Save(bmp, $"Breakthrough Deploy {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}");
            }
        }
    }
}
