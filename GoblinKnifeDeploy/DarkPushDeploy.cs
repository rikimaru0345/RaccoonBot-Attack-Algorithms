using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using CoC_Bot.Modules.Helpers;
using CustomAlgorithmSettings;

namespace GoblinKnifeDeploy
{
    [AttackAlgorithm("DarkPushDeploy", "Bowler attack to push trophies")]
    internal class DarkPushDeploy : BaseAttack
    {
        RectangleT border;
        Container<PointFT> orgin;
        Tuple<PointFT, PointFT> attackLine, hasteLine, hasteLine2, rageLine, rageLine2;
        PointFT hastePoint, QWHealear, queenRagePoint, nearestWall, core, earthQuakePoint, healPoint, ragePoint, ragePoint2, target, jumpPoint, jumpPoint1, red, red1, red2;
        bool targetIsSet = false, watchHeroes = false, watchQueen = false, useJump = false, isWarden = false, QW = false, debug, isFunneled, airAttack;
        int bowlerFunnelCount, witchFunnelCount, healerFunnlCount, jumpSpellCount;
        DeployElement freezeSpell;
        const string Version = "1.2.8.132";
        const string AttackName = "Dark Push Deploy";
        const float MinDistace = 18f;

        /// <summary>
        /// Returns a Custom Setting's Current Value.  The setting Name must be defined in the DefineSettings Function for this algorithm.
        /// </summary>
        /// <param name="settingName">Name of the setting to Get</param>
        /// <returns>Current Value of the setting.</returns>
        internal int CurrentSetting(string settingName)
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
            SettingsController.Instance.ShowSettingsWindow(AttackName);
        }

        /// <summary>
        /// Called from the Bot Framework when the bot is closing.
        /// </summary>
        public static void OnShutdown()
        {
            //Save settings for this algorithm.
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
            var HealersForQueenWalk = new AlgorithmSetting("Number of healers to use on Queen", "How meny healers to follow the queen , the rest will be dropped on the main troops", 4, SettingType.ActiveAndDead);
            HealersForQueenWalk.MinValue = 1;
            HealersForQueenWalk.MaxValue = 8;
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

            var customDeployOrder = new AlgorithmSetting("use custom deploy order", "Change the deploying troops order, the default order is: 1-Golems if more than 1, 2- funnling, 3-giants or one golem, 4-heroes, 5-wallBreakers, 6-Normal troops", 0, SettingType.ActiveAndDead);
            customDeployOrder.PossibleValues.Add(new SettingOption("Off", 0));
            customDeployOrder.PossibleValues.Add(new SettingOption("On", 1));
            settings.DefineSetting(customDeployOrder);

            // deploy order if custom deploy is on
            var deploy1 = new AlgorithmSetting("#1", "", 1, SettingType.ActiveAndDead);
            deploy1.PossibleValues.Add(new SettingOption("Golems", 1));
            deploy1.PossibleValues.Add(new SettingOption("funnelling", 2));
            deploy1.PossibleValues.Add(new SettingOption("Giants", 3));
            deploy1.PossibleValues.Add(new SettingOption("Heroes", 4));
            deploy1.PossibleValues.Add(new SettingOption("Wall Breakers", 5));
            deploy1.PossibleValues.Add(new SettingOption("Noraml Troops", 6));
            deploy1.HideInUiWhen.Add(new SettingOption("use custom deploy order", 0));
            settings.DefineSetting(deploy1);

            var deploy2 = new AlgorithmSetting("#2", "", 2, SettingType.ActiveAndDead);
            deploy2.PossibleValues.Add(new SettingOption("Golems", 1));
            deploy2.PossibleValues.Add(new SettingOption("funnelling", 2));
            deploy2.PossibleValues.Add(new SettingOption("Giants", 3));
            deploy2.PossibleValues.Add(new SettingOption("Heroes", 4));
            deploy2.PossibleValues.Add(new SettingOption("Wall Breakers", 5));
            deploy2.PossibleValues.Add(new SettingOption("Noraml Troops", 6));
            deploy2.HideInUiWhen.Add(new SettingOption("use custom deploy order", 0));
            settings.DefineSetting(deploy2);

            var deploy3 = new AlgorithmSetting("#3", "", 3, SettingType.ActiveAndDead);
            deploy3.PossibleValues.Add(new SettingOption("Golems", 1));
            deploy3.PossibleValues.Add(new SettingOption("funnelling", 2));
            deploy3.PossibleValues.Add(new SettingOption("Giants", 3));
            deploy3.PossibleValues.Add(new SettingOption("Heroes", 4));
            deploy3.PossibleValues.Add(new SettingOption("Wall Breakers", 5));
            deploy3.PossibleValues.Add(new SettingOption("Noraml Troops", 6));
            deploy3.HideInUiWhen.Add(new SettingOption("use custom deploy order", 0));
            settings.DefineSetting(deploy3);

            var deploy4 = new AlgorithmSetting("#4", "", 4, SettingType.ActiveAndDead);
            deploy4.PossibleValues.Add(new SettingOption("Golems", 1));
            deploy4.PossibleValues.Add(new SettingOption("funnelling", 2));
            deploy4.PossibleValues.Add(new SettingOption("Giants", 3));
            deploy4.PossibleValues.Add(new SettingOption("Heroes", 4));
            deploy4.PossibleValues.Add(new SettingOption("Wall Breakers", 5));
            deploy4.PossibleValues.Add(new SettingOption("Noraml Troops", 6));
            deploy4.HideInUiWhen.Add(new SettingOption("use custom deploy order", 0));
            settings.DefineSetting(deploy4);

            var deploy5 = new AlgorithmSetting("#5", "", 5, SettingType.ActiveAndDead);
            deploy5.PossibleValues.Add(new SettingOption("Golems", 1));
            deploy5.PossibleValues.Add(new SettingOption("funnelling", 2));
            deploy5.PossibleValues.Add(new SettingOption("Giants", 3));
            deploy5.PossibleValues.Add(new SettingOption("Heroes", 4));
            deploy5.PossibleValues.Add(new SettingOption("Wall Breakers", 5));
            deploy5.PossibleValues.Add(new SettingOption("Noraml Troops", 6));
            deploy5.HideInUiWhen.Add(new SettingOption("use custom deploy order", 0));
            settings.DefineSetting(deploy5);

            var deploy6 = new AlgorithmSetting("#6", "", 6, SettingType.ActiveAndDead);
            deploy6.PossibleValues.Add(new SettingOption("Golems", 1));
            deploy6.PossibleValues.Add(new SettingOption("funnelling", 2));
            deploy6.PossibleValues.Add(new SettingOption("Giants", 3));
            deploy6.PossibleValues.Add(new SettingOption("Heroes", 4));
            deploy6.PossibleValues.Add(new SettingOption("Wall Breakers", 5));
            deploy6.PossibleValues.Add(new SettingOption("Noraml Troops", 6));
            deploy6.HideInUiWhen.Add(new SettingOption("use custom deploy order", 0));
            settings.DefineSetting(deploy6);

            return settings;
        }

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
        /// check how meny walls in the EQ area 
        /// </summary>
        /// <param name="point">pointFT of EQ </param>
        /// <returns>true or false</returns>
        int GetWallsInsideEQ(PointFT eqPoint, float eqRadius = 3.5f)
        {
            var walls = Wall.Find();
            var eqWalls = walls.Count(w => w.Location.X >= eqPoint.X - eqRadius && w.Location.X <= eqPoint.X + eqRadius && w.Location.Y >= eqPoint.Y - eqRadius && w.Location.Y <= eqPoint.Y + eqRadius && w.Location.GetCenter().DistanceSq(eqPoint) <= eqRadius * eqRadius);

            return eqWalls;
        }

