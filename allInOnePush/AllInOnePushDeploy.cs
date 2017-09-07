using System;
using System.Collections.Generic;
using System.Linq;
using CoC_Bot.API;
using CustomAlgorithmSettings;
using System.Reflection;

[assembly: Addon("AllInOnePushDeploy", "Push tropies using all types of troops", "CobraTST")]
namespace AllInOnePushDeploy
{
    [AttackAlgorithm("AllInOnePushDeploy", "Push tropies using all types of troops")]
    public class AllInOnePushDeploy : BaseAttack
    {
        internal static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        public static string AttackName = "All In One Push";
        public static int MinDistace = 18;
        public static bool Debug = false;

        public static PointFT Core { get; set; }
        public static PointFT Origin { get; set; }
        public static PointFT Target { get; set; }
        public static PointFT FirstFunnellingPoint { get; set; }
        public static PointFT SecondFunnellingPoint { get; set; }
        public static PointFT FirstJumpPoint { get; set; }
        public static PointFT SecondJumpPoint { get; set; }
        public static PointFT FirstRagePoint { get; set; }
        public static PointFT SecondRagePoint { get; set; }
        public static PointFT FirstHastePoint { get; set; }
        public static PointFT FirstHealPoint { get; set; }
        public static PointFT SecondHealPoint { get; set; }
        public static PointFT EqPoint { get; set; }
        public static PointFT QWHealer { get; set; }
        public static PointFT QWRagePoint { get; set; }

        public static Tuple<PointFT, PointFT> AttackLine { get; set; }
        public static Tuple<PointFT, PointFT> FirstHasteLine { get; set; }
        public static Tuple<PointFT, PointFT> FirstRageLine { get; set; }
        public static Tuple<PointFT, PointFT> SecondHasteLine { get; set; }
        public static Tuple<PointFT, PointFT> SecondRageLine { get; set; }

        public static int ClanCastleSettings { get; set; }
        public static int QWSettings { get; set; }
        public static int HealerOnQWSettings { get; set; }
        public static int RageOnQWSettings { get; set; }

        public static bool IsAirAttack { get; set; }
        public static List<int> CustomOrderList { get; set; }


        #region Advanced settings methods

        /// <summary>
        /// Returns a Custom Setting's Current Value.  The setting Name must be defined in the DefineSettings Function for this algorithm.
        /// </summary>
        /// <param name="settingName">Name of the setting to Get</param>
        /// <returns>Current Value of the setting.</returns>
        public int GetCurrentSetting(string settingName)
        {
            return SettingsController.Instance.GetSetting(AttackName, settingName, Opponent.IsDead());
        }

        /// <summary>
        /// Returns a list of all current Algorithm Setting Values.
        /// </summary>
        /// <returns>Current Value of the all settings for this algorithm.</returns>
        internal List<AlgorithmSetting> GetAllCurrentSettings()
        {
            return SettingsController.Instance.AllAlgorithmSettings[AttackName].AllSettings;
        }

