using System;
using System.Collections.Generic;
using System.Linq;
using CoC_Bot;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using SharedCode;
using System.Reflection;
using CustomAlgorithmSettings;

[assembly: Addon("HumanBarchDeploy Addon", "Contains the Human Barch deploy algorithm", "Bert")]

namespace HumanBarchDeploy
{
    [AttackAlgorithm("HumanBarchDeploy", "Deploys Barch units close to collectors in a believeable Human pattern.  (So that a review of the attack does not look like a BOT)")]
    internal class HumanBarchDeploy : BaseAttack
    {
        #region Constructor
        public HumanBarchDeploy(Opponent opponent) : base(opponent)
        {
        }
        #endregion

        #region Algorithm Name Override
        /// <summary>
        /// Returns the Name of this algorithm.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return algorithmName;
        }
        #endregion

        #region Private Member varaibles

        const string Tag = "[Human Barch]";
        const string algorithmName = "Human Barch Deploy";
        private float _thDeployRadius = 1.2f;
        private float _collectorDeployRadius = 1.4f;

        private bool IgnoreGold { get; set; }

        private bool IgnoreElixir { get; set; }

        #endregion

        #region Custom Algorithm Settings

        #region CurrentSetting
        /// <summary>
        /// Returns a Custom Setting's Current Value.  The setting Name must be defined in the DefineSettings Function for this algorithm.
        /// </summary>
        /// <param name="settingName">Name of the setting to Get</param>
        /// <returns>Current Value of the setting.</returns>
        internal int CurrentSetting(string settingName)
        {
            return SettingsController.Instance.GetSetting(algorithmName, settingName, Opponent.IsDead());
        }
        #endregion

        #region AllCurrentSettings
        /// <summary>
        /// Returns a list of all current Algorithm Setting Values.
        /// </summary>
        /// <returns>Current Value of the all settings for this algorithm.</returns>
        internal List<AlgorithmSetting> AllCurrentSettings
        {
            get
            {
                return SettingsController.Instance.AllAlgorithmSettings[algorithmName].AllSettings;
            }
        }
        #endregion

