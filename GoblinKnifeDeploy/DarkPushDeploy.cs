using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using CoC_Bot.Modules.Helpers;
using CoC_Bot;

namespace GoblinKnifeDeploy
{
    [AttackAlgorithm("DarkPushDeploy", "Bowler attack to push trophies")]
    internal class DarkPushDeploy : BaseAttack
    {
        RectangleT border;
        Container<PointFT> orgin;
        Tuple<PointFT, PointFT> attackLine;
        PointFT nearestWall, core, earthQuakePoint, healPoint, ragePoint, ragePoint2, target, jumpPoint, jumpPoint1, red1, red2;
        bool useJump = false;
        int bowlerFunnelCount, witchFunnelCount,healerFunnlCount;
        DeployElement freezeSpell;
        const string Version = "1.0.2.34";
        const string AttackName = "Dark Push Deploy";
        const float MinDistace = 18f;

        /// <summary>
        /// whatch Inforno to drop FreezSpell on
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="a"></param>
        void DropFreeze(object sender, EventArgs a)
        {
            var inferno = (InfernoTower)sender;

            foreach (var t in Deploy.AtPoint(freezeSpell, inferno.Location.GetCenter()))
                Thread.Sleep(t);

            inferno.StopWatching();
        }

        public DarkPushDeploy(Opponent opponent) : base(opponent)
        {
        }

        /// <summary>
        /// check if there are walls in the EQ area or not "still working on it"
        /// </summary>
        /// <param name="point">pointFT of EQ </param>
        /// <returns>true or false</returns>
        int GetMaxWallsInside(PointFT eqPoint, float eqRadius = 3.5f)
        {
            var walls = Wall.Find();
            var eqWalls = walls.Count(w => w.Location.X >= eqPoint.X - eqRadius && w.Location.X <= eqPoint.X + eqRadius && w.Location.Y >= eqPoint.Y - eqRadius && w.Location.Y <= eqPoint.Y + eqRadius && w.Location.GetCenter().DistanceSq(eqPoint) <= eqRadius * eqRadius);
            //var wall = Wall.Find().Where(w => w.Location.X >= eqPoint.X - eqRadius && w.Location.X <= eqPoint.X + eqRadius && w.Location.Y >= eqPoint.Y - eqRadius && w.Location.Y <= eqPoint.Y + eqRadius && w.Location.GetCenter().DistanceSq(eqPoint) <= eqRadius * eqRadius);
            return eqWalls;
        }