        IEnumerable<Wall> GetNearstWallInFrontOfDeployPoint(float PointOfDeployXorY, string DirctionOfWalls)
        {
            IEnumerable<Wall> wallsToTarget;
            if (DirctionOfWalls == "Y")
                wallsToTarget = Wall.Find().Where(w => ((int)w.Location.GetCenter().Y == (int)PointOfDeployXorY) || ((int)w.Location.GetCenter().Y + 1 == (int)PointOfDeployXorY) || ((int)w.Location.GetCenter().Y - 1 == (int)PointOfDeployXorY) || ((int)w.Location.GetCenter().Y - 2 == (int)PointOfDeployXorY) || ((int)w.Location.GetCenter().Y + 2 == (int)PointOfDeployXorY));
            else
                wallsToTarget = Wall.Find().Where(w => ((int)w.Location.GetCenter().X == (int)PointOfDeployXorY) || ((int)w.Location.GetCenter().X + 1 == (int)PointOfDeployXorY) || ((int)w.Location.GetCenter().X - 1 == (int)PointOfDeployXorY) || ((int)w.Location.GetCenter().X - 2 == (int)PointOfDeployXorY) || ((int)w.Location.GetCenter().X + 2 == (int)PointOfDeployXorY));
            return wallsToTarget;
        }


        public override double ShouldAccept()
        {
            if (Opponent.MeetsRequirements(BaseRequirements.All))
            {
                Log.Debug($"[{AttackName}] searching for TownHall ....");
                var TH = TownHall.Find()?.Location.GetCenter();
                if (TH == null)
                {
                    Log.Debug("Couldn't found TH .. we will skip this base");
                    Log.Error("We can't find TownHall .. skipping this base");
                    return 0;
                }
                else
                {
                    return 1;
                }
            }
            return 0;
        }

