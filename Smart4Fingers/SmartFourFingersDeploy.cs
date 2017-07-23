using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using CustomAlgorithmSettings;
using System.Reflection;

[assembly: Addon("SmartFourFingersDeploy", "deploy troops in for sides with human behavior", "Cobratst")]
namespace SmartFourFingersDeploy
{
    [AttackAlgorithm("SmartFourFingersDeploy", "Four Fingers Deploy with advanced settings")]
    public class SmartFourFingersDeploy : BaseAttack
    {
        internal static readonly Version Version = Assembly.GetEntryAssembly().GetName().Version;
        internal static readonly string FormattedVersionString = $"{Version.Major}.{Version.Minor}.{Version.Build}.{Version.Revision}";
        const string AttackName = "[Smart 4 Fingers Deploy]";
        PointFT target;

        public SmartFourFingersDeploy(Opponent opponent) : base(opponent)
        {
        }

        public override string ToString()
        {
            return "Smart 4 Fingers Deploy";
        }

        /// <summary>
        /// Returns a Custom Setting's Current Value.  The setting Name must be defined in the DefineSettings Function for this algorithm.
        /// </summary>
        /// <param name="settingName">Name of the setting to Get</param>
        /// <returns>Current Value of the setting.</returns>
        internal int GetCurrentSetting(string settingName)
        {
            return SettingsController.Instance.GetSetting(AttackName, settingName, Opponent.IsDead());
        }

        /// <summary>
        /// Returns a list of all current Algorithm Setting Values.
        /// </summary>
        /// <returns>Current Value of the all settings for this algorithm.</returns>
        internal List<AlgorithmSetting> AllCurrentSettings
        {
            get
            {
                return SettingsController.Instance.AllAlgorithmSettings[AttackName].AllSettings;
            }
        }

        /// <summary>
        /// Called from the Bot Framework when the Algorithm is first loaded into memory.
        /// </summary>
        public static void OnInit()
        {
            // On load of the Plug-In DLL, Define the Default Settings for the Algorithm.
            SettingsController.Instance.DefineCustomAlgorithmSettings(DefineSettings());
        }