        #region DefineSettings
        /// <summary>
        /// Allows any Algorithm Dev to specify their own Settings.
        /// </summary>
        /// <returns>A Template of Settings to Dynamically build the UI for - so the Bot User can customize the settings.</returns>
        internal static AlgorithmSettings DefineSettings()
        {
            var settings = new AlgorithmSettings();

            settings.AlgorithmName = algorithmName;
            settings.AlgorithmDescriptionURL = "http://www.raccoonbot.com/forum/topic/22848-human-barch-deploy/";

            var debugMode = new AlgorithmSetting("Debug Mode", "When on, Debug Images will be written out for each attack showing what the algorithm is seeing.", 1, SettingType.Global);
            debugMode.PossibleValues.Add(new SettingOption("Off", 0));
            debugMode.PossibleValues.Add(new SettingOption("On", 1));
            settings.DefineSetting(debugMode);

            var ignoreGold = new AlgorithmSetting("Ignore Gold", "When on, The algorithm will not target Gold Collectors/Storages.", 0, SettingType.Global);
            ignoreGold.PossibleValues.Add(new SettingOption("Off", 0));
            ignoreGold.PossibleValues.Add(new SettingOption("On", 1));
            settings.DefineSetting(ignoreGold);

            var ignoreElixir = new AlgorithmSetting("Ignore Elixir", "When on, The algorithm will not target Elixir Collectors/Storages.", 0, SettingType.Global);
            ignoreElixir.PossibleValues.Add(new SettingOption("Off", 0));
            ignoreElixir.PossibleValues.Add(new SettingOption("On", 1));
            settings.DefineSetting(ignoreElixir);

            var minExposedTargets = new AlgorithmSetting("Minimum Exposed Targets", "Minimum number of Collectors/Drills/Storages that are on the outside of the base - before the attack phase will start.", 3, SettingType.ActiveAndDead);
            minExposedTargets.PossibleValues.Add(new SettingOption("1", 1));
            minExposedTargets.PossibleValues.Add(new SettingOption("2", 2));
            minExposedTargets.PossibleValues.Add(new SettingOption("3", 3));
            minExposedTargets.PossibleValues.Add(new SettingOption("4", 4));
            minExposedTargets.PossibleValues.Add(new SettingOption("5", 5));
            minExposedTargets.PossibleValues.Add(new SettingOption("6", 6));
            minExposedTargets.PossibleValues.Add(new SettingOption("7", 7));
            minExposedTargets.PossibleValues.Add(new SettingOption("8", 8));
            settings.DefineSetting(minExposedTargets);

            var maximumDistanceToTarget = new AlgorithmSetting("Acceptable Target Range", "Specify the maximum number of tiles a target can be from the ouside of the base. (Collectors/Drills/Storages furthor inside than this will not be targeted.)", 9, SettingType.ActiveAndDead);
            maximumDistanceToTarget.PossibleValues.Add(new SettingOption("2", 2));
            maximumDistanceToTarget.PossibleValues.Add(new SettingOption("3", 3));
            maximumDistanceToTarget.PossibleValues.Add(new SettingOption("4", 4));
            maximumDistanceToTarget.PossibleValues.Add(new SettingOption("5", 5));
            maximumDistanceToTarget.PossibleValues.Add(new SettingOption("6", 6));
            maximumDistanceToTarget.PossibleValues.Add(new SettingOption("7", 7));
            maximumDistanceToTarget.PossibleValues.Add(new SettingOption("8", 8));
            maximumDistanceToTarget.PossibleValues.Add(new SettingOption("9", 9));
            maximumDistanceToTarget.PossibleValues.Add(new SettingOption("10", 10));
            settings.DefineSetting(maximumDistanceToTarget);

            var minFillLevel = new AlgorithmSetting("Min Collector Fill Level", "The minimum Fullness of Collectors to Attack. (5-50)", 26, SettingType.ActiveOnly);
            minFillLevel.MinValue = 5;
            minFillLevel.MaxValue = 50;
            settings.DefineSetting(minFillLevel);

            var minAvgCollectorLvl = new AlgorithmSetting("Min Average Collector Level", "Specify the Average Collector Level to accept when attacking live bases. (6-12)", 9, SettingType.ActiveOnly);
            minAvgCollectorLvl.MinValue = 6;
            minAvgCollectorLvl.MaxValue = 12;
            settings.DefineSetting(minAvgCollectorLvl);

            var deployAllTroops = new AlgorithmSetting("Deploy All Troops Mode", "When Turned on, The Algorithm Will divide all available troops by number of valid targets, and Deploy all Troops in the First Wave.", 1, SettingType.ActiveAndDead);
            deployAllTroops.PossibleValues.Add(new SettingOption("On", 1));
            deployAllTroops.PossibleValues.Add(new SettingOption("Off", 0));
            settings.DefineSetting(deployAllTroops);

            var groundUnits = new AlgorithmSetting("Ground Units Per Target", "Specify the number of Ground units (Barbs, Goblins etc.) to deploy at each Target.", 10, SettingType.ActiveAndDead);
            groundUnits.MinValue = 1;
            groundUnits.MaxValue = 25;
            groundUnits.HideInUiWhen.Add(new SettingOption("Deploy All Troops Mode", 1));
            settings.DefineSetting(groundUnits);

            var rangedUnits = new AlgorithmSetting("Ranged Units Per Target", "Specify the number of Ranged units (Archers, Minions etc.) to deploy at each Target.", 8, SettingType.ActiveAndDead);
            rangedUnits.MinValue = 1;
            rangedUnits.MaxValue = 25;
            rangedUnits.HideInUiWhen.Add(new SettingOption("Deploy All Troops Mode", 1));
            settings.DefineSetting(rangedUnits);

            var tankUnits = new AlgorithmSetting("Tank Units Per Target", "Specify the number of Tanks (Giants) to deploy at each target.", 1, SettingType.ActiveAndDead);
            tankUnits.PossibleValues.Add(new SettingOption("1", 1));
            tankUnits.PossibleValues.Add(new SettingOption("2", 2));
            tankUnits.HideInUiWhen.Add(new SettingOption("Deploy All Troops Mode", 1));
            settings.DefineSetting(tankUnits);

            return settings;
        }
        #endregion