        /// <summary>
        /// Called from the Bot Framework when the Algorithm is first loaded into memory.
        /// </summary>
        public static void OnInit()
        {
            // On load of the Plug-In DLL, Define the Default Settings for the Algorithm.
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

        internal static AlgorithmSettings DefineSettings()
        {
            var settings = new AlgorithmSettings();

            settings.AlgorithmName = AttackName;
            settings.AlgorithmDescriptionURL = "https://www.raccoonbot.com/forum/topic/24589-dark-push-deploy/";

            //Global Settings

            var debugMode = new AlgorithmSetting("Debug Mode", "When on, Debug Images will be written out for each attack showing what the algorithm is seeing.", 0, SettingType.Global);
            debugMode.PossibleValues.Add(new SettingOption("Off", 0));
            debugMode.PossibleValues.Add(new SettingOption("On", 1));
            settings.DefineSetting(debugMode);

            var setTargetTo = new AlgorithmSetting("Select Your Target", "", 0, SettingType.ActiveAndDead);
            setTargetTo.PossibleValues.Add(new SettingOption("TownHall", 0));
            setTargetTo.PossibleValues.Add(new SettingOption("Dark Elixir Storage", 1));
            setTargetTo.PossibleValues.Add(new SettingOption("Eagle Artilary", 2));
            settings.DefineSetting(setTargetTo);

            var UseQueenWalk = new AlgorithmSetting("Use Queen Walk", "When on, healer will be used for Queen walk, When off: it will be used for Bowler and Witch walk", 0, SettingType.ActiveAndDead);
            UseQueenWalk.PossibleValues.Add(new SettingOption("Off", 0));
            UseQueenWalk.PossibleValues.Add(new SettingOption("On", 1));
            settings.DefineSetting(UseQueenWalk);

            //Show These ONLY when Use Queen Walk Mode is on
            var HealersForQueenWalk = new AlgorithmSetting("Number of healers to use on Queen", "How meny healers to follow the queen , the rest will be dropped on the main troops", 4, SettingType.ActiveAndDead)
            {
                MinValue = 1,
                MaxValue = 8
            };
            HealersForQueenWalk.HideInUiWhen.Add(new SettingOption("Use Queen Walk", 0));
            settings.DefineSetting(HealersForQueenWalk);

            var UseRageForQW = new AlgorithmSetting("Drop 1 rage in the first of the QW", "use 1 rage on the Queen To help fast funnelling", 0, SettingType.ActiveAndDead);
            UseRageForQW.PossibleValues.Add(new SettingOption("Off", 0));
            UseRageForQW.PossibleValues.Add(new SettingOption("On", 1));
            UseRageForQW.HideInUiWhen.Add(new SettingOption("Use Queen Walk", 0));
            settings.DefineSetting(UseRageForQW);

            var useCCAs = new AlgorithmSetting("use Clan Castle troops as", "", 0, SettingType.ActiveAndDead);
            useCCAs.PossibleValues.Add(new SettingOption("Normal troops (deploy at the end)", 0));
            useCCAs.PossibleValues.Add(new SettingOption("Golem (deploy at the first)", 1));
            useCCAs.PossibleValues.Add(new SettingOption("Giants (deploy before normal troops)", 2));
            settings.DefineSetting(useCCAs);

            var customDeployOrder = new AlgorithmSetting("use custom deploy order", "Change the deploying troops order, the default order is: 1-Golems if more than 1, 2- funnling, 3-giants or one golem, 4-heroes, 5-wallBreakers, 6-Normal troops", 0, SettingType.Global);
            customDeployOrder.PossibleValues.Add(new SettingOption("Off", 0));
            customDeployOrder.PossibleValues.Add(new SettingOption("On", 1));
            settings.DefineSetting(customDeployOrder);

            // deploy order if custom deploy is on
            var deploy1 = new AlgorithmSetting("#1", "", 1, SettingType.Global);
            deploy1.PossibleValues.Add(new SettingOption("Golems", 1));
            deploy1.PossibleValues.Add(new SettingOption("Ground Funnelling", 2));
            deploy1.PossibleValues.Add(new SettingOption("Giants", 3));
            deploy1.PossibleValues.Add(new SettingOption("Heroes", 4));
            deploy1.PossibleValues.Add(new SettingOption("Wall Breakers", 5));
            deploy1.PossibleValues.Add(new SettingOption("Normal Troops", 6));
            deploy1.PossibleValues.Add(new SettingOption("Balloons", 7));
            deploy1.PossibleValues.Add(new SettingOption("BabyDragons", 8));
            deploy1.PossibleValues.Add(new SettingOption("Lava", 9));
            deploy1.PossibleValues.Add(new SettingOption("Dragons", 10));
            deploy1.PossibleValues.Add(new SettingOption("Air Funnelling", 11));
            deploy1.HideInUiWhen.Add(new SettingOption("use custom deploy order", 0));
            settings.DefineSetting(deploy1);

            var deploy2 = new AlgorithmSetting("#2", "", 2, SettingType.Global);
            deploy2.PossibleValues.Add(new SettingOption("Golems", 1));
            deploy2.PossibleValues.Add(new SettingOption("Ground Funnelling", 2));
            deploy2.PossibleValues.Add(new SettingOption("Giants", 3));
            deploy2.PossibleValues.Add(new SettingOption("Heroes", 4));
            deploy2.PossibleValues.Add(new SettingOption("Wall Breakers", 5));
            deploy2.PossibleValues.Add(new SettingOption("Normal Troops", 6));
            deploy2.PossibleValues.Add(new SettingOption("Balloons", 7));
            deploy2.PossibleValues.Add(new SettingOption("BabyDragons", 8));
            deploy2.PossibleValues.Add(new SettingOption("Lava", 9));
            deploy2.PossibleValues.Add(new SettingOption("Dragons", 10));
            deploy2.PossibleValues.Add(new SettingOption("Air Funnelling", 11));
            deploy2.HideInUiWhen.Add(new SettingOption("use custom deploy order", 0));
            settings.DefineSetting(deploy2);

            var deploy3 = new AlgorithmSetting("#3", "", 3, SettingType.Global);
            deploy3.PossibleValues.Add(new SettingOption("Golems", 1));
            deploy3.PossibleValues.Add(new SettingOption("Ground Funnelling", 2));
            deploy3.PossibleValues.Add(new SettingOption("Giants", 3));
            deploy3.PossibleValues.Add(new SettingOption("Heroes", 4));
            deploy3.PossibleValues.Add(new SettingOption("Wall Breakers", 5));
            deploy3.PossibleValues.Add(new SettingOption("Normal Troops", 6));
            deploy3.PossibleValues.Add(new SettingOption("Balloons", 7));
            deploy3.PossibleValues.Add(new SettingOption("BabyDragons", 8));
            deploy3.PossibleValues.Add(new SettingOption("Lava", 9));
            deploy3.PossibleValues.Add(new SettingOption("Dragons", 10));
            deploy3.PossibleValues.Add(new SettingOption("Air Funnelling", 11));
            deploy3.HideInUiWhen.Add(new SettingOption("use custom deploy order", 0));
            settings.DefineSetting(deploy3);

            var deploy4 = new AlgorithmSetting("#4", "", 4, SettingType.Global);
            deploy4.PossibleValues.Add(new SettingOption("Golems", 1));
            deploy4.PossibleValues.Add(new SettingOption("Ground Funnelling", 2));
            deploy4.PossibleValues.Add(new SettingOption("Giants", 3));
            deploy4.PossibleValues.Add(new SettingOption("Heroes", 4));
            deploy4.PossibleValues.Add(new SettingOption("Wall Breakers", 5));
            deploy4.PossibleValues.Add(new SettingOption("Normal Troops", 6));
            deploy4.PossibleValues.Add(new SettingOption("Balloons", 7));
            deploy4.PossibleValues.Add(new SettingOption("BabyDragons", 8));
            deploy4.PossibleValues.Add(new SettingOption("Lava", 9));
            deploy4.PossibleValues.Add(new SettingOption("Dragons", 10));
            deploy4.PossibleValues.Add(new SettingOption("Air Funnelling", 11));
            deploy4.HideInUiWhen.Add(new SettingOption("use custom deploy order", 0));
            settings.DefineSetting(deploy4);

            var deploy5 = new AlgorithmSetting("#5", "", 5, SettingType.Global);
            deploy5.PossibleValues.Add(new SettingOption("Golems", 1));
            deploy5.PossibleValues.Add(new SettingOption("Ground Funnelling", 2));
            deploy5.PossibleValues.Add(new SettingOption("Giants", 3));
            deploy5.PossibleValues.Add(new SettingOption("Heroes", 4));
            deploy5.PossibleValues.Add(new SettingOption("Wall Breakers", 5));
            deploy5.PossibleValues.Add(new SettingOption("Normal Troops", 6));
            deploy5.PossibleValues.Add(new SettingOption("Balloons", 7));
            deploy5.PossibleValues.Add(new SettingOption("BabyDragons", 8));
            deploy5.PossibleValues.Add(new SettingOption("Lava", 9));
            deploy5.PossibleValues.Add(new SettingOption("Dragons", 10));
            deploy5.PossibleValues.Add(new SettingOption("Air Funnelling", 11));
            deploy5.HideInUiWhen.Add(new SettingOption("use custom deploy order", 0));
            settings.DefineSetting(deploy5);

            var deploy6 = new AlgorithmSetting("#6", "", 6, SettingType.Global);
            deploy6.PossibleValues.Add(new SettingOption("Golems", 1));
            deploy6.PossibleValues.Add(new SettingOption("Ground Funnelling", 2));
            deploy6.PossibleValues.Add(new SettingOption("Giants", 3));
            deploy6.PossibleValues.Add(new SettingOption("Heroes", 4));
            deploy6.PossibleValues.Add(new SettingOption("Wall Breakers", 5));
            deploy6.PossibleValues.Add(new SettingOption("Normal Troops", 6));
            deploy6.PossibleValues.Add(new SettingOption("Balloons", 7));
            deploy6.PossibleValues.Add(new SettingOption("BabyDragons", 8));
            deploy6.PossibleValues.Add(new SettingOption("Lava", 9));
            deploy6.PossibleValues.Add(new SettingOption("Dragons", 10));
            deploy6.PossibleValues.Add(new SettingOption("Air Funnelling", 11));
            deploy6.HideInUiWhen.Add(new SettingOption("use custom deploy order", 0));
            settings.DefineSetting(deploy6);

            return settings;
        }
        #endregion



        public override IEnumerable<int> AttackRoutine()
        {
            Log.Info($"[{AttackName}] V{Version} Deploy start");
            
            // Get user settings
            ClanCastleSettings = GetCurrentSetting("use Clan Castle troops as");
            QWSettings = GetCurrentSetting("Use Queen Walk");
            HealerOnQWSettings = GetCurrentSetting("Number of healers to use on Queen");
            RageOnQWSettings = GetCurrentSetting("Drop 1 rage in the first of the QW");
            int customOrder = GetCurrentSetting("use custom deploy order");
            Debug = GetCurrentSetting("Debug Mode") == 1 ? true : false;

            // Set core point
            AllInOnePushHelper.SetCore();

            // Set the target
            var targetFromUserSettings = GetCurrentSetting("Select Your Target");
            foreach (var t in AllInOnePushHelper.SeTarget(targetFromUserSettings))
                yield return t;

            // Set Origin
            var originPoints = new[]
            {
                new PointFT(GameGrid.DeployExtents.MaxX, Core.Y),
                new PointFT(GameGrid.DeployExtents.MinX, Core.Y),
                new PointFT(Core.X, GameGrid.DeployExtents.MaxY),
                new PointFT(Core.X, GameGrid.DeployExtents.MinY)
            };
            Origin = originPoints.OrderBy(point => point.DistanceSq(Target)).First();

            // Check attack type (air or ground attack)
            AllInOnePushHelper.IsAirAttack();

            // Set deployment points
            AllInOnePushHelper.SetDeployPoints();

            // Set custom order list
            if (customOrder == 1)
            {
                CustomOrderList = new List<int>
                {
                    GetCurrentSetting("#1"),
                    GetCurrentSetting("#2"),
                    GetCurrentSetting("#3"),
                    GetCurrentSetting("#4"),
                    GetCurrentSetting("#5"),
                    GetCurrentSetting("#6")
                };
            }

            // Deploy troops
            if (!IsAirAttack)
            {
                Log.Info($"[{AttackName}] Ground attack start");
                foreach (var t in TroopsDeployment.GroundAttack(customOrder))
                    yield return t;
            }
            else
            {
                Log.Info($"[{AttackName}] Air attack start");
                foreach (var t in TroopsDeployment.AirAttack(customOrder))
                    yield return t;
            }

            yield break;
        }

        public override double ShouldAccept()
        {
            if (Opponent.MeetsRequirements(BaseRequirements.All))
                    return 1;
            return 0;
        }

        public override string ToString()
        {
            return "All In One Push (© CobraTST)";
        }

        public AllInOnePushDeploy(Opponent opponent) : base(opponent)
        {
        }
    }
}