        /// <summary>
        /// create depoly points for troops and spells
        /// </summary>
        void CreateDeployPoints()
        {
            var th = TownHall.Find()?.Location.GetCenter();
            if (th != null)
            {
                target = (PointFT)th;
            }
            else
            {
                Log.Debug($"{AttackName} coundn't locate the TARGET after aligning the base");
                Log.Error("Couldn't find Townhall we will return home");
                Surrender();
            }


            var getOutRedArea = 0.5f;

            // don't include corners in case build huts are there
            var maxRedPointX = (GameGrid.RedPoints.Where(p => -18 < p.Y && p.Y < 18)?.Max(point => point.X)  ?? GameGrid.RedZoneExtents.MaxX) + getOutRedArea;
            var minRedPointX = (GameGrid.RedPoints.Where(p => -18 < p.Y && p.Y < 18)?.Min(point => point.X)  ?? GameGrid.RedZoneExtents.MinX) - getOutRedArea;
            var maxRedPointY = (GameGrid.RedPoints.Where(p => -18 < p.X && p.X < 18)?.Max(point => point.Y)  ?? GameGrid.RedZoneExtents.MaxY) + getOutRedArea;
            var minRedPointY = (GameGrid.RedPoints.Where(p => -18 < p.X && p.X < 18)?.Min(point => point.Y)  ?? GameGrid.RedZoneExtents.MinY) - getOutRedArea;
            // build a box around the base
            var left = new PointFT(minRedPointX, maxRedPointY);
            var top = new PointFT(maxRedPointX, maxRedPointY);
            var right = new PointFT(maxRedPointX, minRedPointY);
            var bottom = new PointFT(minRedPointX, minRedPointY);

            // border around the base
            border = new RectangleT((int)minRedPointX, (int)maxRedPointY, (int)(maxRedPointX - minRedPointX), (int)(minRedPointY - maxRedPointY));

            // core is center of the box
            core = border.GetCenter();

            var orginPoints = new[]
            {
                new PointFT(maxRedPointX, core.Y),
                new PointFT(minRedPointX, core.Y),
                new PointFT(core.X, maxRedPointY),
                new PointFT(core.X, minRedPointY)
            };

            orgin = new Container<PointFT> { Item = orginPoints.OrderBy(point => point.DistanceSq(target)).First() };


            if (orgin.Item.X > core.X)
            {
                Log.Info($"[{AttackName}] Attacking from the top right");

                attackLine = new Tuple<PointFT, PointFT>(top, right);
                
                var distance = orgin.Item.X - this.target.X;
                var target = distance >= MinDistace ? this.target : core;

                var wallsToTarget = Wall.Find().Where(w => ((int)w.Location.GetCenter().Y == (int)orgin.Item.Y) || ((int)w.Location.GetCenter().Y + 1 == (int)orgin.Item.Y) || (int)w.Location.GetCenter().Y - 1 == (int)orgin.Item.X);
                nearestWall = orgin.Item;
                if (wallsToTarget?.Count() > 0)
                    nearestWall = wallsToTarget.OrderByDescending(w => w.Location.GetCenter().X).First().Location.GetCenter();

                var earthQuakePoints = new List<PointFT>();
                var jumpPoints = new List<PointFT>();
                
                var maxX = nearestWall.X - 5f;
                var start = target.X + 4f;
                while ( maxX > start)
                {
                    earthQuakePoints.Add(new PointFT(start, core.Y));
                    jumpPoints.Add(new PointFT(start - 0.5f, core.Y));
                    start += 0.25f;
                }

                earthQuakePoint = earthQuakePoints.OrderByDescending(e => GetMaxWallsInside(e)).FirstOrDefault();
                jumpPoint = jumpPoints.OrderByDescending(e => GetMaxWallsInside(e)).FirstOrDefault();

                jumpPoint1 = new PointFT(nearestWall.X - 3f, core.Y);
   
                ragePoint = new PointFT(orgin.Item.X - 9f, core.Y);
                healPoint = new PointFT(orgin.Item.X - 15f, core.Y);
                ragePoint2 = new PointFT(orgin.Item.X - 20f, core.Y);
            }

            else if (orgin.Item.X < core.X)
            {
                Log.Info($"[{AttackName}] Attacking from the bottom left");

                attackLine = new Tuple<PointFT, PointFT>(bottom, left);

                var distance = (orgin.Item.X - this.target.X) * -1;
                var target = distance >= MinDistace ? this.target : core;

                var wallsToTarget = Wall.Find().Where(w => ((int)w.Location.GetCenter().Y == (int)orgin.Item.Y) || ((int)w.Location.GetCenter().Y + 1 == (int)orgin.Item.Y) || (int)w.Location.GetCenter().Y - 1 == (int)orgin.Item.Y);
                //set default value to the nearst wall if there is no walls
                nearestWall = orgin.Item;
                if(wallsToTarget?.Count() > 0)
                    nearestWall = wallsToTarget.OrderBy(w => w.Location.GetCenter().X).First().Location.GetCenter();

                var earthQuakePoints = new List<PointFT>();
                var jumpPoints = new List<PointFT>();

                var maxX = nearestWall.X + 5f;
                var start = target.X - 4f;
                while (maxX < start)
                {
                    earthQuakePoints.Add(new PointFT(start, core.Y));
                    jumpPoints.Add(new PointFT(start + 0.5f, core.Y));
                    start -= 0.25f;
                }
                earthQuakePoint = earthQuakePoints.OrderByDescending(e => GetMaxWallsInside(e)).FirstOrDefault();
                jumpPoint = jumpPoints.OrderByDescending(e => GetMaxWallsInside(e)).FirstOrDefault();

                jumpPoint1 = new PointFT(nearestWall.X + 3f, core.Y);

                ragePoint = new PointFT(orgin.Item.X + 9f, core.Y);
                healPoint = new PointFT(orgin.Item.X + 15f, core.Y);
                ragePoint2 = new PointFT(orgin.Item.X + 20f, core.Y);
            }

            else if (orgin.Item.Y > core.Y)
            {
                Log.Info($"[{AttackName}] Attacking from the top left");

                attackLine = new Tuple<PointFT, PointFT>(left, top);

                var distance = orgin.Item.Y - this.target.Y;
                var target = distance >= MinDistace ? this.target : core;

                var wallsToTarget = Wall.Find().Where(w => ((int)w.Location.GetCenter().X == (int)orgin.Item.X) || ((int)w.Location.GetCenter().X + 1 == (int)orgin.Item.X) || (int)w.Location.GetCenter().X - 1 == (int)orgin.Item.X);
                nearestWall = orgin.Item;
                if(wallsToTarget?.Count() > 0)
                    nearestWall = wallsToTarget.OrderByDescending(w => w.Location.GetCenter().Y).First().Location.GetCenter();

                var earthQuakePoints = new List<PointFT>();
                var jumpPoints = new List<PointFT>();

                var maxX = nearestWall.Y - 5f;
                var start = target.Y + 4f;
                while (maxX > start)
                {
                    earthQuakePoints.Add(new PointFT(core.X, start));
                    jumpPoints.Add(new PointFT(core.X, start - 0.5f));
                    start += 0.25f;
                }
                
                earthQuakePoint = earthQuakePoints.OrderByDescending(e => GetMaxWallsInside(e)).FirstOrDefault();
                jumpPoint = jumpPoints.OrderByDescending(e => GetMaxWallsInside(e)).FirstOrDefault();

                jumpPoint1 = new PointFT(core.X, nearestWall.Y - 3f);

                ragePoint = new PointFT(core.X, orgin.Item.Y - 9f);
                healPoint = new PointFT(core.X, orgin.Item.Y - 15f);
                ragePoint2 = new PointFT(core.X, orgin.Item.Y - 20f);
            }

            else // (orgin.Y < core.Y)
            {
                Log.Info($"[{AttackName}] Attacking from the bottom right");

                attackLine = new Tuple<PointFT, PointFT>(right, bottom);

                var distance = (orgin.Item.Y - this.target.Y) * -1;
                var target = distance >= MinDistace ? this.target : core;

                var wallsToTarget = Wall.Find().Where(w => ((int)w.Location.GetCenter().X == (int)orgin.Item.X) || ((int)w.Location.GetCenter().X + 1 == (int)orgin.Item.X) || (int)w.Location.GetCenter().X - 1 == (int)orgin.Item.X);
                nearestWall = orgin.Item;
                if(wallsToTarget?.Count() > 0)
                    nearestWall = wallsToTarget.OrderBy(w => w.Location.GetCenter().Y).First().Location.GetCenter();

                var earthQuakePoints = new List<PointFT>();
                var jumpPoints = new List<PointFT>();

                var maxX = nearestWall.Y + 5f;
                var start = target.Y - 4f;
                while (maxX < start)
                {
                    earthQuakePoints.Add(new PointFT(core.X, start));
                    jumpPoints.Add(new PointFT(core.X, start + 0.5f));
                    start -= 0.25f;
                }

                earthQuakePoint = earthQuakePoints.OrderByDescending(e => GetMaxWallsInside(e)).FirstOrDefault();
                jumpPoint = jumpPoints.OrderByDescending(e => GetMaxWallsInside(e)).FirstOrDefault();

                jumpPoint1 = new PointFT(core.X, nearestWall.Y + 3f);

                ragePoint = new PointFT(core.X, orgin.Item.Y + 9f);
                healPoint = new PointFT(core.X, orgin.Item.Y + 15f);
                ragePoint2 = new PointFT(core.X, orgin.Item.Y + 20f);
            }

            //try to find better funneling points
            var frac = 0.65f;

            red1 = new PointFT(orgin.Item.X + frac * (attackLine.Item1.X - orgin.Item.X),
                         orgin.Item.Y + frac * (attackLine.Item1.Y - orgin.Item.Y));

            red2 = new PointFT(orgin.Item.X + frac * (attackLine.Item2.X - orgin.Item.X),
                         orgin.Item.Y + frac * (attackLine.Item2.Y - orgin.Item.Y));
            VisualizeDeployment();
        }