        internal static AlgorithmSettings DefineSettings()
        {
            var settings = new AlgorithmSettings();

            settings.AlgorithmName = AttackName;
            //settings.AlgorithmDescriptionURL = "https://www.raccoonbot.com/forum/topic/24589-dark-push-deploy/";

            // Global Settings.
            var debugMode = new AlgorithmSetting("Debug Mode", "When on, Debug Images will be written out for each attack showing what the algorithm is seeing.", 0, SettingType.Global);
            debugMode.PossibleValues.Add(new SettingOption("Off", 0));
            debugMode.PossibleValues.Add(new SettingOption("On", 1));
            settings.DefineSetting(debugMode);

            var setCollMines = new AlgorithmSetting("Set Exposed Collecotors & Mines", "turn on and off searching for outside elixir collectors and gold mines.", 1, SettingType.ActiveAndDead);
            setCollMines.PossibleValues.Add(new SettingOption("Off", 0));
            setCollMines.PossibleValues.Add(new SettingOption("On", 1));
            settings.DefineSetting(setCollMines);

            // Show These ONLY when Set Exposed Collecotors & Mines is on.
            var minDistance = new AlgorithmSetting("Acceptable Target Range", "the maximun numbers of tiles the collectors and drills can be far from red line", 6, SettingType.ActiveAndDead);
            minDistance.MinValue = 2;
            minDistance.MaxValue = 10;
            minDistance.HideInUiWhen.Add(new SettingOption("Set Exposed Collecotors & Mines", 0));
            settings.DefineSetting(minDistance);

            var minimElixir = new AlgorithmSetting("Minimum Exposed Colloctors", "Minimum Elixir Colloctores found outside before attack", 3, SettingType.ActiveAndDead);
            minimElixir.MinValue = 0;
            minimElixir.MaxValue = 7;
            minimElixir.HideInUiWhen.Add(new SettingOption("Set Exposed Collecotors & Mines", 0));
            settings.DefineSetting(minimElixir);

            var minimGold = new AlgorithmSetting("Minimum Exposed Mines", "Minimum Gold Mines found outside before attack", 3, SettingType.ActiveAndDead);
            minimGold.MinValue = 0;
            minimGold.MaxValue = 7;
            minimGold.HideInUiWhen.Add(new SettingOption("Set Exposed Collecotors & Mines", 0));
            settings.DefineSetting(minimGold);
            

            var useSmartZapDrills = new AlgorithmSetting("Smart Zap Drills", "use lighting Drills with smart way to save lighting spells if no need to use (please disable default Lighting drills if you select this option)", 0, SettingType.ActiveAndDead);
            useSmartZapDrills.PossibleValues.Add(new SettingOption("Off", 0));
            useSmartZapDrills.PossibleValues.Add(new SettingOption("On", 1));
            settings.DefineSetting(useSmartZapDrills);

            // Show These ONLY when Smart Zap Drills is on.
            var startZapAfter = new AlgorithmSetting("Start Zap Drills After ?(sec)", "change when bot start to use smart zap , this time start from deployment is done with all troops", 30, SettingType.ActiveAndDead);
            startZapAfter.MinValue = 10;
            startZapAfter.MaxValue = 60;
            startZapAfter.HideInUiWhen.Add(new SettingOption("Smart Zap Drills", 0));
            settings.DefineSetting(startZapAfter);

            var minDrillLvl = new AlgorithmSetting("Min Drill Level", "select minimum level of the drill to be zapped", 3, SettingType.ActiveAndDead);
            minDrillLvl.MinValue = 1;
            minDrillLvl.MaxValue = 6;
            minDrillLvl.HideInUiWhen.Add(new SettingOption("Smart Zap Drills", 0));
            settings.DefineSetting(minDrillLvl);

            var minDEAmount = new AlgorithmSetting("Min Dark Elixir per Zap", "we will zap only drills that have more than this amount of DE.", 200, SettingType.ActiveAndDead);
            minDEAmount.MinValue = 100;
            minDEAmount.MaxValue = 600;
            minDEAmount.HideInUiWhen.Add(new SettingOption("Smart Zap Drills", 0));
            settings.DefineSetting(minDEAmount);

            var endBattleAfterZap = new AlgorithmSetting("End Battle after zap ?(sec)", "end battle after this time in sec after Smart Zap is done (0 is disabled)", 10, SettingType.ActiveAndDead);
            endBattleAfterZap.MinValue = 0;
            endBattleAfterZap.MaxValue = 60;
            endBattleAfterZap.HideInUiWhen.Add(new SettingOption("Smart Zap Drills", 0));
            settings.DefineSetting(endBattleAfterZap);

            var deployHeroesAt = new AlgorithmSetting("Deploy Heroes At", "choose where to deploy Heroes", 0, SettingType.ActiveAndDead);
            deployHeroesAt.PossibleValues.Add(new SettingOption("Normal (at the end)", 0));
            deployHeroesAt.PossibleValues.Add(new SettingOption("TownHall Side", 1));
            deployHeroesAt.PossibleValues.Add(new SettingOption("DE storage Side", 1));
            settings.DefineSetting(deployHeroesAt);


            return settings;
        }

        /// <summary>
        /// Called by the Bot Framework when This algorithm Row is selected in Attack Options tab
        /// to check to see whether or not this algorithm has Advanced Settings/Options
        /// </summary>
        public static bool ShowAdvancedSettingsButton()
        {
            return true;
        }
        /// <summary>
        /// Called when the Advanced button is clicked in the Bot UI with this algorithm Selected.
        /// </summary>
        public static void OnAdvancedSettingsButtonClicked()
        {
            // Show the Settings Dialog for this Algorithm.
            SettingsController.Instance.ShowSettingsWindow(AttackName);
        }

        /// <summary>
        /// Called from the Bot Framework when the bot is closing.
        /// </summary>
        public static void OnShutdown()
        {
            // Save settings for this algorithm.
            SettingsController.Instance.SaveAlgorithmSettings(AttackName);
        }

