using System;
using System.Collections.Generic;
using System.Linq;
using CoC_Bot.API;
using CustomAlgorithmSettings;
using System.Reflection;
using CoC_Bot.Modules.Helpers;

[assembly: Addon("SmartFourFingersDeploy", "Four Fingers Deploy with advanced settings", "CobraTST")]
namespace SmartFourFingersDeploy
{
    [AttackAlgorithm("SmartFourFingersDeploy", "Four Fingers Deploy with advanced settings")]
    public class SmartFourFingersDeploy : BaseAttack
    {
        internal static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        public const string AttackName = "[Smart 4 Fingers Deploy]";
        DateTime startTime;
        bool isZapped = false;

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
            var settings = new AlgorithmSettings()
            {
                AlgorithmName = AttackName,
                AlgorithmDescriptionURL = "https://www.raccoonbot.com/forum/topic/25606-smart-4-fingers-deploy/"
            };

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
            var minDistance = new AlgorithmSetting("Acceptable Target Range", "the maximun numbers of tiles the collectors and drills can be far from red line", 6, SettingType.ActiveAndDead)
            {
                MinValue = 2,
                MaxValue = 10
            };
            minDistance.HideInUiWhen.Add(new SettingOption("Set Exposed Collecotors & Mines", 0));
            settings.DefineSetting(minDistance);

            var minimElixir = new AlgorithmSetting("Minimum Exposed Colloctors", "Minimum Elixir Colloctores found outside before attack", 3, SettingType.ActiveAndDead)
            {
                MinValue = 0,
                MaxValue = 7
            };
            minimElixir.HideInUiWhen.Add(new SettingOption("Set Exposed Collecotors & Mines", 0));
            settings.DefineSetting(minimElixir);

            var minimGold = new AlgorithmSetting("Minimum Exposed Mines", "Minimum Gold Mines found outside before attack", 3, SettingType.ActiveAndDead)
            {
                MinValue = 0,
                MaxValue = 7
            };
            minimGold.HideInUiWhen.Add(new SettingOption("Set Exposed Collecotors & Mines", 0));
            settings.DefineSetting(minimGold);


            var useSmartZapDrills = new AlgorithmSetting("Smart Zap Drills", "replace default zap drills module with smart zap module.", 0, SettingType.ActiveAndDead);
            useSmartZapDrills.PossibleValues.Add(new SettingOption("Off", 0));
            useSmartZapDrills.PossibleValues.Add(new SettingOption("On", 1));
            settings.DefineSetting(useSmartZapDrills);

            var minDEAmount = new AlgorithmSetting("Min Dark Elixir per Zap", "we will zap only drills that have more than this amount of DE.", 200, SettingType.ActiveAndDead)
            {
                MinValue = 100,
                MaxValue = 600
            };
            minDEAmount.HideInUiWhen.Add(new SettingOption("Smart Zap Drills", 0));
            settings.DefineSetting(minDEAmount);

            var useEQOnDrills = new AlgorithmSetting("Use EarthQuake spell on drills", "use EarthQuake spell to gain DE from drills ", 0, SettingType.ActiveAndDead);
            useEQOnDrills.PossibleValues.Add(new SettingOption("Off", 0));
            useEQOnDrills.PossibleValues.Add(new SettingOption("On", 1));
            useEQOnDrills.HideInUiWhen.Add(new SettingOption("Smart Zap Drills", 0));
            settings.DefineSetting(useEQOnDrills);

            var deployHeroesAt = new AlgorithmSetting("Deploy Heroes At", "choose where to deploy Heroes", 0, SettingType.ActiveAndDead);
            deployHeroesAt.PossibleValues.Add(new SettingOption("Normal (at the end)", 0));
            deployHeroesAt.PossibleValues.Add(new SettingOption("TownHall Side", 1));
            deployHeroesAt.PossibleValues.Add(new SettingOption("DE Storage Side", 2));
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