        public override IEnumerable<int> AttackRoutine()
        {
            CreateDeployPoints();
            Log.Info($"[{AttackName}] V{Version} Deploy start");

            //get troops (under respect of the user settings)
            var deployElements = Deploy.GetTroops();

            var clanCastle = deployElements.ExtractOne(u => u.ElementType == DeployElementType.ClanTroops);

            //spells
            var earthQuakeSpell = deployElements.ExtractOne(DeployId.Earthquake);
            var ragespell = deployElements.ExtractOne(DeployId.Rage);
            var healspell = deployElements.ExtractOne(DeployId.Heal);
            freezeSpell = deployElements.ExtractOne(DeployId.Freeze);
            var jumpSpell = deployElements.ExtractOne(DeployId.Jump);
            //tanks
            var giant = deployElements.ExtractOne(DeployId.Giant);
            var golem = deployElements.ExtractOne(DeployId.Golem);
            //main troops
            var wallbreaker = deployElements.ExtractOne(DeployId.WallBreaker);
            var valk = deployElements.ExtractOne(DeployId.Valkyrie);
            var bowler = deployElements.ExtractOne(DeployId.Bowler);
            var witch = deployElements.ExtractOne(DeployId.Witch);
            var healer = deployElements.ExtractOne(DeployId.Healer);
            var spells = deployElements.Extract(DeployElementType.Spell);


            var heroes = deployElements.Extract(x => x.IsHero);

            //get warden in a seperated member
            var warden = heroes.ExtractOne(u => u.ElementType == DeployElementType.HeroWarden);

            bool oneHealerDeployed = false;

            //open near to dark elixer with 4 earthquakes
            if (earthQuakeSpell?.Count >= 4)
            {
                Log.Info($"[{AttackName}] preak walls beside Twonhall ");
                foreach (var t in Deploy.AtPoint(earthQuakeSpell, earthQuakePoint, 4))
                    yield return t;
            }
            else
                useJump = true;

            if (useJump && jumpSpell?.Count >= 2)
            {
                foreach (var t in Deploy.AtPoint(jumpSpell, jumpPoint1))
                    yield return t;
            }
            yield return 1000;
            //deploy tanks
            Log.Info($"[{AttackName}] deploy tank troops .. ");
            if (golem?.Count > 0)
            {
                foreach (var t in Deploy.AlongLine(golem, attackLine.Item1, attackLine.Item2, golem.Count, golem.Count))
                    yield return t;
            }

            yield return 1000;

            //deploy funnelling bowlers behind each tank point
            Log.Info($"[{AttackName}] deploy funnelling troops on corners");
            if (bowler?.Count > 0)
            {
                bowlerFunnelCount = bowler.Count / 4;
                foreach (var t in Deploy.AtPoint(bowler, red1, bowlerFunnelCount))
                    yield return t;
            }
            if (witch?.Count > 0)
            {
                witchFunnelCount = witch.Count / 4;
                foreach (var t in Deploy.AtPoint(witch, red1, witchFunnelCount))
                    yield return t;
            }

            if (healer?.Count == 2)
            {
                foreach (var t in Deploy.AtPoint(healer, red1))
                    yield return t;
                oneHealerDeployed = true;
            }

            if (healer?.Count >= 4)
            {
                healerFunnlCount = healer.Count / 3;
                foreach (var t in Deploy.AtPoint(healer, red1, healerFunnlCount))
                    yield return t;
            }

            foreach (var t in Deploy.AtPoint(bowler, red2, bowlerFunnelCount))
                yield return t;

            foreach (var t in Deploy.AtPoint(witch, red2, witchFunnelCount))
                yield return t;

            if (healer?.Count == 1 && oneHealerDeployed)
            {
                foreach (var t in Deploy.AtPoint(healer, red2))
                    yield return t;
            }

            if (healer?.Count >= 3)
            {
                foreach (var t in Deploy.AtPoint(healer, red2, healerFunnlCount))
                    yield return t;
            }

            yield return 7000;

            if (giant?.Count > 0)
            {
                foreach (var t in Deploy.AlongLine(giant, red1, red2, giant.Count, 2))
                    yield return t;
            }
            Log.Info($"[{AttackName}] droping heroes");
            if (heroes.Any())
            {
                foreach (var hero in heroes.Where(u => u.Count > 0))
                {
                    foreach (var t in Deploy.AtPoint(hero, orgin))
                        yield return t;
                }
                Deploy.WatchHeroes(heroes);
            }
            if (warden?.Count > 0)
            {
                foreach (var t in Deploy.AtPoint(warden, orgin))
                    yield return t;
            }

            Log.Info($"[{AttackName}] droping wallBreakers");

            while (wallbreaker?.Count > 0)
            {
                var count = wallbreaker.Count;
                Log.Info($"[{AttackName}] send wall breakers in groups");
                foreach (var t in Deploy.AtPoint(wallbreaker, orgin, 3))
                    yield return t;

                yield return 800;
                // prevent infinite loop if deploy point is on red
                if (wallbreaker.Count != count) continue;

                Log.Warning($"[{AttackName}] Couldn't deploy {wallbreaker.PrettyName}");
                break;
            }

            Log.Info($"[{AttackName}] deploy rest of troops");
            if (bowler?.Count > 0)
            {
                foreach (var t in Deploy.AlongLine(bowler, red1, red2, bowlerFunnelCount, 4))
                    yield return t;
            }
            if (bowler?.Count > 0)
            {
                foreach (var t in Deploy.AtPoint(bowler, orgin, bowler.Count))
                    yield return t;
            }
            if (witch?.Count > 0)
            {
                foreach (var t in Deploy.AlongLine(witch, red1, red2, witch.Count, 4)) 
                    yield return t;
            }

            if (clanCastle?.Count > 0)
            {
                Log.Info($"[{AttackName}] Deploying {clanCastle.PrettyName}");
                foreach (var t in Deploy.AtPoint(clanCastle, orgin))
                    yield return t;
            }

            if (healer?.Count > 0)
            {
                foreach (var t in Deploy.AtPoint(healer, orgin, healer.Count))
                    yield return t;
            }

            foreach (var unit in deployElements)
            {
                Log.Info($"[{AttackName}] deploy any remaining troops");
                if (unit?.Count > 0)
                {
                    if (unit.IsRanged)
                    {
                        foreach (var t in Deploy.AlongLine(unit, red1, red2, unit.Count, 4))
                            yield return t;
                    }
                    else
                    {
                        foreach (var t in Deploy.AtPoint(unit, orgin, unit.Count))
                            yield return t;
                    }
                }
            }

            yield return 1000;
            Log.Info($"[{AttackName}] deploy jump next to Townhall");
            if (useJump && jumpSpell?.Count > 0)
            {
                foreach (var t in Deploy.AtPoint(jumpSpell, jumpPoint))
                    yield return t;
            }
            yield return 2000;
            
            //deploy spells
            foreach (var t in Deploy.AtPoint(ragespell, ragePoint))
                yield return t;

            yield return 3000;

            // activate Grand Warden apility
            if (warden?.Count > 0)
            {
                var heroList = new List<DeployElement> { warden };
                TryActivateHeroAbilities(heroList, true, 2000);
            }


            yield return 2000;
            foreach (var t in Deploy.AtPoint(healspell, healPoint))
                yield return t;

            if (ragespell?.Count >= 2)
            {
                foreach (var t in Deploy.AtPoint(ragespell, healPoint))
                    yield return t;
            }
            //use freeze if inferno is found
            if (freezeSpell?.Count > 0)
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

            yield return 4000;
            foreach (var t in Deploy.AtPoint(ragespell, ragePoint2))
                yield return t;
        }