        void CreateDeployPoints()
        {
            // Top right side
            var topRight = new PointFT((float)GameGrid.DeployExtents.MaxX, (float)GameGrid.MaxY - 2);
            var rightTop = new PointFT((float)GameGrid.DeployExtents.MaxX, (float)GameGrid.MinY + 2);

            // Bottom right side
            var rightBottom = new PointFT((float)GameGrid.MaxX - 2, (float)GameGrid.DeployExtents.MinY);
            var bottomRight = new PointFT((float)GameGrid.MinX + 8, (float)GameGrid.DeployExtents.MinY);
            
            // Bottom left side
            // Move 8 tiles from bottom corner due to unitsbar.
            var bottomLeft = new PointFT((float)GameGrid.DeployExtents.MinX, (float)GameGrid.MinY + 8);
            var leftBottom = new PointFT((float)GameGrid.DeployExtents.MinX, (float)GameGrid.MaxY - 2);

            // Top Left side
            var leftTop = new PointFT((float)GameGrid.MinX + 2, (float)GameGrid.DeployExtents.MaxY);
            var topLeft = new PointFT((float)GameGrid.MaxX - 2, (float)GameGrid.DeployExtents.MaxY);

            if (orgin.Item.X > core.X)
            {
                Log.Info($"[{AttackName}] Attacking from the top right");

                attackLine = new Tuple<PointFT, PointFT>(topRight, rightTop);

                var distance = orgin.Item.X - this.target.X;
                var target = distance >= MinDistace ? this.target : core;

                var wallsToTarget = GetNearstWallInFrontOfDeployPoint(orgin.Item.Y, "Y");
                nearestWall = orgin.Item;
                if (wallsToTarget?.Count() > 0)
                    nearestWall = wallsToTarget.OrderByDescending(w => w.Location.GetCenter().X).First().Location.GetCenter();

                var maxX = nearestWall.X - 5f;
                var start = target.X + 4f;

                var earthQuakePoints = new List<PointFT> { new PointFT(target.X + 6f, core.Y) };
                var jumpPoints = new List<PointFT> { new PointFT(target.X + 5.5f, core.Y) };

                if (GetWallsInsideEQ(earthQuakePoints[0], 4f) < 8)
                {
                    while (maxX > start)
                    {
                        earthQuakePoints.Add(new PointFT(start, core.Y));
                        jumpPoints.Add(new PointFT(start - 0.5f, core.Y));
                        start += 0.25f;
                    }
                }


                earthQuakePoint = earthQuakePoints.OrderByDescending(e => GetWallsInsideEQ(e)).FirstOrDefault();
                jumpPoint = jumpPoints.OrderByDescending(e => GetWallsInsideEQ(e)).FirstOrDefault();

                jumpPoint1 = new PointFT(nearestWall.X - 2.75f, core.Y);

                ragePoint = new PointFT(orgin.Item.X - 10f, core.Y);
                healPoint = new PointFT(orgin.Item.X - 16f, core.Y);
                ragePoint2 = new PointFT(orgin.Item.X - 21f, core.Y);
                hastePoint = new PointFT(orgin.Item.X - 25f, core.Y);

                orgin = new Container<PointFT> { Item = new PointFT(GameGrid.DeployExtents.MaxX, orgin.Item.Y) };

                //try to find better funneling points
                var frac = 0.65f;

                red1 = new PointFT(orgin.Item.X + frac * (attackLine.Item1.X - orgin.Item.X),
                             orgin.Item.Y + frac * (attackLine.Item1.Y - orgin.Item.Y));

                red1 = new PointFT(GameGrid.DeployExtents.MaxX, red1.Y);

                red2 = new PointFT(orgin.Item.X + frac * (attackLine.Item2.X - orgin.Item.X),
                             orgin.Item.Y + frac * (attackLine.Item2.Y - orgin.Item.Y));

                red2 = new PointFT(GameGrid.DeployExtents.MaxX, red2.Y);

                hasteLine = new Tuple<PointFT, PointFT>(new PointFT(red1.X - 10f, red1.Y), new PointFT(red2.X - 10f, red2.Y));
                rageLine = new Tuple<PointFT, PointFT>(new PointFT(red1.X - 18f, red1.Y), new PointFT(red2.X - 18f, red2.Y));

                hasteLine2 = new Tuple<PointFT, PointFT>(new PointFT(red1.X - 23f, red1.Y), new PointFT(red2.X - 23f, red2.Y));
                rageLine2 = new Tuple<PointFT, PointFT>(new PointFT(red1.X - 23f, red1.Y), new PointFT(red2.X - 23f, red2.Y));

                QWHealear = new PointFT(GameGrid.DeployExtents.MaxX, red1.Y);
                queenRagePoint = new PointFT(red1.X - 1, red1.Y);
            }

            else if (orgin.Item.X < core.X)
            {
                Log.Info($"[{AttackName}] Attacking from the bottom left");

                attackLine = new Tuple<PointFT, PointFT>(leftBottom, bottomLeft);

                var distance = (orgin.Item.X - this.target.X) * -1;
                var target = distance >= MinDistace ? this.target : core;

                var wallsToTarget = GetNearstWallInFrontOfDeployPoint(orgin.Item.Y, "Y");
                //set default value to the nearst wall if there is no walls
                nearestWall = orgin.Item;
                if (wallsToTarget?.Count() > 0)
                    nearestWall = wallsToTarget.OrderBy(w => w.Location.GetCenter().X).First().Location.GetCenter();

                var maxX = nearestWall.X + 5f;
                var start = target.X - 4f;

                var earthQuakePoints = new List<PointFT> { new PointFT(target.X - 6f, core.Y) };
                var jumpPoints = new List<PointFT> { new PointFT(target.X - 5.5f, core.Y) };

                if (GetWallsInsideEQ(earthQuakePoints[0], 4f) < 8)
                {
                    while (maxX < start)
                    {
                        earthQuakePoints.Add(new PointFT(start, core.Y));
                        jumpPoints.Add(new PointFT(start + 0.5f, core.Y));
                        start -= 0.25f;
                    }
                }

                earthQuakePoint = earthQuakePoints.OrderByDescending(e => GetWallsInsideEQ(e)).FirstOrDefault();
                jumpPoint = jumpPoints.OrderByDescending(e => GetWallsInsideEQ(e)).FirstOrDefault();

                jumpPoint1 = new PointFT(nearestWall.X + 2.75f, core.Y);

                ragePoint = new PointFT(orgin.Item.X + 10f, core.Y);
                healPoint = new PointFT(orgin.Item.X + 16f, core.Y);
                ragePoint2 = new PointFT(orgin.Item.X + 21f, core.Y);
                hastePoint = new PointFT(orgin.Item.X + 25f, core.Y);

                orgin = new Container<PointFT> { Item = new PointFT(GameGrid.DeployExtents.MinX, orgin.Item.Y) };

                //try to find better funneling points
                var frac = 0.65f;

                red1 = new PointFT(orgin.Item.X + frac * (attackLine.Item1.X - orgin.Item.X),
                             orgin.Item.Y + frac * (attackLine.Item1.Y - orgin.Item.Y));

                red1 = new PointFT(GameGrid.DeployExtents.MinX, red1.Y);

                red2 = bottomLeft;

                hasteLine = new Tuple<PointFT, PointFT>(new PointFT(red1.X + 10f, red1.Y), new PointFT(red2.X + 10f, red2.Y));
                rageLine = new Tuple<PointFT, PointFT>(new PointFT(red1.X + 18f, red1.Y), new PointFT(red2.X + 18f, red2.Y));

                hasteLine2 = new Tuple<PointFT, PointFT>(new PointFT(red1.X + 23f, red1.Y), new PointFT(red2.X + 23f, red2.Y));
                rageLine2 = new Tuple<PointFT, PointFT>(new PointFT(red1.X + 23f, red1.Y), new PointFT(red2.X + 23f, red2.Y));

                QWHealear = new PointFT(GameGrid.DeployExtents.MinX, red1.Y);
                queenRagePoint = new PointFT(red1.X + 1, red1.Y);
            }

            else if (orgin.Item.Y > core.Y)
            {
                Log.Info($"[{AttackName}] Attacking from the top left");

                attackLine = new Tuple<PointFT, PointFT>(leftTop, topLeft);

                var distance = orgin.Item.Y - this.target.Y;
                var target = distance >= MinDistace ? this.target : core;

                var wallsToTarget = GetNearstWallInFrontOfDeployPoint(orgin.Item.X, "X");
                nearestWall = orgin.Item;
                if (wallsToTarget?.Count() > 0)
                    nearestWall = wallsToTarget.OrderByDescending(w => w.Location.GetCenter().Y).First().Location.GetCenter();

                var maxX = nearestWall.Y - 5f;
                var start = target.Y + 4f;

                var earthQuakePoints = new List<PointFT> { new PointFT(core.X, target.Y + 6f) };
                var jumpPoints = new List<PointFT> { new PointFT(core.X, target.Y + 5.5f) };

                if (GetWallsInsideEQ(earthQuakePoints[0], 4f) < 8)
                {
                    while (maxX > start)
                    {
                        earthQuakePoints.Add(new PointFT(core.X, start));
                        jumpPoints.Add(new PointFT(core.X, start - 0.5f));
                        start += 0.25f;
                    }
                }


                earthQuakePoint = earthQuakePoints.OrderByDescending(e => GetWallsInsideEQ(e)).FirstOrDefault();
                jumpPoint = jumpPoints.OrderByDescending(e => GetWallsInsideEQ(e)).FirstOrDefault();

                jumpPoint1 = new PointFT(core.X, nearestWall.Y - 2.75f);

                ragePoint = new PointFT(core.X, orgin.Item.Y - 10f);
                healPoint = new PointFT(core.X, orgin.Item.Y - 16f);
                ragePoint2 = new PointFT(core.X, orgin.Item.Y - 21f);
                hastePoint = new PointFT(core.X, orgin.Item.Y - 25f);

                orgin = new Container<PointFT> { Item = new PointFT(orgin.Item.X, GameGrid.DeployExtents.MaxY) };

                //try to find better funneling points
                var frac = 0.65f;

                red1 = new PointFT(orgin.Item.X + frac * (attackLine.Item1.X - orgin.Item.X),
                             orgin.Item.Y + frac * (attackLine.Item1.Y - orgin.Item.Y));

                red1 = new PointFT(red1.X, GameGrid.DeployExtents.MaxY);

                red2 = new PointFT(orgin.Item.X + frac * (attackLine.Item2.X - orgin.Item.X),
                             orgin.Item.Y + frac * (attackLine.Item2.Y - orgin.Item.Y));

                red2 = new PointFT(red2.X, GameGrid.DeployExtents.MaxY);

                hasteLine = new Tuple<PointFT, PointFT>(new PointFT(red1.X, red1.Y - 10f), new PointFT(red2.X, red2.Y - 10f));
                rageLine = new Tuple<PointFT, PointFT>(new PointFT(red1.X, red1.Y - 18f), new PointFT(red2.X, red2.Y - 18f));

                hasteLine2 = new Tuple<PointFT, PointFT>(new PointFT(red1.X, red1.Y - 23f), new PointFT(red2.X, red2.Y - 23f));
                rageLine2 = new Tuple<PointFT, PointFT>(new PointFT(red1.X, red1.Y - 23f), new PointFT(red2.X, red2.Y - 23f));

                QWHealear = new PointFT(red1.X, GameGrid.DeployExtents.MaxY);
                queenRagePoint = new PointFT(red1.X, red1.Y - 1);
            }

            else // (orgin.Y < core.Y)
            {
                Log.Info($"[{AttackName}] Attacking from the bottom right");

                attackLine = new Tuple<PointFT, PointFT>(rightBottom, bottomRight);

                var distance = (orgin.Item.Y - this.target.Y) * -1;
                var target = distance >= MinDistace ? this.target : core;

                var wallsToTarget = GetNearstWallInFrontOfDeployPoint(orgin.Item.X, "X");
                nearestWall = orgin.Item;
                if (wallsToTarget?.Count() > 0)
                    nearestWall = wallsToTarget.OrderBy(w => w.Location.GetCenter().Y).First().Location.GetCenter();



                var maxX = nearestWall.Y + 5f;
                var start = target.Y - 4f;

                var earthQuakePoints = new List<PointFT> { new PointFT(core.X, target.Y - 6f) };
                var jumpPoints = new List<PointFT> { new PointFT(core.X, target.Y - 5.5f) };
                if (GetWallsInsideEQ(earthQuakePoints[0], 4f) < 8)
                {
                    while (maxX < start)
                    {
                        start -= 0.25f;
                        earthQuakePoints.Add(new PointFT(core.X, start));
                        jumpPoints.Add(new PointFT(core.X, start + 0.5f));

                    }
                }

                earthQuakePoint = earthQuakePoints.OrderByDescending(e => GetWallsInsideEQ(e)).FirstOrDefault();
                jumpPoint = jumpPoints.OrderByDescending(e => GetWallsInsideEQ(e)).FirstOrDefault();

                jumpPoint1 = new PointFT(core.X, nearestWall.Y + 2.75f);

                ragePoint = new PointFT(core.X, orgin.Item.Y + 10f);
                healPoint = new PointFT(core.X, orgin.Item.Y + 16f);
                ragePoint2 = new PointFT(core.X, orgin.Item.Y + 21f);
                hastePoint = new PointFT(core.X, orgin.Item.Y + 25f);

                orgin = new Container<PointFT> { Item = new PointFT(orgin.Item.X, GameGrid.DeployExtents.MinY) };

                //try to find better funneling points
                var frac = 0.65f;

                red1 = new PointFT(orgin.Item.X + frac * (attackLine.Item1.X - orgin.Item.X),
                             orgin.Item.Y + frac * (attackLine.Item1.Y - orgin.Item.Y));

                red1 = new PointFT(red1.X, GameGrid.DeployExtents.MinY);
                
                red2 = bottomRight;

                

                hasteLine = new Tuple<PointFT, PointFT>(new PointFT(red1.X, red1.Y + 10f), new PointFT(red2.X, red2.Y + 10f));
                rageLine = new Tuple<PointFT, PointFT>(new PointFT(red1.X, red1.Y + 18f), new PointFT(red2.X, red2.Y + 18f));

                hasteLine2 = new Tuple<PointFT, PointFT>(new PointFT(red1.X, red1.Y + 23f), new PointFT(red2.X, red2.Y + 23f));
                rageLine2 = new Tuple<PointFT, PointFT>(new PointFT(red1.X, red1.Y + 23f), new PointFT(red2.X, red2.Y + 23f));

                QWHealear = new PointFT(red1.X, GameGrid.DeployExtents.MinY);
                queenRagePoint = new PointFT(red1.X, red1.Y + 1);
            }

            red = new PointFT(attackLine.Item2.X + 0.5f * (attackLine.Item1.X - attackLine.Item2.X),
                             attackLine.Item2.Y + 0.5f * (attackLine.Item1.Y - attackLine.Item2.Y));
        }

