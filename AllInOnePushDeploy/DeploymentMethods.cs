using System.Collections.Generic;
using System.Linq;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using System;

namespace AllInOnePushDeploy
{
    class DeploymentMethods
    {
        public static bool useJump, watchHeroes = false, watchQueen = false, isWarden = false, dragonAttack, babyLoon, lavaloonion;
        public static DeployElement golem, giant, queen, bowler, witch, wizard, wallbreaker, healer, freezeSpell, clanCastle, warden, balloon, dragon, babyDragon, lava, minion, lightingSpell;
        public static List<DeployElement> rageSpell, healSpell, hasteSpell, jumpSpell, poison, eq, heroes, spells;
        static int bowlerFunnelCount, healerFunnlCount, witchFunnelCount;

        public static IEnumerable<int> DropEQ()
        {
            var EQCount = eq?.Sum(u => u.Count);
            if (EQCount >= 4)
            {
                Log.Info($"[{AllInOnePushDeploy.AttackName}] Use earthquakes spells to open walls near target."); ;
                foreach (var unit in eq)
                {
                    foreach (var t in Deploy.AtPoint(unit, AllInOnePushDeploy.EqPoint, unit.Count, 50))
                        yield return t;
                }
                yield return new Random().Next(2000, 4000);
            }
            else
            {
                useJump = true;
            }
        }

        public static IEnumerable<int> DeployInCustomOrder(List<int> order)
        {
            foreach (var o in order)
            {
                switch (o)
                {
                    case 1:
                        foreach (var s in DeployGolems())
                            yield return s;
                        break;
                    case 2:
                        foreach (var s in DeployFunnlling())
                            yield return s;
                        break;
                    case 3:
                        foreach (var s in DeployGiants())
                            yield return s;
                        break;
                    case 4:
                        foreach (var s in DeployHeroes())
                            yield return s;
                        break;
                    case 5:
                        foreach (var s in DeployWB())
                            yield return s;
                        break;
                    case 6:
                        foreach (var s in DeployNormalTroops())
                            yield return s;
                        break;
                }
            }
        }

        public static IEnumerable<int> DeployGolems()
        {
            if (golem?.Count >= 2)
            {
                Log.Info($"[{AllInOnePushDeploy.AttackName}] deploy Golems troops .. ");

                foreach (var t in Deploy.AlongLine(golem, AllInOnePushDeploy.AttackLine.Item1, AllInOnePushDeploy.AttackLine.Item2, golem.Count, golem.Count))
                    yield return t;

                if (AllInOnePushDeploy.ClanCastleSettings == 1)
                {
                    if (clanCastle?.Count > 0)
                    {
                        foreach (var t in Deploy.AtPoint(clanCastle, AllInOnePushDeploy.Origin)) 
                            yield return t;
                    }
                }

                yield return new Random().Next(800, 1500);

                var waves = wizard?.Count >= 12 ? 2 : 1;
                foreach (var f in DeployWizard(waves))
                    yield return f;
            }
            else if (golem?.Count == 1 && AllInOnePushDeploy.ClanCastleSettings == 1)
            {
                if (clanCastle?.Count > 0)
                {
                    foreach (var t in Deploy.AtPoint(golem, AllInOnePushDeploy.FirstFunnellingPoint, golem.Count))
                        yield return t;

                    foreach (var t in Deploy.AtPoint(clanCastle, AllInOnePushDeploy.SecondFunnellingPoint))
                        yield return t;

                    yield return 1000;
                }
            }
            else
            {
                if (AllInOnePushDeploy.ClanCastleSettings == 1)
                {
                    if(clanCastle?.Count > 0)
                    {
                        foreach (var t in Deploy.AtPoint(clanCastle, AllInOnePushDeploy.Origin))
                            yield return t;
                    }
                }
            }
        }