        public override double ShouldAccept()
        {
            CreateDeployPoints();
            if (Opponent.MeetsRequirements(BaseRequirements.All))
            {
                Log.Debug($"[{AttackName}] searching for TownHall ....");
                var target = TownHall.Find()?.Location.GetCenter();
                if (target == null)
                {
                    Log.Debug("Couldn't found TH .. we will skip this base");
                    Log.Error("Counld not locate TownHall .. skipping this base");
                    return 0;
                }
                else
                {
                    Log.Debug($"[{AttackName}] Found TownHall .. move to CreateDeployPoints Method");
                    return 0;
                }
            }
            return 0;
        }

        public override string ToString()
        {
            return "Dark Push Deploy";
        }

        void VisualizeDeployment()
        {
            using (var bmp = Screenshot.Capture())
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    // find the radius of 5 tiles
                    var p1 = new PointFT(0f, 0f).ToScreenAbsolute();
                    var p2 = new PointFT(0f, 3.5f).ToScreenAbsolute();
                    var distance = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

                    Visualize.RectangleT(bmp, border, new Pen(Color.FromArgb(128, Color.Red)));

                    //g.DrawLine(new Pen(Color.FromArgb(192, Color.Orange)), attackLine.Item1.ToScreenAbsolute(), attackLine.Item2.ToScreenAbsolute());

                    //draw new deploy points for funnling troops
                    Visualize.RectangleT(bmp, new RectangleT((int)red1.X, (int)red1.Y, 1, 1), new Pen(Color.Blue));
                    Visualize.RectangleT(bmp, new RectangleT((int)red2.X, (int)red2.Y, 1, 1), new Pen(Color.Blue));

                    Visualize.RectangleT(bmp, new RectangleT((int)nearestWall.X, (int)nearestWall.Y, 1, 1), new Pen(Color.White));
                
                    Visualize.CircleT(bmp, earthQuakePoint, 4, Color.Brown, 128, 0);

                    Visualize.CircleT(bmp, jumpPoint, 3.5f, Color.DarkGreen, 130, 0);
                    Visualize.CircleT(bmp, jumpPoint1, 3.5f, Color.DarkGreen, 130, 0);

                    /*g.FillEllipse(new SolidBrush(Color.FromArgb(128, Color.Gold)),
                        healPoint.ToScreenAbsolute().ToRectangle((int)distance, (int)distance));

                    g.FillEllipse(new SolidBrush(Color.FromArgb(128, Color.Magenta)),
                        ragePoint.ToScreenAbsolute().ToRectangle((int)distance, (int)distance));

                    g.FillEllipse(new SolidBrush(Color.FromArgb(128, Color.Magenta)),
                        ragePoint2.ToScreenAbsolute().ToRectangle((int)distance, (int)distance));*/
                }
                var d = DateTime.UtcNow;
                Screenshot.Save(bmp, $"{AttackName} {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}");
            }
        }
    }
}