        #region Algorithm Bot Framework Hooks
        /// <summary>
        /// Called from the Bot Framework when the Algorithm is first loaded into memory.
        /// </summary>
        public static void OnInit()
        {
            //On load of the Plug-In DLL, Define the Default Settings for the Algorithm.
            SettingsController.Instance.DefineCustomAlgorithmSettings(DefineSettings());
        }

        /// <summary>
        /// Called by the Bot Framework when This algorithm Row is selected in Attack Options tab
        /// to check to see whether or not this algorithm has Advanced Settings/Options
        /// </summary>
        /// <returns>returns true if there are advanced settings.</returns>
        public static bool ShowAdvancedSettingsButton()
        {
            return true;
        }

        /// <summary>
        /// Called when the Advanced button is clicked in the Bot UI with this algorithm Selected.
        /// </summary>
        public static void OnAdvancedSettingsButtonClicked()
        {
            //Show the Settings Dialog for this Algorithm.
            SettingsController.Instance.ShowSettingsWindow(algorithmName);
        }

        /// <summary>
        /// Called from the Bot Framework when the bot is closing.
        /// </summary>
        public static void OnShutdown()
        {
            //Save settings for this algorithm.
            SettingsController.Instance.SaveAlgorithmSettings(algorithmName);
        }

        #endregion

        #endregion

        #region Should Accept Function

        public override double ShouldAccept()
        {
            double returnVal = 0;

            // check if the base meets ALL the user's requirements
            if (!PassesBasicAcceptRequirements())
            {
                return 0;
            }

            //Check to see if the settings are favoring Gold or Elixir...
            IgnoreGold = (CurrentSetting("Ignore Gold") == 1);
            IgnoreElixir = (CurrentSetting("Ignore Elixir") == 1);

            var minTargets = CurrentSetting("Minimum Exposed Targets");

            //If ignoring Gold, Reduce the min required targets by half.
            if (IgnoreGold)
            {
                Log.Info($"{Tag}Ignoring Gold Storages/Collectors");
            }

            //If ignoring Gold, Reduce the min required targets by half.
            if (IgnoreElixir)
            {
                Log.Info($"{Tag}Ignoring Elixir Storages/Collectors");
            }

            var acceptableTargetRange = CurrentSetting("Acceptable Target Range");
            acceptableTargetRange = acceptableTargetRange * acceptableTargetRange;

            int ripeCollectors = 0;
            double avgfillState = 0;
            double avgCollectorLvel = 0;
            var activeBase = !Opponent.IsDead();

            //Check how many Collectors are Ripe for the taking (outside walls)
            ripeCollectors = HumanLikeAlgorithms.CountRipeCollectors(algorithmName, acceptableTargetRange, IgnoreGold, IgnoreElixir, out avgfillState, out avgCollectorLvel, CacheBehavior.Default, activeBase);

            if (activeBase)
            {
                var minFillLevel = (double)(CurrentSetting("Min Collector Fill Level"));
                if ((avgfillState * 10) < minFillLevel)
                {
                    //FillState is too Low. Skip
                    Log.Warning($"{Tag}Skipping - Avg fillstate is too low: {(avgfillState * 10).ToString("F1")}. Must be > {minFillLevel}.");
                    return 0;
                }
                else
                {
                    Log.Info($"{Tag}Avg Collector Fillstate Accepted: {(avgfillState * 10).ToString("F1")} > {minFillLevel}.");
                }

                var minAvgCollectorLevel = CurrentSetting("Min Average Collector Level");
                if (avgCollectorLvel < minAvgCollectorLevel)
                {
                    //Level of Collectors is too low.
                    Log.Warning($"{Tag}Skipping - Avg Collector Level is too low: {avgCollectorLvel.ToString("F1")}. Must be > {minAvgCollectorLevel}.");
                    return 0;
                }
                else
                {
                    Log.Info($"{Tag}Avg Collector Level Accepted: {avgCollectorLvel.ToString("F1")} > {minAvgCollectorLevel}.");
                }
            }
            else
            {
                //Log some info about the AvgCollectorLevel and Fill State
                Log.Info($"{Tag}Avg Collector Fillstate: {(avgfillState * 10).ToString("F1")}");
                Log.Info($"{Tag}Avg Collector Level: {avgCollectorLvel.ToString("F1")}");
            }

            Log.Debug($"{Tag}{ripeCollectors} targets found outside walls. Min={minTargets}");

            if (ripeCollectors < minTargets)
            {
                Log.Warning($"{Tag}Skipping - {ripeCollectors} targets were found outside the wall. Min={minTargets}");
                returnVal = 0;
            }
            else
            {
                returnVal = .99;
            }

            return returnVal;
        }