        // Get next item from list
        Tuple<PointFT, PointFT> NextOf(List<Tuple<PointFT, PointFT>> list, Tuple<PointFT, PointFT> item)
        {
            return list[(list.IndexOf(item) + 1) == list.Count ? 0 : (list.IndexOf(item) + 1)];
        }

        //TODO: use IsEngineeredBase to attack engineered bases even if it hasen't outside collectors and mines
        bool IsEngineeredBase()
        {
            var defenses = ArcherTower.Find()?.Count();
            defenses += WizardTower.Find()?.Count();
            defenses += AirDefense.Find().Count();

            if (defenses <= 3)
                return true;

            return false;
        }

        public override IEnumerable<int> AttackRoutine()
        {
            int waveLimit = UserSettings.WaveSize;
            int waveDelay = (int)(UserSettings.WaveDelay * 1000);
            int heroesIndex = -1;

            
            Random rnd = new Random();

            var core = new PointFT(-0.01f, 0.01f);

            // Points to draw lines in deploy extends area.
            var topLeft = new PointFT((float)GameGrid.MaxX - 2, (float)GameGrid.DeployExtents.MaxY);
            var topRight = new PointFT((float)GameGrid.DeployExtents.MaxX, (float)GameGrid.MaxY - 2);

            var rightTop = new PointFT((float)GameGrid.DeployExtents.MaxX, (float)GameGrid.MinY + 2);
            var rightBottom = new PointFT((float)GameGrid.MaxX - 2, (float)GameGrid.DeployExtents.MinY);

            // Move 8 tiles from bottom corner due to unitsbar.
            var bottomLeft = new PointFT((float)GameGrid.DeployExtents.MinX, (float)GameGrid.MinY + 8);
            var bottomRight = new PointFT((float)GameGrid.MinX + 8, (float)GameGrid.DeployExtents.MinY);

            var leftTop = new PointFT((float)GameGrid.MinX + 2, (float)GameGrid.DeployExtents.MaxY);
            var leftBottom = new PointFT((float)GameGrid.DeployExtents.MinX, (float)GameGrid.MaxY - 2);

            var linesPointsList = new List<PointFT>
            {
                topLeft, topRight,
                rightTop, rightBottom,
                bottomLeft, bottomRight,
                leftBottom, leftTop
            };

            // Main four lines of attack.
            var topRightLine = new Tuple<PointFT, PointFT>(topRight, rightTop);
            var bottomRightLine = new Tuple<PointFT, PointFT>(bottomRight, rightBottom);
            var bottomLeftLine = new Tuple<PointFT, PointFT>(bottomLeft, leftBottom);
            var topLeftLine = new Tuple<PointFT, PointFT>(topLeft, leftTop);

            // List of the four attack lines in clocwise order
            var attackLines = new List<Tuple<PointFT, PointFT>>
            {
                topLeftLine,
                topRightLine,
                bottomRightLine,
                bottomLeftLine
            };

            var deployHeroesAt = GetCurrentSetting("Deploy Heroes At");

            // deployHeroesAt -> 1 = townhall, 2 = DEstorage, 0 = normal behavior (after last line of troops).
            if (deployHeroesAt == 1)
            {
                var th = TownHall.Find()?.Location.GetCenter();
                if (th == null)
                {
                    for (var i = 0; i < 3; i++)
                    {
                        Log.Warning($"bot didn't found the TownHall .. we will attemp search NO. {i + 2}");
                        yield return 1000;
                        th = TownHall.Find(CacheBehavior.ForceScan)?.Location.GetCenter();
                        if (th != null)
                        {
                            Log.Warning($"TownHall found after {i + 2} retries");
                            target = (PointFT)th;
                            deployHeroesAt = 1;
                            break;
                        }
                        else
                            deployHeroesAt = 0;  
                    }
                }
                else
                    target = (PointFT)th;
            }
            else if(deployHeroesAt == 2)
            {
                var de = DarkElixirStorage.Find()?.FirstOrDefault()?.Location.GetCenter();
                if (de == null)
                {
                    for (var i = 0; i < 3; i++)
                    {
                        Log.Warning($"bot didn't found the DE Storage .. we will attemp search NO. {i + 2}");
                        yield return 1000;
                        de = DarkElixirStorage.Find(CacheBehavior.ForceScan)?.FirstOrDefault()?.Location.GetCenter();
                        if (de != null)
                        {
                            Log.Warning($"DE Storage found after {i + 2} retries");
                            target = (PointFT)de;
                            deployHeroesAt = 2;
                            break;
                        }
                        else
                            deployHeroesAt = 0;
                    }
                }
                else
                    target = (PointFT)de;
            }
            else
            {
                target = new PointFT(0f, 0f);
                deployHeroesAt = 0;
            }

            if(deployHeroesAt != 0)
            {
                var nearestRedPointToTarget = GameGrid.RedPoints.OrderBy(p => p.DistanceSq(target)).FirstOrDefault();
                var nearestLinePoint = linesPointsList.OrderBy(p => p.DistanceSq(nearestRedPointToTarget)).FirstOrDefault();

                heroesIndex = attackLines.FindIndex(u => (u.Item1.X == nearestLinePoint.X && u.Item1.Y == nearestLinePoint.Y) || (u.Item2.X == nearestLinePoint.X && u.Item2.Y == nearestLinePoint.Y));
            }

            var units = Deploy.GetTroops();
            var heroes = units.Extract(x => x.IsHero);
            var cc = units.ExtractOne(u => u.ElementType == DeployElementType.ClanTroops);
            var spells = units.Extract(u => u.ElementType == DeployElementType.Spell);

            units.OrderForDeploy();

            // Get random line from attackLines list.
            int index = rnd.Next(attackLines.Count);

            // Set first attack line (IF user didn't define deploy point for heros -> rendom ELSE -> start from the next line, SO it ends with the user defined line).
            var line = heroesIndex == -1 ? attackLines[index] : NextOf(attackLines, attackLines[heroesIndex]);
            index = attackLines.FindIndex(u => u.Item1.X == line.Item1.X && u.Item1.Y == line.Item1.Y);

            // Start troops deployment on 1st line.
            Log.Info($"{AttackName} deploy 1st group of troops");
            var count = 0;
            foreach (var unit in units)
            {
                if (unit?.Count > 0)
                {
                    count = unit.Count / 4;
                    var fingers = count < 4 ? count : 4;
                    foreach (var t in Deploy.AlongLine(unit, line.Item1, line.Item2, count, fingers, 50, waveDelay))
                        yield return t;
                    yield return waveDelay;
                }
            }

            line = NextOf(attackLines, attackLines[index]);
            index = attackLines.FindIndex(u => u.Item1.X == line.Item1.X && u.Item1.Y == line.Item1.Y);

            // Start troops deployment on 2ed line.
            Log.Info($"{AttackName} deploy 2ed group of troops");

            foreach (var unit in units)
            {
                if (unit?.Count > 0)
                {
                    count = unit.Count / 3;
                    var fingers = count < 4 ? count : 4;
                    foreach (var t in Deploy.AlongLine(unit, line.Item1, line.Item2, count, fingers, 50, waveDelay))
                        yield return t;
                    yield return waveDelay;
                }
            }
            
            line = NextOf(attackLines, attackLines[index]);
            index = attackLines.FindIndex(u => u.Item1.X == line.Item1.X && u.Item1.Y == line.Item1.Y);

            // Start troops deployment on 3rd line.
            Log.Info($"{AttackName} deploy 3rd group of troops");

            foreach (var unit in units)
            {
                if (unit?.Count > 0)
                {
                    count = unit.Count / 2;
                    var fingers = count < 4 ? count : 4;
                    foreach (var t in Deploy.AlongLine(unit, line.Item1, line.Item2, count, fingers, 50, waveDelay))
                        yield return t;
                    yield return waveDelay;
                }
            }

            line = NextOf(attackLines, attackLines[index]);

            // Start troops deployment on last line.
            Log.Info($"{AttackName} deploy last group of troops");

            foreach (var unit in units)
            {
                if (unit?.Count > 0)
                {
                    count = unit.Count;
                    var fingers = count < 4 ? count : 4;
                    foreach (var t in Deploy.AlongLine(unit, line.Item1, line.Item2, count, fingers, 50, waveDelay))
                        yield return t;
                    yield return waveDelay;
                }
            }

            if (cc?.Count > 0)
            {
                Log.Info($"{AttackName} Deploy Clan Castle troops");
                foreach (var t in Deploy.AlongLine(cc, line.Item1, line.Item2, 1, 1, 50, waveDelay))
                    yield return t;
            }

            if (heroes.Any())
            {
                Log.Info($"{AttackName} Deploy Heroes");
                foreach (var hero in heroes.Where(u => u.Count > 0))
                {
                    foreach (var t in Deploy.AlongLine(hero, line.Item1, line.Item2, 1, 1, 50, waveDelay))
                        yield return t;
                }
                Deploy.WatchHeroes(heroes,5000);
            }

            IEnumerable<int> smartZap(int DEAmount, int lvl)
            {
                Log.Info($"{AttackName} Smart Zap Drills module");
                bool zapDrill = true;
                var minDEAmount = GetCurrentSetting("Min Dark Elixir per Zap");

                var zap = spells.Extract(u => u.Id == DeployId.Lightning);
                var zapCount = zap?.Sum(u => u.Count);

                if (zapCount <= 0)
                {
                    Log.Error($"{AttackName} Smart Zap Drills No lighting Spells found for Smart Zap");
                    zapDrill = false;
                    foreach (var t in endBattle())
                        yield return t;
                    yield break;
                }

                var drills = DarkElixirDrill.Find(CacheBehavior.ForceScan, lvl);

                if (drills == null)
                {
                    Log.Error("{AttackName} Smart Zap Drills didn't found Dark Drills matches the requirements");
                    zapDrill = false;
                    foreach (var t in endBattle())
                        yield return t;
                    yield break;
                }

                var availableDE = Opponent.GetAvailableLoot(false).DarkElixir;
                var availableDEAfterZap = 0;

                if (availableDE < DEAmount)
                {
                    Log.Error($"{AttackName} Smart Zap Drills this base only has {availableDE} DE .. it doesn't match the requirements ({minDEAmount})");
                    zapDrill = false;
                    foreach (var t in endBattle())
                        yield return t;
                    yield break;
                }

                if (zapDrill)
                {
                    Log.Info($"{AttackName} Smart Zap Drills found {zap.Sum(u => u.Count)} Lighting Spell(s)");
                    Log.Info($"{AttackName} Smart Zap Drills found {drills.Count()} Dark drill(s)");

                    // Zap each drill only twice beacuse (level 4 lighting will got 90% DE from max drill level)
                    for (var j = 1; j <= 2; j++) 
                    {
                        for (var i = 0; i < drills.Count(); i++)
                        {
                            if (drills[i] != null && zapCount > 0)
                            {
                                // Get location of each drill
                                var DP = drills[i].Location.GetCenter();

                                // If we have our own lighting we will drop it first .. if we don't, use CC "beacuse IsClanSpell not working if only CC spell"
                                var zp = zap.FirstOrDefault().Count > 0 ? zap.FirstOrDefault() : zap.LastOrDefault();

                                foreach (var t in Deploy.AtPoint(zp, DP, 1))
                                    yield return t;

                                yield return 6000;

                                zapCount--;

                                availableDEAfterZap = Opponent.GetAvailableLoot(false).DarkElixir;
                                if (availableDE - availableDEAfterZap < DEAmount)
                                {
                                    Log.Warning($"{AttackName} Smart Zap Drills only {availableDE - availableDEAfterZap} DE from this drill .. you set it to {minDEAmount} .. will not zap it again ");
                                    drills[i] = null;
                                }
                                else
                                {
                                    Log.Info($"{AttackName} Smart Zap Drills gain {availableDE - availableDEAfterZap} DE from this drill");
                                    availableDE = availableDEAfterZap;
                                }
                            }
                            
                            if (zapCount <= 0)
                                break;
                        }
                        yield return 4000;

                        drills = DarkElixirDrill.Find(CacheBehavior.ForceScan, lvl);
                        if (!drills.Any())
                        {
                            Log.Warning("no other drills to zap");
                            break;
                        }
                    }
                    foreach (var t in endBattle())
                        yield return t;
                    
                    yield break;
                }
            }

            IEnumerable<int> endBattle()
            {
                var endBattleTime = GetCurrentSetting("End Battle after zap ?(sec)");

                if (endBattleTime > 0)
                {
                    Log.Info($"end battle after {endBattleTime} sec");

                    for (var i = endBattleTime; i > 0; i--)
                    {
                        Log.Info($"{AttackName} end battle after {i} sec");
                        yield return 1000;
                    }
                    Attack.Surrender();
                }
            }

            if (GetCurrentSetting("Smart Zap Drills") == 1)
            {
                var waitBeforeSmartZap = GetCurrentSetting("Start Zap Drills After ?(sec)") * 1000;
                var minDEAmount = GetCurrentSetting("Min Dark Elixir per Zap");
                var minDEDrillLevel = GetCurrentSetting("Min Drill Level");

                yield return waitBeforeSmartZap;

                foreach (var t in smartZap(minDEAmount, minDEDrillLevel))
                    yield return t;
            }
        }