        public static IEnumerable<int> DeployFunnlling()
        {
            Log.Info($"[{AllInOnePushDeploy.AttackName}] deploy funnelling troops on sides");

            var QW = AllInOnePushDeploy.QWSettings == 1 && queen?.Count > 0 && healer?.Count >= AllInOnePushDeploy.HealerOnQWSettings ? true : false;
            
            if (QW)
            {
                Log.Info($"{AllInOnePushDeploy.AttackName} start queen walk");
                foreach (var t in Deploy.AtPoint(queen, AllInOnePushDeploy.FirstFunnellingPoint))
                    yield return t;

                yield return 400;

                foreach (var t in Deploy.AtPoint(healer, AllInOnePushDeploy.QWHealer, AllInOnePushDeploy.HealerOnQWSettings))
                    yield return t;

                Deploy.WatchHeroes(new List<DeployElement> { queen });

                if (AllInOnePushDeploy.RageOnQWSettings == 1)
                {

                    var rageCount = rageSpell?.Sum(u => u.Count);
                    if (rageCount > 0)
                    {
                        foreach (var unit in rageSpell)
                        {
                            unit.Select();
                            foreach (var t in Deploy.AtPoint(unit, AllInOnePushDeploy.QWRagePoint))
                                yield return t;
                        }
                    }
                }

                yield return 10000;

                if (bowler?.Count > 0)
                {
                    bowlerFunnelCount = bowler.Count / 4;
                    foreach (var t in Deploy.AtPoint(bowler, AllInOnePushDeploy.SecondFunnellingPoint, bowlerFunnelCount))
                        yield return t;
                }
                if (witch?.Count > 4)
                {
                    witchFunnelCount = witch.Count / 4;
                    foreach (var t in Deploy.AtPoint(witch, AllInOnePushDeploy.SecondFunnellingPoint, witchFunnelCount))
                        yield return t;
                }

                if (healer?.Count > 0)
                {
                    foreach (var t in Deploy.AtPoint(healer, AllInOnePushDeploy.SecondFunnellingPoint, healer.Count))
                        yield return t;
                }

                yield return 5000;
            }
            else
            {
                if (bowler?.Count > 0 || witch?.Count > 0)
                {
                    Log.Info($"{AllInOnePushDeploy.AttackName} start funnlling ");
                    if (bowler?.Count > 0)
                    {
                        bowlerFunnelCount = bowler.Count / 4;
                        foreach (var t in Deploy.AtPoint(bowler, AllInOnePushDeploy.FirstFunnellingPoint, bowlerFunnelCount))
                            yield return t;
                    }
                    if (witch?.Count > 0)
                    {
                        witchFunnelCount = witch.Count > 4 ? witch.Count / 4 : witch.Count / 2;
                        foreach (var t in Deploy.AtPoint(witch, AllInOnePushDeploy.FirstFunnellingPoint, witchFunnelCount))
                            yield return t;
                    }

                    if (healer?.Count >= 2)
                    {
                        healerFunnlCount = healer.Count <= 4 ? healer.Count / 2 : healer.Count / 3;
                        foreach (var t in Deploy.AtPoint(healer, AllInOnePushDeploy.FirstFunnellingPoint, healerFunnlCount))
                            yield return t;
                    }

                    if (bowler?.Count > 0)
                    {
                        foreach (var t in Deploy.AtPoint(bowler, AllInOnePushDeploy.SecondFunnellingPoint, bowlerFunnelCount))
                            yield return t;
                    }
                    if (witchFunnelCount > 0 && witch?.Count > 0)
                    {
                        foreach (var t in Deploy.AtPoint(witch, AllInOnePushDeploy.SecondFunnellingPoint, witchFunnelCount))
                            yield return t;
                    }

                    if (healer?.Count > 0 && healerFunnlCount > 0)
                    {
                        foreach (var t in Deploy.AtPoint(healer, AllInOnePushDeploy.SecondFunnellingPoint, healerFunnlCount))
                            yield return t;
                    }

                    yield return new Random().Next(10000, 13000);
                }
            }
        }

        public static IEnumerable<int> DeployGiants()
        {

            var jumpSpellCount = jumpSpell?.Sum(u => u.Count) > 0 ? jumpSpell.Sum(u => u.Count) : 0;
            if ((useJump && jumpSpellCount >= 2) || (!useJump && jumpSpellCount >= 1))
            {
                foreach (var unit in jumpSpell)
                {
                    foreach (var t in Deploy.AtPoint(unit, AllInOnePushDeploy.FirstJumpPoint))
                        yield return t;
                }
            }

            if (giant?.Count > 0)
            {
                Log.Info($"[{AllInOnePushDeploy.AttackName}] deploy Giants ...");
                foreach (var t in Deploy.AlongLine(giant, AllInOnePushDeploy.FirstFunnellingPoint, AllInOnePushDeploy.SecondFunnellingPoint, 8, 4))
                    yield return t;

                var waves = wizard?.Count >= 8 ? 2 : 1;
                foreach (var f in DeployWizard(waves))
                    yield return f;

                foreach (var f in DeployWB())
                    yield return f;

                foreach (var t in Deploy.AtPoint(giant, AllInOnePushDeploy.Origin, giant.Count))
                    yield return t;
            }

            if (clanCastle?.Count > 0 && AllInOnePushDeploy.ClanCastleSettings == 2)
            {
                foreach (var t in Deploy.AtPoint(clanCastle, AllInOnePushDeploy.Origin))
                    yield return t;
            }

            //if one golem deploy after funnlling
            if (golem?.Count > 0)
            {
                Log.Info($"[{AllInOnePushDeploy.AttackName}] deploy Golem ...");
                foreach (var t in Deploy.AlongLine(golem, AllInOnePushDeploy.AttackLine.Item1, AllInOnePushDeploy.AttackLine.Item2, golem.Count, golem.Count))
                    yield return t;
            }
        }