        #region PassesBasicAcceptRequirements

        bool PassesBasicAcceptRequirements()
        {
            // check if the base meets ALL the user's requirements (One at a time, and log a warning for WHY its skipping)
            if (!Opponent.MeetsRequirements(BaseRequirements.Elixir))
            {
                Log.Warning($"{Tag} Elixir Requirements not Met - Skipping");
                return false;
            }

            if (!Opponent.MeetsRequirements(BaseRequirements.Gold))
            {
                Log.Warning($"{Tag} Gold Requirements not Met - Skipping");
                return false;
            }

            if (!Opponent.MeetsRequirements(BaseRequirements.DarkElixir))
            {
                Log.Warning($"{Tag} Dark Elixir Requirements not Met - Skipping");
                return false;
            }

            if (!Opponent.MeetsRequirements(BaseRequirements.MaxThLevel))
            {
                Log.Warning($"{Tag} Base Over Town Hall Max - Skipping");
                return false;
            }

            if (!Opponent.MeetsRequirements(BaseRequirements.AvoidStrongBases))
            {
                Log.Warning($"{Tag} Strong Base Detected - Skipping");
                return false;
            }

            //Everything meets requirements...
            return true;
        }
        #endregion

        #endregion

        #region Attack Routine
        public override IEnumerable<int> AttackRoutine()
        {
            Log.Info($"{Tag}Deploy start - V.{Assembly.GetExecutingAssembly().GetName().Version.ToString()}");

            //Write out all the Current Algorithm Settings...
            foreach (var setting in AllCurrentSettings)
            {
                Log.Debug($"{Tag}'{setting.Name}' Value: {setting.Value}");
            }

            var waveCounter = 1;

            //Check if we can snipe the town hall, and if so, what are the Deployment points for Gruns/Ranged.
            TownHall townHall = TownHall.Find(CacheBehavior.Default);

            Target townHallTarget = townHall.GetSnipeDeployPoints();

            // Get starting resources
            LootResources preLoot = Opponent.GetAvailableLoot();

            if (preLoot == null)
            {
                Log.Error($"{Tag}Could not read available starting loot");
                Attack.Surrender();
                yield break;
            }
            Log.Info($"{Tag}Pre-attack resources - G: {preLoot.Gold}, E: {preLoot.Elixir}, DE: {preLoot.DarkElixir}");

            var collectorCacheBehavior = CacheBehavior.ForceScan;
            var collectorCount = 0;
            var acceptableTargetRange = CurrentSetting("Acceptable Target Range");
            acceptableTargetRange = acceptableTargetRange * acceptableTargetRange;
            var activeBase = !Opponent.IsDead();
            var clanCastleDeployed = false;

            // Loop until surrender conditions are met
            while (true)
            {
                // Get all the units available
                Log.Info($"{Tag}Scanning troops for wave {waveCounter}");

                var allElements = Attack.GetAvailableDeployElements();
                var deployElements = allElements.Where(x => x.UnitData != null).ToArray();
                var rangedUnits = deployElements.Where(x => x.IsRanged == true && x.ElementType == DeployElementType.NormalUnit && x.UnitData.AttackType == AttackType.Damage);
                var gruntUnits = deployElements.Where(x => x.IsRanged == false && x.ElementType == DeployElementType.NormalUnit && x.UnitData.AttackType == AttackType.Damage);
                var tankUnits = deployElements.Where(x => x.IsRanged == false && x.ElementType == DeployElementType.NormalUnit && x.UnitData.AttackType == AttackType.Tank);
                List<DeployElement> king = allElements.Where(x => x.IsHero && x.Name.ToLower().Contains("king")).ToList();
                List<DeployElement> queen = allElements.Where(x => x.IsHero && x.Name.ToLower().Contains("queen")).ToList();
                List<DeployElement> warden = allElements.Where(x => x.IsHero && x.Name.ToLower().Contains("warden")).ToList();
                List<DeployElement> allHeroes = new List<DeployElement>();
                allHeroes.AddRange(king);
                allHeroes.AddRange(queen);
                allHeroes.AddRange(warden);

                bool watchHeroes = false;
                bool kingDeployed = false;

                //Write out all the unit pretty names we found...
                Log.Debug($"{Tag}Deployable Troops (wave {waveCounter}): {ToUnitString(allElements)}");

                var outputDebugImage = (CurrentSetting("Debug Mode") == 1);
                double avgFillState = 0;
                double avgCollectorLvl = 0;

                //First time through force a Scan... after the first wave always recheck for Destroyed ones...
                Target[] targets = HumanLikeAlgorithms.GenerateTargets(algorithmName, acceptableTargetRange, IgnoreGold, IgnoreElixir, out avgFillState, out avgCollectorLvl, collectorCacheBehavior, outputDebugImage, activeBase);

                collectorCount = targets.Length;

                Target reminderTarget = null;
                if (collectorCount > 0) {
                    reminderTarget = targets[0];
                }

                //Reorder the Deploy points so they look more human like when attacking.
                var groupedTargets = targets.ReorderToClosestNeighbor().GroupCloseTargets();

                collectorCacheBehavior = CacheBehavior.CheckForDestroyed;

                if (collectorCount < 1)
                {
                    Log.Info($"{Tag}Collectors Remaining = {collectorCount}");

                    // Wait for the wave to finish
                    Log.Info($"{Tag}Deploy done. Waiting to finish...");
                    var x = Attack.WatchResources(10d).Result;

                    break;
                }

                int meleCount = 0;
                int rangedCount = 0;
                int tankCount = 0;

                if (CurrentSetting("Deploy All Troops Mode") == 0)
                {
                    //Determine Counts of each type of unit to use...
                    meleCount = CurrentSetting("Ground Units Per Target");
                    rangedCount = CurrentSetting("Ranged Units Per Target");
                    tankCount = CurrentSetting("Tank Units Per Target");
                }
                else {
                    //Get the total count of Valid Targets. (Including Town Hall if there is one.)
                    int totalTargetCount = targets.Length;

                    if (townHallTarget.ValidTarget) {
                        totalTargetCount++;
                    }

                    //Will be the largest int without remainder.
                    meleCount = gruntUnits.TotalUnitCount() / totalTargetCount;
                    rangedCount = rangedUnits.TotalUnitCount() / totalTargetCount;
                    tankCount = tankUnits.TotalUnitCount() / totalTargetCount;

                    //Make sure if there are less than 1 per target, but still more than zero. set to 1.
                    if (tankCount == 0 && tankUnits.TotalUnitCount() > 0)
                        tankCount = 1;
                    if (rangedCount == 0 && rangedUnits.TotalUnitCount() > 0)
                        rangedCount = 1;
                    if (meleCount == 0 && gruntUnits.TotalUnitCount() > 0)
                        meleCount = 1;
                }

                if (townHallTarget.ValidTarget)
                {
                    //Drop some Grunt and Ranged troups on the TH as well as collectors.
                    //If there are Teslas around it, oh well. we only spent 9-12 units  of each type trying.
                    if (gruntUnits.Any())
                    {
                        var gruntsToDeploy = Rand.Int(meleCount - 1, meleCount + 1);
                        Log.Info($"{Tag}TH Snipe Dead {gruntsToDeploy} Grunts Near: X:{townHallTarget.DeployGrunts.X} Y:{townHallTarget.DeployGrunts.Y}");
                        foreach (var t in Deploy.AtPoints(gruntUnits.FilterTypesByCount(), townHallTarget.DeployGrunts.RandomPointsInArea(_thDeployRadius, gruntsToDeploy), 1))
                            yield return t;
                        yield return Rand.Int(300, 500); //Wait 
                    }

                    if (rangedUnits.Any())
                    {
                        var rangedToDeploy = Rand.Int(rangedCount - 1, rangedCount + 1);
                        Log.Info($"{Tag}TH Snipe Dead {rangedToDeploy} Ranged Near: X:{townHallTarget.DeployRanged.X} Y:{townHallTarget.DeployRanged.Y}");
                        foreach (var t in Deploy.AtPoints(rangedUnits.FilterTypesByCount(), townHallTarget.DeployRanged.RandomPointsInArea(_thDeployRadius, rangedToDeploy), 1))
                            yield return t;
                        yield return Rand.Int(300, 500); //Wait 
                    }

                    if (UserSettings.UseClanTroops)
                    {
                        var clanCastle = allElements.FirstOrDefault(u => u.ElementType == DeployElementType.ClanTroops);

                        clanCastleDeployed = true;

                        if (clanCastle?.Count > 0)
                        {
                            Log.Info($"{Tag}Deploying Clan Castle Near Town Hall");
                            foreach (var t in Deploy.AtPoint(clanCastle, townHallTarget.DeployRanged, clanCastle.Count))
                                yield return t;

                        }
                        else
                        {
                            Log.Info($"{Tag}No Clan Castle Troops found to Deploy on Town Hall...");
                        }
                    }

                    //Only do this once.
                    townHallTarget.ValidTarget = false;
                }

                //Determine the index of the 1st and 2nd largest set of targets all in a row.
                var largestSetIndex = -1;
                int largestSetCount = 0;
                var secondLargestSetIndex = -1;
                int secondLargestSetCount = 0;

                for (int i = 0; i < groupedTargets.Count; i++)
                {
                    if (groupedTargets[i].Length > largestSetCount)
                    {
                        secondLargestSetCount = largestSetCount;
                        secondLargestSetIndex = largestSetIndex;
                        largestSetCount = groupedTargets[i].Length;
                        largestSetIndex = i;
                    }
                    else if (groupedTargets[i].Length > secondLargestSetCount)
                    {
                        secondLargestSetCount = groupedTargets[i].Length;
                        secondLargestSetIndex = i;
                    }
                }

                Log.Info($"{Tag}{groupedTargets.Count} Target Groups, Largest has {largestSetCount} targets, Second Largest {secondLargestSetCount} targets.");

                //Deploy Barch Units - In Groups on Sets of collectors that are close together.
                for (int p = 0; p < groupedTargets.Count; p++)
                {
                    //Deploy Tanks on the Set of Targets. (If any exist)
                    for (int i = 0; i < groupedTargets[p].Length; i++)
                    {
                        var gruntDeployPoint = groupedTargets[p][i].DeployGrunts;

                        //First Deploy tanks
                        if (tankUnits.Any())
                        {
                            Log.Debug($"{Tag}Deploying {tankCount} Tank Units on {groupedTargets[p][i].Name} {p + 1}-{i}");
                            foreach (var t in Deploy.AtPoints(tankUnits.FilterTypesByCount(), gruntDeployPoint.RandomPointsInArea(_collectorDeployRadius, tankCount), 1))
                                yield return t;
                            yield return Rand.Int(10, 40); //Wait
                        }
                    }

                    if (gruntUnits.Any())
                    {
                        //Pause inbetween switching units.
                        yield return Rand.Int(90, 100); //Wait
                    }

                    //Deploy Grunts on the Set of Targets.
                    for (int i = 0; i < groupedTargets[p].Length; i++)
                    {
                        var gruntDeployPoint = groupedTargets[p][i].DeployGrunts;

                        //Next Deploy Ground troops
                        if (gruntUnits.Any())
                        {
                            int decreaseFactor = 0;
                            if (i > 0)
                                decreaseFactor = (int)Math.Ceiling(i / 2d);

                            var gruntsAtCollector = (Rand.Int(meleCount - 1, meleCount + 1) - decreaseFactor);
                            Log.Debug($"{Tag}Deploying {gruntsAtCollector} Ground Units on {groupedTargets[p][i].Name} {p + 1}-{i}");
                            foreach (var t in Deploy.AtPoints(gruntUnits.FilterTypesByCount(), gruntDeployPoint.RandomPointsInArea(_collectorDeployRadius, gruntsAtCollector), 1))
                                yield return t;
                            yield return Rand.Int(10, 40); //Wait
                        }
                    }

                    if (largestSetIndex == p && largestSetCount >= 2)
                    {
                        //We are currently deploying to the largest set of Targets - AND its a set of 2 or more.
                        //Preferrably Drop the Queen on this set (2nd Target in the set.) - if she is not available drop the king here.
                        reminderTarget = groupedTargets[p][1];

                        if (!clanCastleDeployed && UserSettings.UseClanTroops)
                        {
                            var clanCastle = allElements.FirstOrDefault(u => u.ElementType == DeployElementType.ClanTroops);

                            if (clanCastle?.Count > 0)
                            {
                                Log.Info($"{Tag}Deploying Clan Castle on Largest set of Targets: {largestSetCount} targets.");
                                foreach (var t in Deploy.AtPoint(clanCastle, groupedTargets[p][1].DeployRanged, clanCastle.Count))
                                    yield return t;
                            }
                            else
                            {
                                Log.Info($"{Tag}No Clan Castle Troops found to Deploy...");
                            }
                            clanCastleDeployed = true;
                        }

                        if (UserSettings.UseQueen && queen.Any())
                        {
                            yield return Rand.Int(90, 100); //Wait before dropping Queen

                            Log.Info($"{Tag}Deploying Queen on largest set of targets: {largestSetCount} targets.");
                            foreach (var t in Deploy.AtPoint(queen[0], groupedTargets[p][1].DeployRanged))
                                yield return t;
                            yield return Rand.Int(200, 500); //Wait
                            watchHeroes = true;
                        }
                        else if (UserSettings.UseKing && king.Any())
                        {
                            yield return Rand.Int(90, 100); //Wait before dropping King

                            Log.Info($"{Tag}Deploying King on largest set of targets: {largestSetCount} targets.");
                            foreach (var t in Deploy.AtPoint(king[0], groupedTargets[p][1].DeployGrunts))
                                yield return t;
                            yield return Rand.Int(200, 500); //Wait
                            kingDeployed = true;
                            watchHeroes = true;
                        }

                        if (UserSettings.UseWarden && warden.Any())
                        {
                            Log.Info($"{Tag}Deploying Warden on largest set of targets: {largestSetCount} targets.");
                            foreach (var t in Deploy.AtPoint(warden[0], groupedTargets[p][1].DeployRanged))
                                yield return t;
                            yield return Rand.Int(200, 500); //Wait
                            watchHeroes = true;
                        }
                    }

                    if (secondLargestSetIndex == p && secondLargestSetCount >= 2)
                    {
                        //We are currently deploying to the 2nd largest set of Targets - AND its a set of 2 or more.
                        //Drop the King on the 2nd Target in the set.

                        if (UserSettings.UseKing && king.Any() && !kingDeployed)
                        {
                            Log.Info($"{Tag}Deploying King on 2nd largest set of targets: {secondLargestSetCount} targets.");
                            foreach (var t in Deploy.AtPoint(king[0], groupedTargets[p][1].DeployGrunts))
                                yield return t;
                            yield return Rand.Int(900, 1000); //Wait

                            watchHeroes = true;
                        }
                    }

                    if (watchHeroes)
                    {
                        //Watch Heros and Hit ability when they get low.
                        Log.Info($"{Tag}Watching heros to activate abilities when health gets Low.");
                        Deploy.WatchHeroes(allHeroes);
                        watchHeroes = false; //Only do this once through the loop.
                    }

                    if (rangedUnits.Any())
                    {
                        //Pause inbetween switching units.
                        yield return Rand.Int(90, 100); //Wait
                    }

                    //Deploy Ranged units on same set of Targets.
                    for (int i = 0; i < groupedTargets[p].Length; i++)
                    {
                        var rangedDeployPoint = groupedTargets[p][i].DeployRanged;

                        if (rangedUnits.Any())
                        {
                            int decreaseFactor = 0;
                            if (i > 0)
                                decreaseFactor = (int)Math.Ceiling(i / 2d);

                            var rangedAtCollector = (Rand.Int(rangedCount - 1, rangedCount + 1) - decreaseFactor);
                            Log.Debug($"{Tag}Deploying {rangedAtCollector} Ranged Units on {groupedTargets[p][i].Name} {p + 1}-{i}");
                            foreach (var t in Deploy.AtPoints(rangedUnits.FilterTypesByCount(), rangedDeployPoint.RandomPointsInArea(_collectorDeployRadius, rangedAtCollector), 1))
                                yield return t;
                            yield return Rand.Int(40, 50); //Wait
                        }
                    }

                    yield return Rand.Int(90, 100); //Wait before switching units back to Grutns and deploying on next set of targets.
                }

                //If Deploy ALL Troops is turned on, 
                if (CurrentSetting("Deploy All Troops Mode") == 1 && reminderTarget != null)
                {
                    //Deploy the Reminder of troops on the LARGEST Group of targets.

                    //First Deploy tanks
                    foreach (var units in tankUnits)
                    {
                        if (units?.Count > 0)
                        {
                            Log.Debug($"{Tag}Deploying Reminder of {units.PrettyName} Tank Units ({units.Count}) on {reminderTarget.Name}");
                            foreach (var t in Deploy.AtPoint(units, reminderTarget.DeployGrunts, units.Count))
                                yield return t;
                            yield return Rand.Int(2000, 3000); //Wait
                        }
                    }

                    //Next Deploy Grunts
                    foreach (var units in gruntUnits)
                    {
                        if (units?.Count > 0)
                        {
                            Log.Debug($"{Tag}Deploying Reminder of {units.PrettyName} Mele Units ({units.Count}) on {reminderTarget.Name}");
                            foreach (var t in Deploy.AtPoint(units, reminderTarget.DeployGrunts, units.Count))
                                yield return t;
                            yield return Rand.Int(100, 200); //Wait
                        }
                    }

                    //Next Deploy Ranged
                    foreach (var units in rangedUnits)
                    {
                        if (units?.Count > 0)
                        {
                            Log.Debug($"{Tag}Deploying Reminder of {units.PrettyName} Ranged Units ({units.Count}) on {reminderTarget.Name}");
                            foreach (var t in Deploy.AtPoint(units, reminderTarget.DeployRanged, units.Count))
                                yield return t;
                            yield return Rand.Int(100, 200); //Wait
                        }
                    }

                    if (CurrentSetting("Debug Mode") == 1)
                        HumanLikeAlgorithms.SaveBasicDebugScreenShot(algorithmName, "All Deployed");

                    //There is only ONE wave in Deploy all troops mode... Watch for No Change in Resources, then Break out.
                    var x = Attack.WatchResources(10d).Result;
                    break;
                }

                //wait a random number of seconds before the next round on all Targets...
                yield return Rand.Int(2000, 5000);

                // Get starting resources, cache needs to be false to force a new check
                LootResources postLoot = Opponent.GetAvailableLoot(false);
                if (postLoot == null)
                {
                    Log.Warning($"{Tag}could not read available loot this wave");
                    postLoot = new LootResources() { Gold = -1, Elixir = -1, DarkElixir = -1 };
                }

                Log.Info($"{Tag}Wave {waveCounter} resources - G: {postLoot.Gold}, E: {postLoot.Elixir}, DE: {postLoot.DarkElixir}");
                int newGold = preLoot.Gold - postLoot.Gold;
                int newElixir = preLoot.Elixir - postLoot.Elixir;
                int newDark = preLoot.DarkElixir - postLoot.DarkElixir;
                Log.Info($"{Tag}Wave {waveCounter} resource diff - G: {newGold}, E: {newElixir}, DE: {newDark}, Collectors: {collectorCount}");

                //Check to see if we are getting enough Resources... 
                if (postLoot.Gold + postLoot.Elixir + postLoot.DarkElixir >= 0)
                {
                    if (newGold + newElixir < 3000 * collectorCount)
                    {
                        Log.Info($"{Tag}Stopping Troop Deployment because gained resources isn't enough");
                        var x = Attack.WatchResources(10d).Result;
                        break;
                    }
                    preLoot = postLoot;
                }

                waveCounter++;
            }


            //Last thing Call ZapDarkElixterDrills... This uses the Bot settings for when to zap, and what level drills to zap.
            Log.Info($"{Tag}Checking to see if we can Zap DE Drills...");
            foreach (var t in ZapDarkElixirDrills())
                yield return t;

            if (CurrentSetting("Debug Mode") == 1)
                HumanLikeAlgorithms.SaveBasicDebugScreenShot(algorithmName, "Battle End");

            //We broke out of the attack loop - allow attack to end how specified in the General Bot Settings... 
        }

        #endregion
    }
}
