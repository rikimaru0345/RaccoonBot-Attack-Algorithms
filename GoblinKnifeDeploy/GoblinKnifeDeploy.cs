using System;
using System.Collections.Generic;
using System.Threading;
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
        #region my variables "class members"
        private RectangleT _border;
        private Container<PointFT> _orgin;
        private Tuple<PointFT, PointFT> _attackLine;
        private PointFT _core, _earthQuakePoint, _healPoint, _ragePoint, _target, _jumpPoint;
        private bool _useJump = false, watchHeroes = false;
        private int _delay = 2000;
        private DeployElement _freezeSpell;
        private const string Version = "1.0.6.47";
        #endregion

        #region drop freeze function 

        /// <summary>
        /// whatch Inforno to drop FreezSpell on
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="a"></param>
        private void DropFreeze(object sender, EventArgs a)
        {
            var inferno = (InfernoTower)sender;

            foreach (var t in Deploy.AtPoint(_freezeSpell, inferno.Location.GetCenter()))
                Thread.Sleep(t);

            inferno.StopWatching();
        }
        #endregion

        #region create depoly points for troops and spells
        /// <summary>
        /// create depoly points for troops and spells
        /// </summary>
        private void CreateDeployPoints()
        {
            float getOutRedArea = 0.25f;

            // don't include corners in case build huts are there
            float maxRedPointX = (GameGrid.RedPoints.Where(p => -18 < p.Y && p.Y < 18)?.Max(point => point.X) + 1 ?? GameGrid.RedZoneExtents.MaxX)+ getOutRedArea;
            float minRedPointX = (GameGrid.RedPoints.Where(p => -18 < p.Y && p.Y < 18)?.Min(point => point.X) - 1 ?? GameGrid.RedZoneExtents.MinX)- getOutRedArea;
            float maxRedPointY = (GameGrid.RedPoints.Where(p => -18 < p.X && p.X < 18)?.Max(point => point.Y) + 1 ?? GameGrid.RedZoneExtents.MaxY)+ getOutRedArea;
            float minRedPointY = (GameGrid.RedPoints.Where(p => -18 < p.X && p.X < 18)?.Min(point => point.Y) - 1 ?? GameGrid.RedZoneExtents.MinY)- getOutRedArea;
            // build a box around the base
            PointFT left = new PointFT(minRedPointX, maxRedPointY);
            PointFT top = new PointFT(maxRedPointX, maxRedPointY);
            PointFT right = new PointFT(maxRedPointX, minRedPointY);
            PointFT bottom = new PointFT(minRedPointX, minRedPointY);


            // border around the base
            _border = new RectangleT((int)minRedPointX, (int)maxRedPointY, (int)(maxRedPointX - minRedPointX), (int)(minRedPointY - maxRedPointY));

            // core is center of the box
            _core = _border.GetCenter();

            PointFT[] orginPoints = new[]
            {
                new PointFT(maxRedPointX, _core.Y),
                new PointFT(minRedPointX, _core.Y),
                new PointFT(_core.X, maxRedPointY),
                new PointFT(_core.X, minRedPointY)
            };

            _orgin = new Container<PointFT> { Item = orginPoints.OrderBy(point => point.DistanceSq(_target)).First() };
            #region top right
            if (_orgin.Item.X > _core.X)
            {
                Log.Info("[Goblin Knife] Attacking from the top right");
                
                _attackLine = new Tuple<PointFT, PointFT>(top, right);

                _earthQuakePoint = new PointFT(_target.X + 5.5f, _core.Y);
                _jumpPoint = new PointFT(_target.X + 5f, _core.Y);
                _healPoint = new PointFT(_orgin.Item.X - 13f, _core.Y);
                _ragePoint = new PointFT(_orgin.Item.X - 9f, _core.Y);
            }
            #endregion

            #region bottom left
            else if (_orgin.Item.X < _core.X)
            {
                Log.Info("[Goblin Knife] Attacking from the bottom left");
                
                _attackLine = new Tuple<PointFT, PointFT>(bottom, left);

                _earthQuakePoint = new PointFT(_target.X - 5.5f, _core.Y);
                _jumpPoint = new PointFT(_target.X - 5f, _core.Y);
                _healPoint = new PointFT(_orgin.Item.X + 13f, _core.Y);
                _ragePoint = new PointFT(_orgin.Item.X + 9f, _core.Y);
            }
            #endregion

            #region top left
            else if (_orgin.Item.Y > _core.Y)
            {
                Log.Info("[Goblin Knife] Attacking from the top left");
                
                _attackLine = new Tuple<PointFT, PointFT>(left, top);

                _earthQuakePoint = new PointFT(_core.X, _target.Y + 5.5f);
                _jumpPoint = new PointFT(_core.X, _target.Y + 5f);
                _healPoint = new PointFT(_core.X, _orgin.Item.Y - 13f);
                _ragePoint = new PointFT(_core.X, _orgin.Item.Y - 9f);
            }
            #endregion

            #region bottom right
            else // (orgin.Y < core.Y)
            {
                Log.Info("[Goblin Knife] Attacking from the bottom right");
               
                _attackLine = new Tuple<PointFT, PointFT>(right, bottom);

                _earthQuakePoint = new PointFT(_core.X, _target.Y - 5.5f);
                _jumpPoint = new PointFT(_core.X, _target.Y - 5f);
                _healPoint = new PointFT(_core.X, _orgin.Item.Y + 13f);
                _ragePoint = new PointFT(_core.X, _orgin.Item.Y + 9f);
            }
            #endregion
        }
        #endregion

        #region Deployment troops and spells
        public override IEnumerable<int> AttackRoutine()
        {
            var DE = DarkElixirStorage.Find()?.FirstOrDefault()?.Location.GetCenter();
            //set the target
            if (DE == null)
            {
                for (var i = 1; i <= 3; i++)
                {
                    Log.Warning($"bot didn't found the DE Storage .. we will attemp search NO. {i + 1}");
                    yield return 1000;
                    DE = DarkElixirStorage.Find()?.FirstOrDefault()?.Location.GetCenter();
                    if (DE != null)
                    {
                        Log.Warning($"DE Storage found after {i + 1} retries");
                        break;
                    }

                }
            }

            if (DE != null)
                _target = (PointFT)DE;
            else
            {
                Log.Debug("[Goblin Knife] coundn't locate the target after aligning the base");
                Log.Error("Couldn't find DE Storage we will return home");
                Surrender();

                yield break;
            }
            CreateDeployPoints();
            Log.Info($"[Goblin Knife] V{Version} Deploy start");

            //get troops
            var deployElements = Deploy.GetTroops();

            var clanCastle = deployElements.ExtractOne(u => u.ElementType == DeployElementType.ClanTroops && UserSettings.UseClanTroops);

            var earthQuakeSpell = deployElements.Extract(u => u.Id == DeployId.Earthquake);
            var jumpSpell = deployElements.ExtractOne(DeployId.Jump);
            var giant = deployElements.ExtractOne(DeployId.Giant);
            var goblin = deployElements.ExtractOne(DeployId.Goblin);
            var wizard = deployElements.ExtractOne(DeployId.Wizard);
            var barbarian = deployElements.ExtractOne(DeployId.Barbarian);
            var archer = deployElements.ExtractOne(DeployId.Archer);
            var wallbreaker = deployElements.ExtractOne(DeployId.WallBreaker);
            var ragespell = deployElements.ExtractOne(DeployId.Rage);
            var healspell = deployElements.ExtractOne(DeployId.Heal);
            _freezeSpell = deployElements.ExtractOne(DeployId.Freeze);

            var heroes = deployElements
                .Extract(u => (UserSettings.UseKing && u.ElementType == DeployElementType.HeroKing)
                    || (UserSettings.UseQueen && u.ElementType == DeployElementType.HeroQueen)
                    || (UserSettings.UseWarden && u.ElementType == DeployElementType.HeroWarden))
                .ToList();
            
            //open near to dark elixer with 4 earthquakes
            if (earthQuakeSpell?.Sum(u => u.Count) >= 4)
            {
                foreach(var unit in earthQuakeSpell)
                {
                    foreach (int t in Deploy.AtPoint(unit, _earthQuakePoint, unit.Count))
                    {
                        yield return t;
                    }
                }
            }
            else
            {
                _useJump = true;
            }
            yield return 1000;
            if (giant?.Count > 0)
            {
                foreach (int t in Deploy.AlongLine(giant, _attackLine.Item1, _attackLine.Item2, 6, 6))
                {
                    yield return t;
                }
            }

            yield return 1000;

            if (wizard?.Count > 0)
            {
                foreach (int t in Deploy.AlongLine(wizard, _attackLine.Item1, _attackLine.Item2, 8, 4))
                {
                    yield return t;
                }
            }

            if(barbarian?.Count >0)
            {
                while (barbarian.Count > 0)
                {
                    int count = barbarian.Count;

                    Log.Info($"[Goblin Knife] Deploying {barbarian.PrettyName}");
                    foreach (int t in Deploy.AlongLine(barbarian, _attackLine.Item1, _attackLine.Item2, count, 4))
                    {
                        yield return t;
                    }

                    // prevent infinite loop if deploy point is on red
                    if (barbarian.Count != count) continue;

                    Log.Warning($"[Goblin Knife] Couldn't deploy {barbarian.PrettyName}");
                    break;
                }
            }
            
            if (archer?.Count > 0)
            {
                int archerCount = (int)(archer.Count/2);
                Log.Info($"[Goblin Knife] Deploying {archer.PrettyName} ");
                foreach (int t in Deploy.AlongLine(archer, _attackLine.Item1, _attackLine.Item2, archerCount, 4))
                {
                    yield return t;
                }
            }

            yield return 3000;

            if (ragespell?.Count >= 2)
            {
                foreach (int t in Deploy.AtPoint(ragespell, _ragePoint))
                    yield return t;
            }
            if(wallbreaker?.Count>0)
            {
                Log.Info($"[Goblin Knife] send test {wallbreaker.PrettyName} to check for bombs");
                foreach (int t in Deploy.AtPoint(wallbreaker, _orgin, 1))
                    yield return t;
            }

            yield return 1000;
            while (wallbreaker?.Count > 0)
            {
                int count = wallbreaker.Count;
                Log.Info("[Goblin Knife] send wall breakers in groups");
                foreach (int t in Deploy.AtPoint(wallbreaker, _orgin, 3))
                    yield return t;

                // prevent infinite loop if deploy point is on red
                if (wallbreaker.Count != count) continue;

                Log.Warning($"[Goblin Knife] Couldn't deploy {wallbreaker.PrettyName}");
                break;
            }

            while (giant?.Count > 0)
            {
                int count = giant.Count;

                Log.Info($"[Goblin Knife] Deploying {giant.PrettyName} x{count}");
                foreach (int t in Deploy.AtPoint(giant, _orgin, count))
                    yield return t;

                // prevent infinite loop if deploy point is on red
                if (giant.Count != count) continue;

                Log.Warning($"[Goblin Knife] Couldn't deploy {giant.PrettyName}");
                break;
            }

            yield return 1000;

            while (wizard?.Count > 0)
            {
                int count = wizard.Count;

                Log.Info($"[Goblin Knife] Deploying {wizard}");
                foreach (int t in Deploy.AlongLine(wizard, _attackLine.Item1, _attackLine.Item2, 4, 2))
                    yield return t;

                // prevent infinite loop if deploy point is on red
                if (wizard.Count != count) continue;

                Log.Warning($"[Goblin Knife] Couldn't deploy {wizard.PrettyName}");
                break;
            }
            if (archer?.Count > 0)
            {
                Log.Info($"[Goblin Knife] Deploying {archer.PrettyName} ");
                foreach (int t in Deploy.AlongLine(archer, _attackLine.Item1, _attackLine.Item2, archer.Count, 4))
                {
                    yield return t;
                }
            }

            yield return 1500;
            if (_useJump == true && jumpSpell?.Count > 0)
            {
                foreach (int t in Deploy.AtPoint(jumpSpell, _jumpPoint))
                    yield return t;
            }

            if (healspell?.Count > 0)
            {
                foreach (int t in Deploy.AtPoint(healspell, _healPoint))
                    yield return t;
            }

            if (clanCastle?.Count > 0)
            {
                Log.Info($"[Goblin Knife] Deploying {clanCastle.PrettyName}");
                foreach (int t in Deploy.AtPoint(clanCastle, _orgin))
                    yield return t;
            }

            if (heroes.Any())
            {
                _delay = 1000;
                foreach (DeployElement hero in heroes.Where(u => u.Count > 0))
                {
                    foreach (int t in Deploy.AtPoint(hero, _orgin))
                        yield return t;
                }
                watchHeroes = true;
            }
            yield return _delay;

            while (goblin?.Count > 0)
            {
                int testGoblins = (int)(goblin.Count / 3);
                Log.Info($"[Goblin Knife] Deploying {goblin.PrettyName} x{testGoblins}");
                
                foreach (int t in Deploy.AtPoint(goblin, _orgin, testGoblins))
                    yield return t;

                yield return 2000;

                int count = goblin.Count;

                Log.Info($"[Goblin Knife] Deploying {goblin.PrettyName} x{count}");
                foreach (int t in Deploy.AtPoint(goblin, _orgin, count))
                    yield return t;

                // prevent infinite loop if deploy point is on red
                if (goblin.Count != count) continue;

                Log.Warning($"[Goblin Knife] Couldn't deploy {goblin.PrettyName}");
                break;
            }
            //use freeze if inferno is found
            if (_freezeSpell?.Count > 0)
            {
                var infernos = InfernoTower.Find();
                // find and watch inferno towers
                if (infernos != null)
                {
                    foreach (var inferno in infernos)
                    {
                        inferno.FirstActivated += DropFreeze;

                        inferno.StartWatching();
                    }
                }
            }
            yield return 200;

            foreach (int t in Deploy.AtPoint(healspell, _target))
                yield return t;

            foreach (int t in Deploy.AtPoint(ragespell, _target))
                yield return t;
            if(watchHeroes == true)
            {
                Deploy.WatchHeroes(heroes, 7000);
            }
        }
        #endregion

        public GoblinKnifeDeploy(Opponent opponent) : base(opponent)
        {
        }

        #region override ShouldAccept() to check if we fing our tharget or not
        public override double ShouldAccept()
        {
            if (Opponent.MeetsRequirements(BaseRequirements.All))
            {
                Log.Debug("[Goblin Knife] searching for DE Storage ....");
                var DE = DarkElixirStorage.Find()?.FirstOrDefault()?.Location.GetCenter();
                if(DE == null)
                {
                    Log.Debug("Couldn't found DE Storage .. we will skip this base");
                    Log.Error("Counld not locate DE Storage .. skipping this base");
                    return 0;
                }else
                {
                    Log.Debug("[Goblin Knife] Found DE storage .. move to CreateDeployPoints Method");
                    return 1;
                }
            }
            return 0;
        }
        #endregion

        #region override attack name through ToString function
        public override string ToString()
        {
            return "Goblin Knife Deploy";
        }
        #endregion
    }
}