        public override IEnumerable<int> AttackRoutine()
        {
            // Set start battle time.
            startTime = DateTime.Now;
            
            int waveLimit = UserSettings.WaveSize;
            int waveDelay = (int)(UserSettings.WaveDelay * 1000);
            int heroesIndex = -1;

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

            
            var target = SmartFourFingersHelper.GetHeroesTarget(deployHeroesAt);

            // Search for target if not found for 3 more times
            if(target.X == 0f && target.Y == 0f)
            {
                for (var i = 1; i <= 3; i++)
                {
                    yield return 1000;
                    target = SmartFourFingersHelper.GetHeroesTarget(deployHeroesAt);
                    if (target.X != 0f || target.Y != 0f)
                        break;
                }
            }

            var nearestRedPointToTarget = GameGrid.RedPoints.OrderBy(p => p.DistanceSq(target)).FirstOrDefault();
            var nearestLinePoint = linesPointsList.OrderBy(p => p.DistanceSq(nearestRedPointToTarget)).FirstOrDefault();

            heroesIndex = attackLines.FindIndex(u => (u.Item1.X == nearestLinePoint.X && u.Item1.Y == nearestLinePoint.Y) || (u.Item2.X == nearestLinePoint.X && u.Item2.Y == nearestLinePoint.Y));

            var units = Deploy.GetTroops();
            var heroes = units.Extract(x => x.IsHero);
            var cc = units.ExtractOne(u => u.ElementType == DeployElementType.ClanTroops);
            var spells = units.Extract(u => u.ElementType == DeployElementType.Spell);

            units.OrderForDeploy();

            // Set first attack line 
            // Start from the next line to user defined to end with user defined line
            var line = attackLines.NextOf(attackLines[heroesIndex]);
            var index = attackLines.FindIndex(u => u.Item1.X == line.Item1.X && u.Item1.Y == line.Item1.Y);

            Log.Info($"{AttackName} {Version} starts");
            // Start troops deployment on four sides.
            for (var i = 4; i >= 1; i--)
            {
                foreach (var unit in units)
                {
                    if (unit?.Count > 0)
                    {
                        var count = unit.Count / i;
                        var fingers = count < 8 ? count : 4;
                        foreach (var t in Deploy.AlongLine(unit, line.Item1, line.Item2, count, fingers, 0, waveDelay))
                            yield return t;
                    }
                }
                if(i != 1)
                {
                    line = attackLines.NextOf(attackLines[index]);
                    index = attackLines.FindIndex(u => u.Item1.X == line.Item1.X && u.Item1.Y == line.Item1.Y);
                }
            }

            if (cc?.Count > 0)
            {
                Log.Info($"{AttackName} Deploy Clan Castle troops");
                foreach (var t in Deploy.AlongLine(cc, line.Item1, line.Item2, 1, 1, 0, waveDelay))
                    yield return t;
            }

            if (heroes.Any())
            {
                Log.Info($"{AttackName} Deploy Heroes");
                foreach (var hero in heroes.Where(u => u.Count > 0))
                {
                    foreach (var t in Deploy.AlongLine(hero, line.Item1, line.Item2, 1, 1, 0, waveDelay))
                        yield return t;
                }
                Deploy.WatchHeroes(heroes, 5000);
            }

            // Call FinalizeAttack and ForceZap at the same time
            var finalize = this.FinalizeAttack(units).GetEnumerator();
            var force = ForceZap().GetEnumerator();

            var firstEnumMoreItems = finalize.MoveNext();
            var secondEnumMoreItems = force.MoveNext();

            // Start both FinalizeAttack and ForceZap
            while (firstEnumMoreItems && secondEnumMoreItems)
            {
                firstEnumMoreItems = finalize.MoveNext();
                secondEnumMoreItems = force.MoveNext();
                yield return 200;
            }
            // Complete ForceZap if FinalizeAttack finished
            while (!firstEnumMoreItems && secondEnumMoreItems)
            {
                secondEnumMoreItems = force.MoveNext();
                yield return 200;
            }
            // Complete FinalizeAttack if ForceZap finished
            while (!secondEnumMoreItems && firstEnumMoreItems)
            {
                firstEnumMoreItems = finalize.MoveNext();
                yield return 200;
            }
        }

        /// <summary>
        /// Force zap drills before battle time ends if loot keep changing until end of the time.
        /// </summary>
        /// <returns>trigger zap drills before battle ended</returns>
        IEnumerable<int> ForceZap()
        {
            if (UserSettings.ZapDarkElixirDrills)
            {
                Log.Info("[Force Zap] Waiting for Lightning drills to be finished");
                Log.Debug($"[Force Zap] Start time is {startTime.Hour}:{startTime.Minute}:{startTime.Second}");
                
                while (isZapped == false)
                {
                    var timeDiff = DateTime.Now.Subtract(startTime);

                    // Call ZapDarkElixirDrills if timeDiff > 2 mins and half (150 secs).
                    if (timeDiff.TotalSeconds > 140)
                    {
                        Log.Warning("[Force Zap] Force zap dark drills befor battle ended !!");
                        foreach (var t in ZapDarkElixirDrills())
                            yield return (t);
                    }
                    else
                        yield return 800;
                }
            }
        }

        /// <summary>
        /// Override default ZapDarlElixirDrills with SmartZap if user select that.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<int> ZapDarkElixirDrills()
        {
            if(!isZapped)
            {
                if (GetCurrentSetting("Smart Zap Drills") == 1)
                {
                    // Call Smart zap method
                    var minDEAmount = GetCurrentSetting("Min Dark Elixir per Zap");
                    var minDEDrillLevel = UserSettings.MinDarkElixirDrillLevel;
                    var spells = Deploy.GetTroops().Extract(u => u.ElementType == DeployElementType.Spell);

                    foreach (var t in SmartZapping.SmartZap(minDEAmount, minDEDrillLevel, spells, GetCurrentSetting("Use EarthQuake spell on drills")))
                        yield return t;
                }
                else
                {
                    // Call the default Zap drills method.
                    foreach (var t in base.ZapDarkElixirDrills())
                        yield return (t);
                }
            }
            
            isZapped = true;
        }

        public override double ShouldAccept()
        {
            if (!Opponent.MeetsRequirements(BaseRequirements.All))
                return 0;
            if (!Opponent.IsForcedAttack && GetCurrentSetting("Set Exposed Collecotors & Mines") == 1)
            {
                if (!SmartFourFingersHelper.IsBaseMinCollectorsAndMinesOutside(GetCurrentSetting("Acceptable Target Range"), GetCurrentSetting("Minimum Exposed Colloctors"), GetCurrentSetting("Minimum Exposed Mines"), AttackName, GetCurrentSetting("Debug Mode"))) 
                    return 0;
            }
            return 1;
        }
    }
}