        public static IEnumerable<int> DeployWizard(int waves = 1)
        {
            if (wizard?.Count > 0)
            {
                var count = wizard.Count / waves;
                foreach (var t in Deploy.AlongLine(wizard, AllInOnePushDeploy.FirstFunnellingPoint, AllInOnePushDeploy.SecondFunnellingPoint, count, 4))
                    yield return t;
            }
        }

        public static IEnumerable<int> DeployWB()
        {
            if (wallbreaker?.Count > 0)
            {
                Log.Info($"[{AllInOnePushDeploy.AttackName}] droping wallBreakers");
                while (wallbreaker?.Count > 0)
                {
                    var count = wallbreaker.Count;
                    foreach (var t in Deploy.AtPoint(wallbreaker, AllInOnePushDeploy.Origin, 3))
                        yield return t;

                    yield return 400;
                    // prevent infinite loop if deploy point is on red
                    if (wallbreaker.Count != count) continue;

                    Log.Warning($"[{AllInOnePushDeploy.AttackName}] Couldn't deploy {wallbreaker.PrettyName}");
                    break;
                }
            }
        }

        public static IEnumerable<int> DeployHeroes()
        {
            yield return new Random().Next(600, 1000);

            Log.Info($"[{AllInOnePushDeploy.AttackName}] droping heroes");
            if (heroes.Any())
            {
                foreach (var hero in heroes.Where(u => u.Count > 0))
                {
                    foreach (var t in Deploy.AtPoint(hero, AllInOnePushDeploy.Origin))
                        yield return t;
                }
                watchHeroes = true;
            }

            if (queen?.Count > 0)
            {
                foreach (var t in Deploy.AtPoint(queen, AllInOnePushDeploy.Origin))
                    yield return t;
                watchQueen = true;
            }
            if (warden?.Count > 0)
            {
                foreach (var t in Deploy.AtPoint(warden, AllInOnePushDeploy.Origin))
                    yield return t;
                isWarden = true;
            }
            else
                isWarden = false;
        }

        public static IEnumerable<int> DeployNormalTroops()
        {
            Log.Info($"[{AllInOnePushDeploy.AttackName}] deploy rest of troops");

            if (witch?.Count > 4)
            {
                if (bowler?.Count > 0)
                {
                    foreach (var t in Deploy.AlongLine(bowler, AllInOnePushDeploy.FirstFunnellingPoint, AllInOnePushDeploy.SecondFunnellingPoint, bowlerFunnelCount, 4))
                        yield return t;
                }
            }


            if (bowler?.Count > 0)
            {
                foreach (var t in Deploy.AtPoint(bowler, AllInOnePushDeploy.Origin, bowler.Count))
                    yield return t;
            }
            if (witch?.Count > 0)
            {
                foreach (var t in Deploy.AtPoint(witch, AllInOnePushDeploy.Origin, witch.Count))
                    yield return t;
            }
            if (clanCastle?.Count > 0)
            {
                Log.Info($"[{AllInOnePushDeploy.AttackName}] Deploying {clanCastle.PrettyName}");
                foreach (var t in Deploy.AtPoint(clanCastle, AllInOnePushDeploy.Origin))
                    yield return t;
            }

            if (healer?.Count > 0)
            {
                foreach (var t in Deploy.AtPoint(healer, AllInOnePushDeploy.Origin, healer.Count))
                    yield return t;
            }

            foreach (var unit in AllInOnePushDeploy.deployElements)
            {
                Log.Info($"[{AllInOnePushDeploy.AttackName}] deploy any remaining troops");
                if (unit?.Count > 0)
                {
                    if (unit.IsRanged)
                    {
                        foreach (var t in Deploy.AlongLine(unit, AllInOnePushDeploy.FirstFunnellingPoint, AllInOnePushDeploy.SecondFunnellingPoint, unit.Count, 4))
                            yield return t;
                    }
                    else
                    {
                        foreach (var t in Deploy.AtPoint(unit, AllInOnePushDeploy.Origin, unit.Count))
                            yield return t;
                    }
                }
            }

            foreach (var w in DeployWizard())
                yield return w;

            foreach (var w in DeployUnusedTroops())
                yield return w;
        }