        /// <summary>
        /// check to see how many collector and mine near the redline
        /// </summary>
        /// <returns></returns>
        bool IsBaseWithOutsideCollectorsAndMines()
        {
            if(GetCurrentSetting("Set Exposed Collecotors & Mines") == 1)
            {
                var userDistance = GetCurrentSetting("Acceptable Target Range");
                var distance = userDistance * userDistance;

                var redPoints = GameGrid.RedPoints.Where(
                    point =>
                    !(point.X > 18 && point.Y > 18 || point.X > 18 && point.Y < -18 || point.X < -18 && point.Y > 18 ||
                    point.X < -18 && point.Y < -18));

                var collectors = ElixirCollector.Find().Where(c => c.Location.GetCenter().DistanceSq(redPoints.OrderBy(p => p.DistanceSq(c.Location.GetCenter())).FirstOrDefault()) <= distance);
                var mines = GoldMine.Find().Where(c => c.Location.GetCenter().DistanceSq(redPoints.OrderBy(p => p.DistanceSq(c.Location.GetCenter())).FirstOrDefault()) <= distance);

                int collectorsCount = collectors != null ? collectors.Count() : 0;
                int minesCount = mines != null ? mines.Count() : 0;

                Log.Warning($"{AttackName} NO. of Colloctors & mines near from red line:");
                Log.Warning($"elixir colloctors is {collectorsCount}");
                Log.Warning($"gold mines is {minesCount}");
                Log.Warning($"----------------------------");
                Log.Warning($"sum of all is {collectorsCount + minesCount}");

                var debug = GetCurrentSetting("Debug Mode");

                if (debug == 1)
                {
                    using (Bitmap bmp = Screenshot.Capture())
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            foreach (var c in collectors)
                            {
                                var point = c.Location.GetCenter();
                                Visualize.RectangleT(bmp, new RectangleT((int)point.X, (int)point.Y, 2, 2), new Pen(Color.Blue));
                            }


                            foreach (var c in mines)
                            {
                                var point = c.Location.GetCenter();
                                Visualize.RectangleT(bmp, new RectangleT((int)point.X, (int)point.Y, 2, 2), new Pen(Color.White));
                            }
                        }
                        var d = DateTime.UtcNow;
                        Screenshot.Save(bmp, "Collectors and Mines {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}");
                    }
                }

                var minCollectors = GetCurrentSetting("Minimum Exposed Colloctors");
                var minMines = GetCurrentSetting("Minimum Exposed Mines");
                
                if (collectorsCount >= minCollectors && minesCount >= minMines)
                    return true;
                else
                {
                    Log.Error($"{AttackName} this base doesn't meets Collocetors & Mines requirements");
                    return false;
                }
            }

            return true;
        }

        public override double ShouldAccept()
        {
            if (!Opponent.MeetsRequirements(BaseRequirements.All))
                return 0;

            if (!IsBaseWithOutsideCollectorsAndMines())
                return 0;

            return 1;
        }
    }
}