        public override IEnumerable<int> AttackRoutine()
        {
            //analysis the base for the main attack points
            var getOutRedArea = 0.70f;

            // don't include corners in case build huts are there
            var maxRedPointX = (GameGrid.RedPoints.Where(p => -18 < p.Y && p.Y < 18)?.Max(point => point.X) + getOutRedArea ?? GameGrid.RedZoneExtents.MaxX);
            var minRedPointX = (GameGrid.RedPoints.Where(p => -18 < p.Y && p.Y < 18)?.Min(point => point.X) - getOutRedArea ?? GameGrid.RedZoneExtents.MinX);
            var maxRedPointY = (GameGrid.RedPoints.Where(p => -18 < p.X && p.X < 18)?.Max(point => point.Y) + getOutRedArea ?? GameGrid.RedZoneExtents.MaxY);
            var minRedPointY = (GameGrid.RedPoints.Where(p => -18 < p.X && p.X < 18)?.Min(point => point.Y) - getOutRedArea ?? GameGrid.RedZoneExtents.MinY);
            // build a box around the base
            var left = new PointFT(minRedPointX, maxRedPointY);
            var top = new PointFT(maxRedPointX, maxRedPointY);
            var right = new PointFT(maxRedPointX, minRedPointY);
            var bottom = new PointFT(minRedPointX, minRedPointY);

            // border around the base
            border = new RectangleT((int)minRedPointX, (int)maxRedPointY, (int)(maxRedPointX - minRedPointX), (int)(minRedPointY - maxRedPointY));

            // core is center of the box
            core = border.GetCenter();

            //set the target
            var settingsTarget = CurrentSetting("Select Your Target");

            if (settingsTarget == 2)
            {
                foreach (var f in setTarget(EagleArtillery.Find(CacheBehavior.ForceScan)))
                    yield return f;

                if (targetIsSet == false)
                {
                    foreach (var f in setTarget(TownHall.Find(CacheBehavior.ForceScan)))
                        yield return f;
                }
            }
            else if(settingsTarget == 0)
            {
                foreach (var f in setTarget(TownHall.Find(CacheBehavior.ForceScan)))
                    yield return f;

                if (targetIsSet == false)
                {
                    target = core;
                    targetIsSet = true;
                }
            }
            else if(settingsTarget == 1)
            {
                foreach (var f in setTarget(DarkElixirStorage.Find(CacheBehavior.ForceScan)?.FirstOrDefault()))
                    yield return f;

                if (targetIsSet == false)
                {
                    foreach (var f in setTarget(TownHall.Find(CacheBehavior.ForceScan)))
                        yield return f;
                }
            }

            if (targetIsSet == false)
            {
                target = core;
            }

            IEnumerable<int> setTarget(Building building)
            {
                var target = building?.Location.GetCenter();
                if (target == null)
                {
                    for (var i = 2; i <= 4; i++)
                    {
                        Log.Warning($"Bot didn't find the target .. we will attemp search NO. {i}");
                        yield return 1000;
                        target = building?.Location.GetCenter();
                        if (target != null)
                        {
                            Log.Warning($"Target found after {i} retries");
                            this.target = (PointFT)target;
                            targetIsSet = true;
                            break;
                        }
                    }
                }
                else
                {
                    this.target = (PointFT)target;
                    targetIsSet = true;
                }
            }
            var orginPoints = new[]
            {
                new PointFT(GameGrid.DeployExtents.MaxX, core.Y),
                new PointFT(GameGrid.DeployExtents.MinX, core.Y),
                new PointFT(core.X, GameGrid.DeployExtents.MaxY),
                new PointFT(core.X, GameGrid.DeployExtents.MinY)
            };
            
            orgin = new Container<PointFT> { Item = orginPoints.OrderBy(point => point.DistanceSq(target)).First() };

            Log.Info($"[{AttackName}] V{Version} Deploy start");

            CreateDeployPoints();
            
            //get troops (under respect of the user settings)
            var deployElements = Deploy.GetTroops();

            var clanCastle = deployElements.ExtractOne(u => u.ElementType == DeployElementType.ClanTroops);

            //spells
            var earthQuakeSpell = deployElements.Extract(u => u.Id == DeployId.Earthquake);
            var ragespell = deployElements.Extract(u => u.Id == DeployId.Rage);
            var healspell = deployElements.Extract(u => u.Id == DeployId.Heal);
            freezeSpell = deployElements.ExtractOne(DeployId.Freeze);
            var jumpSpell = deployElements.Extract(u => u.Id == DeployId.Jump);
            var hasteSpell = deployElements.Extract(u => u.Id == DeployId.Haste);
            var poison = deployElements.Extract(u => u.Id == DeployId.Poison);
            //tanks
            var giant = deployElements.ExtractOne(DeployId.Giant);
            var golem = deployElements.ExtractOne(DeployId.Golem);
            //main troops
            var wallbreaker = deployElements.ExtractOne(DeployId.WallBreaker);
            var bowler = deployElements.ExtractOne(DeployId.Bowler);
            var witch = deployElements.ExtractOne(DeployId.Witch);
            var healer = deployElements.ExtractOne(DeployId.Healer);
            var spells = deployElements.Extract(DeployElementType.Spell);

            var wizard = deployElements.ExtractOne(DeployId.Wizard);


            var heroes = deployElements.Extract(x => x.IsHero);
            var lava = deployElements.ExtractOne(DeployId.LavaHound);

            airAttack = lava?.Count > 0 ? true : false;


            //get warden in a seperated member
            var warden = heroes.ExtractOne(u => u.ElementType == DeployElementType.HeroWarden);
            var queen = heroes.ExtractOne(DeployId.Queen);

            isWarden = warden?.Count > 0 ? true : false;

            debug = CurrentSetting("Debug Mode") == 1 ? true : false;

            //get user adnavced settings
            int clanCastleSettings = CurrentSetting("use Clan Castle troops as");
            int QWSettings = CurrentSetting("Use Queen Walk");
            int healerOnQWSettings = CurrentSetting("Number of healers to use on Queen");
            int rageOnQWSettings = CurrentSetting("Drop 1 rage in the first of the QW");
            int customOrder = CurrentSetting("use custom deploy order");

            
            if (!airAttack)
            {
                //open near to dark elixer with 4 earthquakes
                var EQCount = earthQuakeSpell?.Sum(u => u.Count);
                if (EQCount >= 4)
                {
                    Log.Info($"[{AttackName}] break walls beside TownHall ");
                    foreach (var unit in earthQuakeSpell)
                    {
                        unit.Select();
                        foreach (var t in Deploy.AtPoint(unit, earthQuakePoint, unit.Count))
                            yield return t;
                    }

                    yield return 1500;

                    if (debug)
                        DebugEQpells();
                }
                else
                {
                    useJump = true;
                    if (debug)
                        DebugJumpspells();
                }


                IEnumerable<int> deployGolems()
                {
                    if (golem?.Count >= 2)
                    {
                        Log.Info($"[{AttackName}] deploy Golems troops .. ");
                        foreach (var t in Deploy.AlongLine(golem, attackLine.Item1, attackLine.Item2, golem.Count, golem.Count))
                            yield return t;
                        if (clanCastle?.Count > 0 && clanCastleSettings == 1)
                        {
                            foreach (var t in Deploy.AtPoint(clanCastle, orgin))
                                yield return t;
                        }

                        yield return 1000;

                        var waves = wizard?.Count >= 12 ? 2 : 1;
                        foreach (var f in DeployWizard(waves))
                            yield return f;
                    }
                    else if (golem?.Count == 1 && clanCastleSettings == 1 && clanCastle?.Count > 0)
                    {
                        foreach (var t in Deploy.AtPoint(golem, red1, golem.Count))
                            yield return t;

                        foreach (var t in Deploy.AtPoint(clanCastle, red2))
                            yield return t;

                        yield return 1000;

                        var waves = wizard?.Count >= 12 ? 2 : 1;
                        foreach (var f in DeployWizard(waves))
                            yield return f;
                    }
                    else
                    {
                        if (clanCastle?.Count > 0 && clanCastleSettings == 1)
                        {
                            foreach (var t in Deploy.AtPoint(clanCastle, orgin))
                                yield return t;
                        }
                    }
                }

                IEnumerable<int> deployFunnlling()
                {
                    Log.Info($"[{AttackName}] deploy funnelling troops on sides");

                    QW = QWSettings == 1 && queen?.Count > 0 && healer?.Count >= healerOnQWSettings ? true : false;
                    if (QW)
                    {
                        if (debug)
                            DebugQueenWalk();

                        foreach (var t in Deploy.AtPoint(queen, red1))
                            yield return t;

                        yield return 400;

                        foreach (var t in Deploy.AtPoint(healer, QWHealear, healerOnQWSettings))
                            yield return t;

                        Deploy.WatchHeroes(new List<DeployElement> { queen });

                        if (rageOnQWSettings == 1)
                        {
                            var rageCount = ragespell?.Sum(u => u.Count);
                            if (rageCount > 0)
                            {
                                foreach (var unit in ragespell)
                                {
                                    unit.Select();
                                    foreach (var t in Deploy.AtPoint(unit, queenRagePoint))
                                        yield return t;
                                }
                            }
                        }

                        yield return 10000;

                        if (!isFunneled)
                        {
                            if (bowler?.Count > 0)
                            {
                                bowlerFunnelCount = bowler.Count / 4;
                                foreach (var t in Deploy.AtPoint(bowler, red2, bowlerFunnelCount))
                                    yield return t;
                            }
                            if (witch?.Count > 4)
                            {
                                witchFunnelCount = witch.Count / 4;
                                foreach (var t in Deploy.AtPoint(witch, red2, witchFunnelCount))
                                    yield return t;
                            }

                            if (healer?.Count > 0)
                            {
                                foreach (var t in Deploy.AtPoint(healer, red2, healer.Count))
                                    yield return t;
                            }

                            yield return 5000;
                        }
                    }
                    else
                    {
                        if ((bowler?.Count > 0 || witch?.Count > 0) && !isFunneled)
                        {
                            if (bowler?.Count > 0)
                            {
                                bowlerFunnelCount = bowler.Count / 4;
                                foreach (var t in Deploy.AtPoint(bowler, red1, bowlerFunnelCount))
                                    yield return t;
                            }
                            if (witch?.Count > 0)
                            {
                                witchFunnelCount = witch.Count > 4 ? witch.Count / 4 : witch.Count / 2;
                                foreach (var t in Deploy.AtPoint(witch, red1, witchFunnelCount))
                                    yield return t;
                            }

                            if (healer?.Count >= 2)
                            {
                                healerFunnlCount = healer.Count <= 4 ? healer.Count / 2 : healer.Count / 3;
                                foreach (var t in Deploy.AtPoint(healer, red1, healerFunnlCount))
                                    yield return t;
                            }

                            if (bowler?.Count > 0)
                            {
                                foreach (var t in Deploy.AtPoint(bowler, red2, bowlerFunnelCount))
                                    yield return t;
                            }
                            if (witchFunnelCount > 0 && witch?.Count > 0)
                            {
                                foreach (var t in Deploy.AtPoint(witch, red2, witchFunnelCount))
                                    yield return t;
                            }

                            if (healer?.Count > 0 && healerFunnlCount > 0)
                            {
                                foreach (var t in Deploy.AtPoint(healer, red2, healerFunnlCount))
                                    yield return t;
                            }
                            yield return 13000;
                        }
                    }
                }

                IEnumerable<int> deployGiants()
                {
                    jumpSpellCount = jumpSpell?.Sum(u => u.Count) > 0 ? jumpSpell.Sum(u => u.Count) : 0;
                    if ((useJump && jumpSpellCount >= 2) || (!useJump && jumpSpellCount >= 1))
                    {
                        foreach (var unit in jumpSpell)
                        {
                            foreach (var t in Deploy.AtPoint(unit, jumpPoint1))
                                yield return t;
                        }
                    }

                    if (giant?.Count > 0)
                    {
                        Log.Info($"[{AttackName}] deploy Giants ...");
                        foreach (var t in Deploy.AlongLine(giant, red1, red2, 8, 4))
                            yield return t;

                        var waves = wizard?.Count >= 12 ? 2 : 1;
                        foreach (var f in DeployWizard(waves))
                            yield return f;

                        foreach (var f in deployWB())
                            yield return f;

                        foreach (var t in Deploy.AtPoint(giant, orgin, giant.Count))
                            yield return t;
                    }

                    if (clanCastle?.Count > 0 && clanCastleSettings == 2)
                    {
                        foreach (var t in Deploy.AtPoint(clanCastle, orgin))
                            yield return t;
                    }

                    //if one golem deploy after funnlling
                    if (golem?.Count > 0)
                    {
                        Log.Info($"[{AttackName}] deploy Golem ...");
                        foreach (var t in Deploy.AlongLine(golem, attackLine.Item1, attackLine.Item2, golem.Count, golem.Count))
                            yield return t;
                    }
                }
                
                IEnumerable<int> deployHeroes()
                {
                    Log.Info($"[{AttackName}] droping heroes");
                    if (heroes.Any())
                    {
                        foreach (var hero in heroes.Where(u => u.Count > 0))
                        {
                            foreach (var t in Deploy.AtPoint(hero, orgin))
                                yield return t;
                        }
                        watchHeroes = true;
                    }
                    
                    if (queen?.Count > 0)
                    {
                        foreach (var t in Deploy.AtPoint(queen, orgin))
                            yield return t;
                        watchQueen = true;
                    }
                    if (isWarden)
                    {
                        foreach (var t in Deploy.AtPoint(warden, orgin))
                            yield return t;
                    }
                }

                IEnumerable<int> DeployWizard(int waves = 1)
                {
                    if (wizard?.Count > 0)
                    {
                        var count = wizard.Count / waves;
                        if (!isFunneled)
                        {
                            foreach (var t in Deploy.AlongLine(wizard, attackLine.Item1, attackLine.Item2, wizard.Count, count))
                                yield return t;

                            isFunneled = true;
                        }
                        else
                        {
                            foreach (var t in Deploy.AlongLine(wizard, red1, red2, count, 4))
                                yield return t;
                        }
                    }
                }

                IEnumerable<int> deployWB()
                {
                    if (wallbreaker?.Count > 0)
                    {
                        Log.Info($"[{AttackName}] droping wallBreakers");
                        while (wallbreaker?.Count > 0)
                        {
                            var count = wallbreaker.Count;
                            Log.Info($"[{AttackName}] send wall breakers in groups");
                            foreach (var t in Deploy.AtPoint(wallbreaker, orgin, 3))
                                yield return t;

                            yield return 400;
                            // prevent infinite loop if deploy point is on red
                            if (wallbreaker.Count != count) continue;

                            Log.Warning($"[{AttackName}] Couldn't deploy {wallbreaker.PrettyName}");
                            break;
                        }
                    }
                }

                IEnumerable<int> deployNormalTroops()
                {
                    Log.Info($"[{AttackName}] deploy rest of troops");

                    if(witch?.Count>4)
                    {
                        if (bowler?.Count > 0)
                        {
                            foreach (var t in Deploy.AlongLine(bowler, red1, red2, bowlerFunnelCount, 4))
                                yield return t;
                        }
                    }
                    
                    
                    if (bowler?.Count > 0)
                    {
                        foreach (var t in Deploy.AtPoint(bowler, orgin, bowler.Count))
                            yield return t;
                    }
                    if (witch?.Count > 0)
                    {
                        foreach (var t in Deploy.AtPoint(witch, orgin, witch.Count))
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

                    foreach (var w in DeployWizard())
                        yield return w;
                }

                if (customOrder == 1)
                {
                    var order = new List<int>
                {
                    CurrentSetting("#1"),
                    CurrentSetting("#2"),
                    CurrentSetting("#3"),
                    CurrentSetting("#4"),
                    CurrentSetting("#5"),
                    CurrentSetting("#6")
                };
                    //use custom order
                    foreach (var s in DeployInCustomOrder(order))
                        yield return s;
                }
                else
                {
                    //use default order
                    QW = QWSettings == 1 && queen?.Count > 0 && healer?.Count >= healerOnQWSettings ? true : false;
                    if (QW)
                    {
                        foreach (var s in deployFunnlling())
                            yield return s;
                        foreach (var s in deployGolems())
                            yield return s;
                    }
                    else
                    {
                        foreach (var s in deployGolems())
                            yield return s;
                        foreach (var s in deployFunnlling())
                            yield return s;
                    }
                    foreach (var s in deployGiants())
                        yield return s;
                    foreach (var s in deployHeroes())
                        yield return s;
                    foreach (var s in deployWB())
                        yield return s;
                    foreach (var s in deployNormalTroops())
                        yield return s;

                }

                IEnumerable<int> DeployInCustomOrder(List<int> order)
                {
                    foreach (var o in order)
                    {
                        switch (o)
                        {
                            case 1:
                                foreach (var s in deployGolems())
                                    yield return s;
                                break;
                            case 2:
                                foreach (var s in deployFunnlling())
                                    yield return s;
                                break;
                            case 3:
                                foreach (var s in deployGiants())
                                    yield return s;
                                break;
                            case 4:
                                foreach (var s in deployHeroes())
                                    yield return s;
                                break;
                            case 5:
                                foreach (var s in deployWB())
                                    yield return s;
                                break;
                            case 6:
                                foreach (var s in deployNormalTroops())
                                    yield return s;
                                break;
                        }
                    }
                }
                //deploy spells
                if (useJump && jumpSpellCount > 0)
                {
                    Log.Info($"[{AttackName}] deploy jump next to Townhall");
                    foreach (var unit in jumpSpell)
                    {
                        unit.Select();
                        foreach (var t in Deploy.AtPoint(unit, jumpPoint))
                            yield return t;
                        break;
                    }
                }
                //deploy spells

                int rageSpellCount = (int)ragespell?.Sum(u => u.Count);
                int hasteSpellCount = (int)hasteSpell?.Sum(u => u.Count);
                int healSpellCount = (int)healspell?.Sum(u => u.Count);

                if (rageSpellCount > 0)
                {
                    foreach (var unit in ragespell)
                    {
                        foreach (var t in Deploy.AtPoint(unit, ragePoint))
                            yield return t;
                        break;
                    }
                    rageSpellCount--;
                }

                yield return 2000;

                if (rageSpellCount > 1)
                {
                    foreach (var unit in ragespell)
                    {
                        foreach (var t in Deploy.AtPoint(unit, healPoint))
                            yield return t;

                        break;
                    }
                    rageSpellCount--;
                }
                else if(healSpellCount == 0)
                {
                    if (hasteSpellCount > 0)
                    {
                        
                        foreach (var unit in hasteSpell)
                        {
                            foreach (var t in Deploy.AtPoint(unit, healPoint))
                                yield return t;

                            break;
                        }
                        hasteSpellCount--;
                    }
                }
                if(poison.Sum(u => u.Count) > 0)
                {
                    foreach(var unit in poison)
                    {
                        foreach (var t in Deploy.AtPoint(unit, healPoint))
                            yield return t;
                    }
                }
                if (healSpellCount > 0)
                {
                    foreach (var unit in healspell)
                    {
                        foreach (var t in Deploy.AtPoint(unit, healPoint))
                            yield return t;
                        break;
                    }
                    healSpellCount--;
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

                yield return 3000;
                // activate Grand Warden apility
                if (isWarden)
                {
                    var heroList = new List<DeployElement> { warden };
                    TryActivateHeroAbilities(heroList, true, 1000);
                }

                if (rageSpellCount > 0) 
                {
                    foreach (var unit in ragespell)
                    {
                        unit.Select();
                        foreach (var t in Deploy.AtPoint(unit, ragePoint2))
                            yield return t;
                    }
                }
                
                if (healSpellCount > 0) 
                {
                    foreach (var unit in healspell)
                    {
                        foreach (var t in Deploy.AtPoint(unit, ragePoint2))
                            yield return t;
                    }
                }

                yield return 1000;

                if (hasteSpellCount > 0)
                {
                    foreach (var unit in hasteSpell)
                    {
                        unit.Select();
                        foreach (var t in Deploy.AtPoint(unit, hastePoint))
                            yield return t;
                    }
                }

                // Start watching heroes
                if (watchHeroes == true)
                {
                    Deploy.WatchHeroes(heroes);
                }

                if(watchQueen == true)
                {
                    Deploy.WatchHeroes(new List<DeployElement> { queen });
                }

                if (debug)
                    DebugSpells();
            }
            else //air attack
            {
                Log.Info($"[{AttackName}] V{Version} 'Air Attack' has been activated");

                var balloon = deployElements.ExtractOne(DeployId.Balloon);
                var minion = deployElements.ExtractOne(DeployId.Minion);
                var babyDragon = deployElements.ExtractOne(DeployId.BabyDragon);
                var dragon = deployElements.ExtractOne(DeployId.Dragon);

                var dragonAttack = dragon?.Count >= 6 ? true : false;
                var babyLoon = babyDragon?.Count >= 7 ? true : false;
                var lavaloonion = minion?.Count >= 10 ? true : false;

                var lightingSpell = spells.ExtractOne(DeployId.Lightning);

                IEnumerable<int> zapAirDefense()
                {
                    var airDefenses = AirDefense.Find(CacheBehavior.ForceScan);
                    var targetAirDefense = airDefenses.OrderBy(a => a.Location.GetCenter().DistanceSq(orgin.Item)).ElementAtOrDefault(2);
                    if(targetAirDefense == null)
                        targetAirDefense = airDefenses.OrderBy(a => a.Location.GetCenter().DistanceSq(orgin.Item)).ElementAtOrDefault(1);
                    if (targetAirDefense == null)
                        targetAirDefense = airDefenses.FirstOrDefault();

                    var zapPoint = targetAirDefense.Location.GetCenter();


                    if (earthQuakeSpell?.Sum(u => u.Count) > 0)
                    {
                        foreach (var unit in earthQuakeSpell)
                        {
                            foreach (var t in Deploy.AtPoint(unit, zapPoint))
                                yield return t;
                            break;
                        }
                    }

                    foreach (var t in Deploy.AtPoint(lightingSpell, zapPoint, 2)) 
                        yield return t;

                    yield return 1200;
                }

                if(lightingSpell?.Count >= 2)
                {
                    foreach (var t in zapAirDefense())
                        yield return t;
                }

                if (lightingSpell?.Count >= 2)
                {
                    foreach (var t in zapAirDefense())
                        yield return t;
                }

                if (dragon?.Count > 0)
                {
                    foreach (var t in Deploy.AtPoint(dragon, red1))
                        yield return t;

                    foreach (var t in Deploy.AtPoint(dragon, red2))
                        yield return t;

                    yield return 8000;

                    foreach (var t in Deploy.AlongLine(dragon, red1, red2, dragon.Count, 4)) 
                        yield return t;
                }

                if(babyDragon?.Count > 0)
                {
                    foreach (var t in Deploy.AtPoint(babyDragon, red1))
                        yield return t;

                    foreach (var t in Deploy.AtPoint(babyDragon, red2))
                        yield return t;

                    yield return 4000;

                    foreach (var t in Deploy.AlongLine(babyDragon, red1, red2, babyDragon.Count, 4))
                        yield return t;
                }

                yield return dragonAttack ? 2000 : 800;

                if(balloon?.Count > 0)
                {
                    if(!dragonAttack)
                    {
                        foreach (var t in Deploy.AlongLine(balloon, attackLine.Item1, attackLine.Item2, balloon.Count, 4))
                            yield return t;
                    }else
                    {
                        var count = balloon.Count / 2;
                        foreach (var t in Deploy.AtPoint(balloon, red1, count)) 
                            yield return t;

                        foreach (var t in Deploy.AtPoint(balloon, red2, count)) 
                        yield return t;
                    }
                }

                if (lava.Count >= 2)
                {
                    var count = lava.Count / 2;

                    foreach (var t in Deploy.AtPoints(lava, new PointFT[] { red1, red2 }, count, 0, 200, 5))
                        yield return t;

                    if (lava?.Count > 0) 
                    {
                        foreach (var t in Deploy.AtPoint(lava, red2, lava.Count))
                            yield return t;
                    }

                    if(clanCastle?.Count > 0 && clanCastleSettings > 0)
                    {
                        foreach (var t in Deploy.AtPoint(clanCastle, red1))
                            yield return t;
                    }
                }
                else
                {
                    if(clanCastle?.Count > 0 && clanCastleSettings > 0)
                    {
                        foreach (var t in Deploy.AtPoint(clanCastle, red1))
                            yield return t;

                        foreach (var t in Deploy.AtPoint(lava, red2))
                            yield return t;
                    }
                }

                if(clanCastle?.Count > 0)
                {
                    foreach (var t in Deploy.AtPoint(clanCastle, orgin))
                        yield return t;
                }

                if (isWarden)
                {
                    foreach (var t in Deploy.AtPoint(warden, orgin))
                        yield return t;
                }
                //setting witch spell to start and the the line for deployment
                var firstSpell = hasteSpell?.Sum(u => u.Count) >= ragespell?.Sum(u => u.Count) ? hasteSpell : ragespell;
                var secondSpell = firstSpell == hasteSpell ? ragespell : hasteSpell;

                var firstSpellUnit = firstSpell.FirstOrDefault()?.Count > 0 ? firstSpell.FirstOrDefault() : firstSpell.LastOrDefault();
                var secondSpellUnit = secondSpell.FirstOrDefault()?.Count > 0 ? secondSpell.FirstOrDefault() : secondSpell.LastOrDefault();

                var line = hasteLine;

                if (firstSpellUnit?.Count > 0)
                {
                    var count = firstSpellUnit.Count >= 3 ? 3 : firstSpellUnit.Count;
                    foreach (var t in Deploy.AlongLine(firstSpellUnit, line.Item1, line.Item2, count, count))
                        yield return t;

                    line = rageLine;
                }

                if (minion?.Count > 0)
                {
                    foreach (var t in Deploy.AlongLine(minion, attackLine.Item1, attackLine.Item2, minion.Count, 4))
                        yield return t;
                }

                yield return 5000;

                if (secondSpellUnit?.Count > 0)
                {
                    var count = secondSpellUnit.Count >= 3 ? 3 : secondSpellUnit.Count;
                    foreach (var t in Deploy.AlongLine(secondSpellUnit, line.Item1, line.Item2, count, count))
                        yield return t;
                    
                    line = hasteLine2;
                }
                else
                {                    
                    if (firstSpell?.Sum(u => u.Count) > 0)
                    {
                        firstSpellUnit = firstSpell.FirstOrDefault().Count > 0 ? firstSpell.FirstOrDefault() : firstSpell.LastOrDefault();
                        var count = firstSpellUnit.Count >= 3 ? 3 : firstSpellUnit.Count;
                        foreach (var t in Deploy.AlongLine(firstSpellUnit, line.Item1, line.Item2, count, count))
                            yield return t;
                        
                        line = hasteLine2;
                    }
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

                if (isWarden)
                {
                    var heroList = new List<DeployElement> { warden };
                    TryActivateHeroAbilities(heroList, true, 1000);
                }

                yield return 4000;

                if (firstSpell?.Sum(u => u.Count) > 0)
                {
                    foreach(var unit in firstSpell)
                    {
                        var count = unit.Count >= 3 ? 3 : unit.Count;
                        foreach (var t in Deploy.AlongLine(unit, line.Item1, line.Item2, count, count))
                            yield return t;
                    }
                    
                    line = rageLine2;
                }
                
                if (secondSpell?.Sum(u => u.Count) > 0)
                {
                    secondSpellUnit = secondSpell.FirstOrDefault().Count > 0 ? secondSpell.FirstOrDefault() : secondSpell.LastOrDefault();
                    foreach (var unit in secondSpell)
                    {
                        var count = unit.Count >= 3 ? 3 : unit.Count;
                        foreach (var t in Deploy.AlongLine(unit, line.Item1, line.Item2, count, count))
                            yield return t;
                    }
                    
                }

                if(wallbreaker?.Count > 0)
                {
                    while(wallbreaker.Count > 0)
                    {
                        var count = wallbreaker.Count;
                        foreach (var t in Deploy.AtPoint(wallbreaker, orgin, 3))
                            yield return t;

                        yield return 400;

                        // prevent infinite loop if deploy point is on red
                        if (wallbreaker.Count != count) continue;

                        Log.Warning($"[{AttackName}] Couldn't deploy {wallbreaker.PrettyName}");
                    }
                }

                if(heroes.Any())
                {
                    foreach (var hero in heroes.Where(u => u.Count > 0))
                    {
                        foreach (var t in Deploy.AtPoint(hero, orgin))
                            yield return t;
                    }
                    Deploy.WatchHeroes(heroes);
                }


                if (queen?.Count > 0)
                {
                    foreach (var t in Deploy.AtPoint(queen, orgin))
                        yield return t;
                    Deploy.WatchHeroes(new List<DeployElement> { queen });
                }
            }
        }

        public override string ToString()
        {
            return "Dark Push Deploy";
        }


        void DebugQueenWalk()
        {
            using (var bmp = Screenshot.Capture())
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    // find the radius of 5 tiles
                    var p1 = new PointFT(0f, 0f).ToScreenAbsolute();
                    var p2 = new PointFT(0f, 5f).ToScreenAbsolute();
                    var distance = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

                    //draw new deploy points for funnling troops
                    Visualize.RectangleT(bmp, new RectangleT((int)red1.X, (int)red1.Y, 1, 1), new Pen(Color.Blue));

                    //draw rectangle around the target
                    Visualize.RectangleT(bmp, new RectangleT((int)target.X, (int)target.Y, 4, 4), new Pen(Color.Blue));
                    
                    Visualize.CircleT(bmp, queenRagePoint, 5, Color.Magenta, 64, 0);
                }
                var d = DateTime.UtcNow;
                Screenshot.Save(bmp, $"{AttackName} Queen Walk {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}");
            }
        }

        void DebugJumpspells()
        {
            using (var bmp = Screenshot.Capture())
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    Visualize.RectangleT(bmp, new RectangleT((int)red1.X, (int)red1.Y, 1, 1), new Pen(Color.Blue));
                    Visualize.RectangleT(bmp, new RectangleT((int)red2.X, (int)red2.Y, 1, 1), new Pen(Color.Blue));

                    Visualize.RectangleT(bmp, new RectangleT((int)nearestWall.X, (int)nearestWall.Y, 1, 1), new Pen(Color.White));

                    //draw rectangle around the target
                    Visualize.RectangleT(bmp, new RectangleT((int)target.X, (int)target.Y, 4, 4), new Pen(Color.Blue));
                    
                    Visualize.CircleT(bmp, jumpPoint, 3.5f, Color.DarkGreen, 64, 0);
                    Visualize.CircleT(bmp, jumpPoint1, 3.5f, Color.DarkGreen, 64, 0);
                }
                var d = DateTime.UtcNow;
                Screenshot.Save(bmp, $"{AttackName} Jump Spells {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}");
            }
        }

        void DebugEQpells()
        {
            using (var bmp = Screenshot.Capture())
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    
                    Visualize.RectangleT(bmp, new RectangleT((int)red1.X, (int)red1.Y, 1, 1), new Pen(Color.Blue));
                    Visualize.RectangleT(bmp, new RectangleT((int)red2.X, (int)red2.Y, 1, 1), new Pen(Color.Blue));
                    Visualize.RectangleT(bmp, new RectangleT((int)red.X, (int)red.Y, 1, 1), new Pen(Color.Yellow));

                    Visualize.RectangleT(bmp, new RectangleT((int)orgin.Item.X, (int)orgin.Item.Y, 1, 1), new Pen(Color.Red));

                    Visualize.RectangleT(bmp, new RectangleT((int)nearestWall.X, (int)nearestWall.Y, 1, 1), new Pen(Color.White));

                    //draw rectangle around the target
                    Visualize.RectangleT(bmp, new RectangleT((int)target.X, (int)target.Y, 3, 3), new Pen(Color.Blue));

                    Visualize.CircleT(bmp, jumpPoint1, 3.5f, Color.DarkGreen, 64, 0);


                    Visualize.CircleT(bmp, earthQuakePoint, 4, Color.SandyBrown, 64, 0);
                }
                var d = DateTime.UtcNow;
                Screenshot.Save(bmp, $"{AttackName} EQ Spells {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}");
            }
        }

        void DebugSpells()
        {
            using (var bmp = Screenshot.Capture())
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    Visualize.RectangleT(bmp, border, new Pen(Color.FromArgb(128, Color.Red)));

                    //g.DrawLine(new Pen(Color.FromArgb(192, Color.Orange)), attackLine.Item1.ToScreenAbsolute(), attackLine.Item2.ToScreenAbsolute());

                    //draw new deploy points for funnling troops
                    Visualize.RectangleT(bmp, new RectangleT((int)red1.X, (int)red1.Y, 1, 1), new Pen(Color.Blue));
                    Visualize.RectangleT(bmp, new RectangleT((int)red2.X, (int)red2.Y, 1, 1), new Pen(Color.Blue));

                    //draw rectangle around the target
                    Visualize.RectangleT(bmp, new RectangleT((int)target.X, (int)target.Y, 3, 3), new Pen(Color.Blue));

                    Visualize.CircleT(bmp, ragePoint, 5, Color.Magenta, 64, 0);
                    Visualize.CircleT(bmp, ragePoint2, 5, Color.Magenta, 64, 0);
                    Visualize.CircleT(bmp, healPoint, 5, Color.Yellow, 64, 0);
                }
                var d = DateTime.UtcNow;
                Screenshot.Save(bmp, $"{AttackName} Spells {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}");
            }
        }
    }
}