        public static IEnumerable<int> DeployUnusedTroops()
        {
            // Check unit bar for unused troops
            Log.Warning($"{AllInOnePushDeploy.AttackName} search for unused troops !!");
            var unusedTroops = Deploy.GetTroops();
            var spell = unusedTroops.Extract(DeployElementType.Spell);
            if (unusedTroops.Sum(u => u.Count) > 0)
            {
                foreach (var u in unusedTroops)
                {
                    if (u?.Count > 0)
                        Log.Warning($"we found {u.Count}x {u.PrettyName}");
                }

                Log.Info($"[{AllInOnePushDeploy.AttackName}] deploy unused troops");
                foreach (var unit in unusedTroops)
                {
                    if (unit?.Count > 0)
                    {
                        if (unit.IsRanged)
                        {
                            foreach (var t in Deploy.AlongLine(unit, AllInOnePushDeploy.FirstFunnellingPoint, AllInOnePushDeploy.SecondFunnellingPoint, unit.Count, 4))
                                yield return t;
                        }
                        else
                        {
                            foreach (var t in Deploy.AtPoint(unit, AllInOnePushDeploy.Origin, unit.Count))
                                yield return t;
                        }
                    }
                }
            }
            else
            {
                Log.Info($"[{AllInOnePushDeploy.AttackName}] all Troops have been deployed");
            }
        }

        public static IEnumerable<int>  DeployJump()
        {
            if (useJump && jumpSpell.Sum(u => u.Count) > 0)
            {
                Log.Info($"[{AllInOnePushDeploy.AttackName}] deploy jump next to Townhall");
                foreach (var unit in jumpSpell)
                {
                    unit.Select();
                    foreach (var t in Deploy.AtPoint(unit, AllInOnePushDeploy.SecondJumpPoint))
                        yield return t;
                    break;
                }
            }
        }

        public static IEnumerable<int> DeploySpell(List<DeployElement> spell, PointFT point)
        {
            if(spell.Sum(u => u.Count) > 0)
            {
                var unit = spell.FirstOrDefault().Count > 0 ? spell.FirstOrDefault() : spell.LastOrDefault();
                foreach (var t in Deploy.AtPoint(unit, point))
                    yield return t;
            }
        }

        // Air deploy methods

        public static IEnumerable<int> ZapAirDefense()
        {
            // Todo: use 3 lighting if th7.
            var airDefenses = AirDefense.Find(CacheBehavior.ForceScan);
            var targetAirDefense = airDefenses.OrderBy(a => a.Location.GetCenter().DistanceSq(AllInOnePushDeploy.Origin)).ElementAtOrDefault(2);
            if (targetAirDefense == null)
                targetAirDefense = airDefenses.OrderBy(a => a.Location.GetCenter().DistanceSq(AllInOnePushDeploy.Origin)).ElementAtOrDefault(1);
            if (targetAirDefense == null)
                targetAirDefense = airDefenses.FirstOrDefault();

            var zapPoint = targetAirDefense.Location.GetCenter();


            if (eq?.Sum(u => u.Count) > 0)
            {
                foreach (var unit in eq)
                {
                    foreach (var t in Deploy.AtPoint(unit, zapPoint))
                        yield return t;
                    break;
                }
            }

            foreach (var t in Deploy.AtPoint(lightingSpell, zapPoint, 2))
                yield return t;

            yield return new Random().Next(1200, 2500);
        }

        public static IEnumerable<int> DeployInCustomOrderAir(List<int> order)
        {
            foreach (var o in order)
            {
                switch (o)
                {
                    case 7:
                        foreach (var s in DeployBalloons())
                            yield return s;
                        break;
                    case 8:
                        foreach (var s in DeployBabyDragons())
                            yield return s;
                        break;
                    case 9:
                        foreach (var s in DeployLava())
                            yield return s;
                        break;
                    case 10:
                        foreach (var s in DeployDragons())
                            yield return s;
                        break;
                    case 11:
                        foreach (var s in AirFunnelling())
                            yield return s;
                        break;
                    default:
                        break;
                }
            }
        }

        public static IEnumerable<int> AirFunnelling()
        {
            if (dragon?.Count > 0)
            {
                foreach (var t in Deploy.AtPoint(dragon, AllInOnePushDeploy.FirstFunnellingPoint))
                    yield return t;

                foreach (var t in Deploy.AtPoint(dragon, AllInOnePushDeploy.SecondFunnellingPoint))
                    yield return t;

                yield return new Random().Next(8000, 9000);
            }
            else if (babyDragon?.Count > 0)
            {
                foreach (var t in Deploy.AtPoint(babyDragon, AllInOnePushDeploy.FirstFunnellingPoint))
                    yield return t;

                foreach (var t in Deploy.AtPoint(babyDragon, AllInOnePushDeploy.SecondFunnellingPoint))
                    yield return t;

                yield return new Random().Next(5000, 6500);
            }
        }

        public static IEnumerable<int> DeployDragons()
        {
            if (dragon?.Count > 0)
            {
                foreach (var t in Deploy.AlongLine(dragon, AllInOnePushDeploy.FirstFunnellingPoint, AllInOnePushDeploy.SecondFunnellingPoint, dragon.Count, 4))
                    yield return t;
            }
        }

        public static IEnumerable<int> DeployBabyDragons()
        {
            if (babyDragon?.Count > 0)
            {
                foreach (var t in Deploy.AlongLine(babyDragon, AllInOnePushDeploy.FirstFunnellingPoint, AllInOnePushDeploy.SecondFunnellingPoint, babyDragon.Count, 4, 50))
                    yield return t;
            }
        }

        public static IEnumerable<int> DeployBalloons()
        {
            yield return dragonAttack ? 2000 : (babyLoon ? 500 : 0);
            if (balloon?.Count > 0)
            {
                if (!dragonAttack)
                {
                    foreach (var t in Deploy.AlongLine(balloon, AllInOnePushDeploy.AttackLine.Item1, AllInOnePushDeploy.AttackLine.Item2, balloon.Count, 4))
                        yield return t;
                }
                else
                {
                    var count = balloon.Count / 2;
                    foreach (var t in Deploy.AtPoint(balloon, AllInOnePushDeploy.FirstFunnellingPoint, count))
                        yield return t;

                    foreach (var t in Deploy.AtPoint(balloon, AllInOnePushDeploy.SecondFunnellingPoint, count))
                        yield return t;
                }
            }
        }

        public static IEnumerable<int> DeployLava()
        {
            if (lava?.Count >= 2)
            {
                var count = lava.Count / 2;

                foreach (var t in Deploy.AtPoints(lava, new PointFT[] { AllInOnePushDeploy.FirstFunnellingPoint, AllInOnePushDeploy.SecondFunnellingPoint }, count, 0, 200, 5))
                    yield return t;

                if (lava?.Count > 0)
                {
                    foreach (var t in Deploy.AtPoint(lava, AllInOnePushDeploy.SecondFunnellingPoint, lava.Count))
                        yield return t;
                }

                if (clanCastle?.Count > 0 && AllInOnePushDeploy.ClanCastleSettings > 0)
                {
                    foreach (var t in Deploy.AtPoint(clanCastle, AllInOnePushDeploy.FirstFunnellingPoint))
                        yield return t;
                }
            }
            else
            {
                if (clanCastle?.Count > 0 && AllInOnePushDeploy.ClanCastleSettings > 0)
                {
                    foreach (var t in Deploy.AtPoint(clanCastle, AllInOnePushDeploy.FirstFunnellingPoint))
                        yield return t;

                    foreach (var t in Deploy.AtPoint(lava, AllInOnePushDeploy.SecondFunnellingPoint))
                        yield return t;
                }
                else
                {
                    foreach (var t in Deploy.AtPoint(lava, AllInOnePushDeploy.Origin))
                        yield return t;
                }
            }
        }

        public static IEnumerable<int> DeployMinions()
        {
            if (minion?.Count > 0)
            {
                foreach (var t in Deploy.AlongLine(minion, AllInOnePushDeploy.AttackLine.Item1, AllInOnePushDeploy.AttackLine.Item2, minion.Count, 4))
                    yield return t;
            }
        }
    }
}
