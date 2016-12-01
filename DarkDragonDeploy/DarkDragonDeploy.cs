using System;
using System.Collections.Generic;
using System.Linq;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using SharedCode;
using System.Drawing;
using System.Text;
using System.Reflection;

[assembly: Addon("DarkDragonDeploy Addon", "Contains the Dark Dragon deploy algorithm", "Bert")]

namespace DarkDragonDeploy
{
    [AttackAlgorithm("DarkDragonDeploy", "Deploys Dragons and use Zap Quake To Maximize chance of Getting Dark Elixir Storage.")]
    class DarkDragonDeploy : BaseAttack
    {

        public DarkDragonDeploy(Opponent opponent) : base(opponent) { }

        #region Private Member Variables
        private List<DeployElement>_deployElements = null;
        private const string _tag = "[Dark Dragon]";
        private Target _darkElixirStorage;
        private PointFT[] _deFunnelPoints;
        private PointFT[] _balloonFunnelPoints;
        private AirDefense[] _airDefenses;
        private bool _zapped1 = false;
        private bool _zapped2 = false;
        private bool _watchHeroes = false;
        #endregion

        #region Name of Deploy
        public override string ToString()
        {
            return "Dark Dragon Deploy";
        }
        #endregion


        #region *******  ShouldAccept  *******
        public override double ShouldAccept()
        {
            if (!PassesBasicAcceptRequirements())
                return 0;

            //TODO - Check which kind of army we have trained. Calculate an Air Offense Score, and Ground Offense Score.

            //TODO - Find all Base Defenses, and calculate an AIR and Ground Defensive Score.  

            //TODO - From Collector/Storage fill levels, determine if loot is in Collectors, or Storages... (Will help to decide which alg to use.)

            //Verify that the Attacking Army contains at least 6 Dragons.
            _deployElements = Deploy.GetTroops();
            var dragons = _deployElements.FirstOrDefault(u => u.Id == DeployId.Dragon);
            if (dragons == null || dragons?.Count < 6)
            {
                Log.Error($"{_tag} Army not correct! - Dark Dragon Deploy Requires at least 6 Dragons to function Properly. (You have {dragons?.Count ?? 0} dragons)");
                return 0;
            }

            if (_deployElements.Count >= 11)
            {
                //Possibly Too Many Deployment Elements!  Bot Doesnt Scroll - Change Army Composition to have less than 12 unit types!
                Log.Warning($"{_tag} Warning! Full Army! - The Bot does not scroll through choices when deploying units... If your army has more than 11 unit types, The bot will not see them all, and cannot deploy everything!)");
            }

            //Write out all the unit pretty names we found...
            Log.Debug($"{_tag} Deployable Troops: {ToUnitString(_deployElements)}");

            Log.Info($"{_tag} Base meets minimum Requirements... Checking DE Storage/Air Defense Locations...");

            //Grab the Locations of the DE Storage
            _darkElixirStorage = HumanLikeAlgorithms.TargetDarkElixirStorage();

            if (!_darkElixirStorage.ValidTarget)
            {
                Log.Warning($"{_tag} No Dark Elixir Storage Found - Skipping");
                return 0;
            }

            //Get the locaiton of all Air Defenses
            _airDefenses = AirDefense.Find(CacheBehavior.Default);

            if (_airDefenses.Length == 0)
            {
                Log.Warning($"{_tag} Could not find ANY air defenses - Skipping");
                return 0;
            }

            Log.Info($"{_tag} Found {_airDefenses.Length} Air Defense Buildings.. Continuing Attack..");

            if (_airDefenses.Length > 1)
            {
                //Now that we found all Air Defenses, order them in the array with closest AD to Target first.
                Array.Sort(_airDefenses, delegate (AirDefense ad1, AirDefense ad2)
                {
                    return HumanLikeAlgorithms.DistanceFromPoint(ad1, _darkElixirStorage.DeployGrunts)
                    .CompareTo(HumanLikeAlgorithms.DistanceFromPoint(ad2, _darkElixirStorage.DeployGrunts));
                });
            }

            //Create the Funnel Points
            _deFunnelPoints = _darkElixirStorage.GetFunnelingPoints(30);
            _balloonFunnelPoints = _darkElixirStorage.GetFunnelingPoints(20);

#if DEBUG
            //During Debug, Create an Image of the base including what we found.
            CreateDebugImages();
#endif

            //We are Good to attack!
            return 1;
        }
        #endregion

        #region *******  AttackRoutine  *******
        public override IEnumerable<int> AttackRoutine()
        {
            Log.Info($"{_tag} Deploy start - V.{Assembly.GetExecutingAssembly().GetName().Version.ToString()}");

            //STEP 1 ******* Destroy all air defenses using Lightling & Quake if needed. *******
            foreach (var t in DestroyAirDefenses())
                yield return t;

            //Pause after killing Air Defenses (to make it look like a person is attacking)
            yield return Rand.Int(1000, 2000);

            //STEP 2 ******* Deploy Dragon funnel and Main Dragon Force. *******
            foreach (var t in DeployDragons())
                yield return t;

            //Pause
            yield return Rand.Int(2000, 3000);

            //STEP 3 ******* Deploy Lava Hounds (if any Exist). *******
            foreach (var t in DeployLavaHounds())
                yield return t;
            
            //Pause for a little while... - Long enough for main dragons to begin to enter the base.
            yield return Rand.Int(3000, 4000);

            //STEP 4 ******* Deploy Ballons And/Or Hogs *******
            foreach (var t in DeployBalloonsAndHogs())
                yield return t;

            //Pause a while... - Then drop the Heros. - They should start going through walls towards the center.
            yield return Rand.Int(7000, 9000);

            //STEP 5 ******* Deploy King (He tanks for wallbreakers a little) *******
            foreach (var t in DeployKing())
                yield return t;

            //Wait for king to be targeted...
            yield return Rand.Int(1000, 1200);

            //STEP 6 ******* Deploy All Wallbreakers (Get the heros going inside the base) *******
            foreach (var t in DeployWallBreakers())
                yield return t;

            //STEP 7 ******* Next Drop the Warden, (if we have one) *******
            foreach (var t in DeployWarden())
                yield return t;

            //Pause a while... - Then drop the Queen, so she starts following the King into the base.
            yield return Rand.Int(2000, 4000);

            //STEP 8 ******* Drop the queen so she will follow the king in. *******
            foreach (var t in DeployQueen())
                yield return t;

            //STEP 9 ******* Now that all heros have been deployed begin watching them and activate ability etc. *******
            WatchHeros();

            //TODO Deploy Baby Drags on the Back End - on Air D's 3 & 4'

            //STEP 10 ******* Deploy the Clan Castle if user settings say to *******
            foreach (var t in DeployClanCastle())
                yield return t;

            //STEP 11 ******* If there is a Rage Spell, Deploy it now - Right in front of the DE Storage! *******
            foreach (var t in DeployRageSpell())
                yield return t;

            //STEP 12 ******* Drop healers on the Heros if healers exist. *******
            foreach (var t in DeployHealers())
                yield return t;

            //Pause for a little while longer...  waiting for things to develop
            yield return Rand.Int(4000, 6000); 

            //STEP 13 ******* Deploy Minions & Others? *******
            foreach (var t in DeployOthers())
                yield return t;

            //TODO If there is a Heal Spell... Deploy it here... (This one will be harder to predict where to drop...) Meh, skipping for now.

            //STEP 14 ******* Use ANY other Spells at this point... (So they are ALL GONE!) *******
            foreach (var t in DeployLeftoverSpells())
                yield return t;

            //STEP 15 ******* Deploy ANY troops left... (So they are ALL GONE!) *******
            foreach (var t in DeployLeftoverTroops())
                yield return t;

            //At this point the attack is fully deployed... just waiting for the timer to run out, or base to be 100% destroyed.

        }
        #endregion


        #region PassesBasicAcceptRequirements
        private bool PassesBasicAcceptRequirements()
        {
            // check if the base meets ALL the user's requirements (One at a time, and log a warning for WHY its skipping)
            if (!Opponent.MeetsRequirements(BaseRequirements.Elixir))
            {
                Log.Warning($"{_tag} Elixir Requirements not Met - Skipping");
                return false;
            }

            if (!Opponent.MeetsRequirements(BaseRequirements.Gold))
            {
                Log.Warning($"{_tag} Gold Requirements not Met - Skipping");
                return false;
            }

            if (!Opponent.MeetsRequirements(BaseRequirements.DarkElixir))
            {
                Log.Warning($"{_tag} Dark Elixir Requirements not Met - Skipping");
                return false;
            }

            if (!Opponent.MeetsRequirements(BaseRequirements.MaxThLevel))
            {
                Log.Warning($"{_tag} Base Over Town Hall Max - Skipping");
                return false;
            }

            if (!Opponent.MeetsRequirements(BaseRequirements.AvoidStrongBases))
            {
                Log.Warning($"{_tag} Strong Base Detected - Skipping");
                return false;
            }

            //Everything meets requirements...
            return true;
        }
        #endregion

        #region CreateDebugImages
        private void CreateDebugImages()
        {
            List<InfernoTower> infernos = InfernoTower.Find(CacheBehavior.Default).ToList();
            List<WizardTower> wizTowers = WizardTower.Find(CacheBehavior.Default).ToList();
            List<ArcherTower> archerTowers = ArcherTower.Find(CacheBehavior.Default).ToList();
            List<ElixirStorage> elixirStorages = ElixirStorage.Find(CacheBehavior.Default).ToList();
            EagleArtillery eagle = EagleArtillery.Find(CacheBehavior.Default);

            var d = DateTime.UtcNow;
            var debugFileName = $"Dragon Deploy {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}";

            using (Bitmap canvas = Screenshot.Capture())
            {
                Screenshot.Save(canvas, $"{debugFileName}_1");

                //Draw some stuff on it.
                Visualize.Axes(canvas);
                Visualize.Grid(canvas, redZone: true);
                Visualize.Target(canvas, _darkElixirStorage.Center, 40, Color.Red);
                Visualize.Target(canvas, _deFunnelPoints[0], 40, Color.White);
                Visualize.Target(canvas, _deFunnelPoints[1], 40, Color.White);
                Visualize.Target(canvas, _balloonFunnelPoints[0], 40, Color.Pink);
                Visualize.Target(canvas, _balloonFunnelPoints[1], 40, Color.Pink);

                for (int i = 0; i < infernos.Count(); i++)
                {
                    Visualize.Target(canvas, infernos.ElementAt(i).Location.GetCenter(), 30, Color.Orange);
                }

                for (int i = 0; i < _airDefenses.Count(); i++)
                {
                    Visualize.Target(canvas, _airDefenses.ElementAt(i).Location.GetCenter(), 30, Color.Cyan);
                }

                for (int i = 0; i < wizTowers.Count(); i++)
                {
                    Visualize.Target(canvas, wizTowers.ElementAt(i).Location.GetCenter(), 30, Color.Purple);
                }

                for (int i = 0; i < archerTowers.Count(); i++)
                {
                    Visualize.Target(canvas, archerTowers.ElementAt(i).Location.GetCenter(), 30, Color.RosyBrown);
                }

                if (eagle != null)
                {
                    Visualize.Target(canvas, eagle.Location.GetCenter(), 30, Color.YellowGreen);
                }

                Visualize.Target(canvas, _darkElixirStorage.DeployGrunts, 40, Color.Beige);

                Screenshot.Save(canvas, $"{debugFileName}_2");
            }

            //Write a text file that goes with all images that shows what is in the image.
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"Town Hall - Level:{_darkElixirStorage.TargetBuilding.Level}");

            for (int i = 0; i < _airDefenses.Count(); i++)
            {
                sb.AppendLine($"Air Defense {i + 1} - Level:{_airDefenses.ElementAt(i).Level}");
            }

            for (int i = 0; i < infernos.Count(); i++)
            {
                sb.AppendLine($"Inferno Tower {i + 1} - Level:{infernos.ElementAt(i).Level}");
            }

            for (int i = 0; i < wizTowers.Count(); i++)
            {
                sb.AppendLine($"Wizard Tower {i + 1} - Level:{wizTowers.ElementAt(i).Level}");
            }

            for (int i = 0; i < archerTowers.Count(); i++)
            {
                sb.AppendLine($"Archer Tower {i + 1} - Level:{archerTowers.ElementAt(i).Level}");
            }

            if (eagle != null)
            {
                sb.AppendLine($"Eagle Artillery 1 - Level:{eagle.Level}");
            }

            //System.IO.File.WriteAllText($@"C:\RaccoonBot\Debug Screenshots\{debugFileName}_3.txt", sb.ToString());

            Log.Info($"{_tag} Deploy Debug Image Saved!");

        }
        #endregion


        #region DestroyAirDefenses
        private IEnumerable<int> DestroyAirDefenses()
        {
            var lightningSpells = _deployElements.FirstOrDefault(u => u.ElementType == DeployElementType.Spell && u.Id == DeployId.Lightning);
            List<DeployElement> earthquakeSpells = _deployElements.Where(u => u.ElementType == DeployElementType.Spell && u.Id == DeployId.Earthquake).ToList();

            var lightningCount = lightningSpells?.Count ?? 0;
            var earthquakeCount = 0;

            //Get a count of all earthquake spells... donated, or brewed...
            foreach (var spell in earthquakeSpells.Where(s => s.Count > 0))
            {
                earthquakeCount += spell.Count;
            }

            if (lightningCount < 3 || (lightningCount < 2 && earthquakeCount < 1))
            {
                //We dont have the Spells to take out the Closest Air Defense... Surrender before we drop any Dragons!
                Log.Error($"{_tag} We dont have the Spells to take out the Closest Air Defense... Surrender");
                Attack.Surrender();
            }
            else
            {
                if (earthquakeCount < 1 && lightningCount >= 3 && _airDefenses.Count() >= 1)
                {
                    _zapped1 = true;
                    Log.Info($"{_tag} Dropping 3 Lightning Spells to take out closest Air Defense...");
                    //Drop 3 Lightning on the closest Air Defense.
                    foreach (var t in Deploy.AtPoint(lightningSpells, _airDefenses.ElementAt(0).Location.GetCenter(), 3))
                        yield return t;
                }

                if (earthquakeCount >= 1 && lightningCount >= 2 && _airDefenses.Count() >= 1)
                {
                    _zapped1 = true;
                    Log.Info($"{_tag} Dropping 2 Lightning Spells & 1 Earthquake to take out closest Air Defense...");
                    //Drop 2 Lightning on the closest Air Defense.
                    var beforeDrop = lightningSpells.Count;
                    foreach (var t in Deploy.AtPoint(lightningSpells, _airDefenses.ElementAt(0).Location.GetCenter(), 1))
                        yield return t;

                    lightningSpells.Recount();
                    yield return Rand.Int(500, 1000);

                    //Only Deploy a 2nd lightning spell if we successfully deployed 1... Seems to be sticking and deploying Two on the first Click. - not sure why, but this fixes it.
                    if (beforeDrop - 1 == lightningSpells.Count)
                    {
                        foreach (var t in Deploy.AtPoint(lightningSpells, _airDefenses.ElementAt(0).Location.GetCenter(), 1))
                            yield return t;
                    }
                    else
                    {
                        Log.Error($"{_tag} First Drop of Lightning actually dropped {beforeDrop - lightningSpells.Count} Lightning Spells. Attempting to Recover & Continue.");
                    }

                    yield return Rand.Int(500, 1000); // pause a little...

                    //Drop 1 Earthquake on the closest Air Defense.
                    foreach (var spell in earthquakeSpells.Where(s => s.Count > 0))
                    {
                        foreach (var t in Deploy.AtPoint(spell, _airDefenses.ElementAt(0).Location.GetCenter(), 1))
                            yield return t;

                        break; //Only deploy one.
                    }
                }

                if (earthquakeCount >= 2 && lightningCount >= 4 && _airDefenses.Count() >= 2)
                {
                    _zapped2 = true;
                    Log.Info($"{_tag} Dropping 2 Lightning Spells & 1 Earthquake to take out 2nd closest Air Defense...");
                    //Drop 2 Lightning on the 2nd closest Air Defense.
                    foreach (var t in Deploy.AtPoint(lightningSpells, _airDefenses.ElementAt(1).Location.GetCenter(), 1))
                        yield return t;

                    yield return Rand.Int(500, 1000);

                    foreach (var t in Deploy.AtPoint(lightningSpells, _airDefenses.ElementAt(1).Location.GetCenter(), 1))
                        yield return t;

                    yield return Rand.Int(500, 1000); // pause a little...

                    //Drop 1 Earthquake on the 2nd closest Air Defense.
                    foreach (var spell in earthquakeSpells.Where(s => s.Count > 0))
                    {
                        foreach (var t in Deploy.AtPoint(spell, _airDefenses.ElementAt(1).Location.GetCenter(), 1))
                            yield return t;

                        break; //Only deploy one.
                    }
                }
            }
        }
        #endregion

        #region DeployDragons
        private IEnumerable<int> DeployDragons()
        {
            var dragons = _deployElements.FirstOrDefault(u => u.Id == DeployId.Dragon);

            if (dragons?.Count > 2)
            {
                Log.Info($"{_tag} Deploying two Dragons to Create a funnel to direct main force at Dark Elixer Storage...");
                //Deploy two dragons - one at each funel point.
                foreach (var t in Deploy.AtPoint(dragons, _deFunnelPoints[0], 1))
                    yield return t;

                yield return Rand.Int(500, 1500);

                foreach (var t in Deploy.AtPoint(dragons, _deFunnelPoints[1], 1))
                    yield return t;

                yield return Rand.Int(1000, 1500); // pause for a little while... - Long enought for dragons to begin to create the funnel.
            }
            else {
                Log.Error($"{_tag} Two Dragons to create the funnel do not exist. {dragons?.Count ?? 0} exists...");
            }

            if (dragons?.Count > 0)
            {
                //Deploy our main force of dragons all on one spot...
                Log.Info($"{_tag} Deploying Main Force of Dragons...");
                foreach (var t in Deploy.AtPoint(dragons, _darkElixirStorage.DeployGrunts, dragons.Count))
                    yield return t;
            }
            else
            {
                Log.Error($"{_tag} When Trying to deploy Main Force of Dragons - None Exist!");
            }

            if (dragons?.Count > 0)
            {
                Log.Error($"{_tag} Main Force of Dragons Not Fully Deployed! Trying to drop them on the Edge of the map...");
                //Find the edge, by adding an arbitrary large distance to the point, and the function will return a safe point always on the map.
                var mapEdge = HumanLikeAlgorithms.Origin.PointOnLineAwayFromEnd(_darkElixirStorage.DeployGrunts, 30);

                foreach (var t in Deploy.AtPoint(dragons, mapEdge, dragons.Count))
                    yield return t;
            }
        }
        #endregion

        #region DeployLavaHounds
        private IEnumerable<int> DeployLavaHounds()
        {
            var lavaHounds = _deployElements.FirstOrDefault(u => u.Id == DeployId.LavaHound);

            //Deploy Lava Hounds - TODO Make the Drop position better for these guys...
            if (lavaHounds?.Count > 0)
            {
                Log.Info($"{_tag} Deploying Lava Hounds...");
                foreach (var t in Deploy.AtPoint(lavaHounds, _darkElixirStorage.DeployGrunts, lavaHounds.Count))
                    yield return t;
            }
            else
            {
                Log.Info($"{_tag} No LavaHounds found to Deploy...");
            }
        }
        #endregion

        #region DeployBalloonsAndHogs
        private IEnumerable<int> DeployBalloonsAndHogs()
        {
            var balloons = _deployElements.FirstOrDefault(u => u.Id == DeployId.Balloon);
            var hogs = _deployElements.FirstOrDefault(u => u.Id == DeployId.HogRider);

            //Add all defense seeeking units to the same list.
            List<DeployElement> defenseSeakers = new List<DeployElement>();
            if (balloons?.Count > 0)
            {
                defenseSeakers.Add(balloons);
            }
            else
            {
                Log.Info($"{_tag} No Balloons found to Deploy...");
            }
            if (hogs?.Count > 0)
            {
                defenseSeakers.Add(hogs);
            }
            else
            {
                Log.Info($"{_tag} No HogRiders found to Deploy...");
            }

            //If we have balloons, and/or Hogs, deploy them now near the Air Defenses...
            foreach (var deploymentElement in defenseSeakers)
            {
                var unitCount = deploymentElement.Count;
                if (unitCount > 0)
                {
                    //Drop the first third.
                    var firstThirdUnitCount = int.Parse(Math.Floor((decimal)unitCount / 3).ToString());
                    if (firstThirdUnitCount > 0)
                    {
                        Log.Info($"{_tag} Deploying First Third of {deploymentElement.PrettyName}({firstThirdUnitCount}) on 1st Funnel Point...");
                        foreach (var t in Deploy.AtPoint(deploymentElement, _balloonFunnelPoints[0], firstThirdUnitCount))
                            yield return t;
                    }

                    var secondThirdUnitCount = int.Parse(Math.Floor((decimal)unitCount / 2).ToString());
                    if (secondThirdUnitCount > 0)
                    {
                        Log.Info($"{_tag} Deploying Second Third of {deploymentElement.PrettyName}({secondThirdUnitCount}) on Main Deploy Point...");
                        foreach (var t in Deploy.AtPoint(deploymentElement, _darkElixirStorage.DeployGrunts, secondThirdUnitCount))
                            yield return t;
                    }

                    //Drop the Remainder.
                    var remainder = deploymentElement.Count; //Whats left
                    if (remainder > 0)
                    {
                        Log.Info($"{_tag} Deploying Remainder of {deploymentElement.PrettyName}({remainder}) on 2nd Funnel Point...");
                        foreach (var t in Deploy.AtPoint(deploymentElement, _balloonFunnelPoints[1], remainder))
                            yield return t;
                    }
                }
            }
        }
        #endregion

        #region DeployWallBreakers
        private IEnumerable<int> DeployWallBreakers()
        {
            var wallBreakers = _deployElements.FirstOrDefault(u => u.Id == DeployId.WallBreaker);

            if (wallBreakers?.Count > 0)
            {
                Log.Info($"{_tag} Deploying {wallBreakers.Count} {wallBreakers.PrettyName}...");
                foreach (var t in Deploy.AtPoint(wallBreakers, _darkElixirStorage.DeployGrunts, wallBreakers.Count))
                    yield return t;
            }
            else
            {
                Log.Info($"{_tag} No WallBreakers found to Deploy...");
            }
        }
        #endregion

        #region DeployHeros

        #region DeployKing
        private IEnumerable<int> DeployKing()
        {
            var king = _deployElements.FirstOrDefault(u => u.IsHero && u.Id == DeployId.King);

            if (UserSettings.UseKing && king != null)
            {
                //Deploy the king
                Log.Info($"{_tag} Deploying King...");
                foreach (var t in Deploy.AtPoint(king, _darkElixirStorage.DeployGrunts))
                    yield return t;

                _watchHeroes = true;
            }
        }
        #endregion

        #region DeployWarden
        private IEnumerable<int> DeployWarden()
        {
            var warden = _deployElements.FirstOrDefault(u => u.IsHero && u.Id == DeployId.Warden);

            if (UserSettings.UseWarden && warden != null)
            {
                Log.Info($"{_tag} Deploying Warden...");
                foreach (var t in Deploy.AtPoint(warden, _darkElixirStorage.DeployRanged))
                    yield return t;
                yield return Rand.Int(500, 1000); //Wait

                _watchHeroes = true;
            }
        }
        #endregion

        #region DeployQueen
        private IEnumerable<int> DeployQueen()
        {
            var queen = _deployElements.FirstOrDefault(u => u.IsHero  && u.Id == DeployId.Queen);

            if (UserSettings.UseQueen && queen != null)
            {
                Log.Info($"{_tag} Deploying Queen...");
                foreach (var t in Deploy.AtPoint(queen, _darkElixirStorage.DeployGrunts))
                    yield return t;
                yield return Rand.Int(500, 1000); //Wait

                _watchHeroes = true;
            }
        }
        #endregion

        #region WatchHeros
        private void WatchHeros()
        {
            var allHeroes = (List<DeployElement>)_deployElements.Where(u => u.IsHero);

            if (_watchHeroes)
            {
                //Watch Heros and Hit ability when they get low.
                Log.Info($"{_tag} Watching Heros to activate Abilities...");
                Deploy.WatchHeroes(allHeroes);
            }
        }
        #endregion

        #endregion

        #region DeployClanCastle
        private IEnumerable<int> DeployClanCastle()
        {
            var clanCastle = _deployElements.FirstOrDefault(u => u.ElementType == DeployElementType.ClanTroops);

            if (clanCastle?.Count > 0 && UserSettings.UseClanTroops)
            {
                Log.Info($"{_tag} Deploying Clan Castle Behind Heros...");
                foreach (var t in Deploy.AtPoint(clanCastle, _darkElixirStorage.DeployGrunts, clanCastle.Count))
                    yield return t;
            }
            else
            {
                Log.Info($"{_tag} No Clan Castle Troops found to Deploy...");
            }
        }
        #endregion

        #region DeployRageSpell
        private IEnumerable<int> DeployRageSpell()
        {
            var rageSpells = _deployElements.FirstOrDefault(u => u.ElementType == DeployElementType.Spell && u.Id == DeployId.Rage);

            if (rageSpells?.Count > 0)
            {
                //Point on line between Center of DE Storage, and The Deploy Point of the Dragons... Such that the spell edge is near the DE Storage.
                var rageDropPoint = _darkElixirStorage.Center.PointOnLineAwayFromStart(_darkElixirStorage.DeployGrunts, 6f);
                Log.Info($"{_tag} Deploying ONE Rage Spell Close to DE Storage....");
                foreach (var t in Deploy.AtPoint(rageSpells, rageDropPoint, 1))
                    yield return t;
            }
            else
            {
                Log.Info($"{_tag} No Rage Spells found to Deploy...");
            }
        }
        #endregion

        #region DeployHealers
        private IEnumerable<int> DeployHealers()
        {
            var healers = _deployElements.FirstOrDefault(u => u.Id == DeployId.Healer);

            if (healers?.Count > 0)
            {
                Log.Info($"{_tag} Deploying Healers near Heros...");
                foreach (var t in Deploy.AtPoint(healers, _darkElixirStorage.DeployGrunts, healers.Count))
                    yield return t;
            }
            else
            {
                Log.Info($"{_tag} No Healers found to Deploy...");
            }
        }
        #endregion

        #region DeployOthers
        private IEnumerable<int> DeployOthers()
        {
            var minions = _deployElements.FirstOrDefault(u => u.Id == DeployId.Minion);

            List<DeployElement> otherUnits = new List<DeployElement>();
            if (minions?.Count > 0)
            {
                otherUnits.Add(minions);
            }
            else
            {
                Log.Info($"{_tag} No Minions found to Deploy...");
            }

            //Deploy the Rest of the units in as Cleanup.
            foreach (var deploymentElement in otherUnits)
            {
                var unitCount = deploymentElement.Count;
                if (unitCount > 0)
                {
                    //Drop the first Quarter.
                    var firstQuarterCount = int.Parse(Math.Floor((decimal)unitCount / 4).ToString());
                    if (firstQuarterCount > 0)
                    {
                        Log.Info($"{_tag} Deploying First Quarter of {deploymentElement.PrettyName}({firstQuarterCount}) on 1st Funnel Point...");
                        foreach (var t in Deploy.AtPoint(deploymentElement, _deFunnelPoints[0], firstQuarterCount))
                            yield return t;
                    }

                    if (firstQuarterCount > 0) //Second quarter should always be same as first quarter... no need to recalc...
                    {
                        Log.Info($"{_tag} Deploying Second Quarter of {deploymentElement.PrettyName}({firstQuarterCount}) on 2nd Funnel Point...");
                        foreach (var t in Deploy.AtPoint(deploymentElement, _balloonFunnelPoints[0], firstQuarterCount))
                            yield return t;
                    }

                    //Drop the third Quarter. - recalc to be safe.
                    var thirdQuarterCount = int.Parse(Math.Floor((decimal)deploymentElement.Count / 2).ToString());
                    if (thirdQuarterCount > 0)
                    {
                        Log.Info($"{_tag} Deploying Third Quarter of {deploymentElement.PrettyName}({thirdQuarterCount}) on 3st Funnel Point...");
                        foreach (var t in Deploy.AtPoint(deploymentElement, _balloonFunnelPoints[1], thirdQuarterCount))
                            yield return t;
                    }

                    //Drop the Remainder.
                    var remainder = deploymentElement.Count; //Whats left after Dropping on the First AD.
                    if (remainder > 0)
                    {
                        Log.Info($"{_tag} Deploying Remainder of {deploymentElement.PrettyName}({remainder}) on Last Funnel Point...");
                        foreach (var t in Deploy.AtPoint(deploymentElement, _deFunnelPoints[1], remainder))
                            yield return t;
                    }

                }
            }
        }
        #endregion

        #region DeployLeftoverSpells
        private IEnumerable<int> DeployLeftoverSpells()
        {
            var lightningSpells = _deployElements.FirstOrDefault(u => u.ElementType == DeployElementType.Spell && u.Id == DeployId.Lightning);
            List<DeployElement> earthquakeSpells = _deployElements.Where(u => u.ElementType == DeployElementType.Spell && u.Id == DeployId.Earthquake).ToList();
            List<DeployElement> skeletonSpells = _deployElements.Where(u => u.ElementType == DeployElementType.Spell && u.Id == DeployId.Skeleton).ToList();

            List<DeployElement> leftoverSpells = new List<DeployElement>();

            //To Prevent errors, perform a recount on all these.
            leftoverSpells.RecountAndAddIfAny(skeletonSpells);
            leftoverSpells.RecountAndAddIfAny(earthquakeSpells);
            leftoverSpells.RecountAndAddIfAny(lightningSpells);

            //Now if any Skeleton Spells exist, drop them ALL on the last air-D to Distract/Destroy.
            if (leftoverSpells.Count > 0)
            {
                yield return Rand.Int(4000, 6000); // pause a while longer... Dragons should be getting close to this one now...

                var adToDistract = 0;
                if (_zapped1)
                    adToDistract = 1;
                if (_zapped2)
                    adToDistract = 2;

                PointFT dropPoint = new PointFT();
                string locationDesc = string.Empty;

                if (_airDefenses.Length < adToDistract + 1)
                {
                    //Only two Air Defenses were found? No Third to use spells on... drop them on A DE Collector.

                    //Put them on any Elixir Collector still up.
                    var deDrill = DarkElixirDrill.Find(CacheBehavior.ForceScan);

                    if (deDrill.Any())
                    {
                        dropPoint = deDrill[0].Location.GetCenter();
                        locationDesc = "Dark Elixir Drill";
                    }
                    else
                    {
                        //Give up and just drop them in the middle of the map to get rid of them.
                        dropPoint = HumanLikeAlgorithms.Origin;
                        locationDesc = "Center of map - (no other air Defenses or DE Drills Could be found)";
                    }
                }
                else
                {
                    //There were aditional air defenses found... drop them on the next one.
                    dropPoint = _airDefenses[adToDistract].Location.GetCenter();
                    locationDesc = $"Air Defense #{adToDistract + 1}";
                }

                foreach (var spell in leftoverSpells)
                {
                    Log.Info($"{_tag} Deploying {spell.Count} left over {spell.PrettyName} Spell(s) to {locationDesc}...");
                    foreach (var t in Deploy.AtPoint(spell, dropPoint, spell.Count))
                        yield return t;
                }
            }
            else
            {
                Log.Info($"{_tag} All Spells Successfully Deployed...");
            }
        }
        #endregion

        #region DeployLeftoverTroops
        private IEnumerable<int> DeployLeftoverTroops()
        {
            var dragons = _deployElements.FirstOrDefault(u => u.Id == DeployId.Dragon);
            var balloons = _deployElements.FirstOrDefault(u => u.Id == DeployId.Balloon);
            var hogs = _deployElements.FirstOrDefault(u => u.Id == DeployId.HogRider);
            var lavaHounds = _deployElements.FirstOrDefault(u => u.Id == DeployId.LavaHound);
            var minions = _deployElements.FirstOrDefault(u => u.Id == DeployId.Minion);
            var wallBreakers = _deployElements.FirstOrDefault(u => u.Id == DeployId.WallBreaker);
            var healers = _deployElements.FirstOrDefault(u => u.Id == DeployId.Healer);

            //Do a last check for ANY units that we have left... at this point if they haven't been deployed, Its probably an error. 
            //Try and dump anything that is left on the edge of the map so the next time Troops are built, it doesnt hang the bot.
            var leftoverTroops = new List<DeployElement>();

            leftoverTroops.RecountAndAddIfAny(dragons);
            leftoverTroops.RecountAndAddIfAny(balloons);
            leftoverTroops.RecountAndAddIfAny(hogs);
            leftoverTroops.RecountAndAddIfAny(lavaHounds);
            leftoverTroops.RecountAndAddIfAny(minions);
            leftoverTroops.RecountAndAddIfAny(wallBreakers);
            leftoverTroops.RecountAndAddIfAny(healers);

            if (leftoverTroops.Count > 0)
            {
                //Spot in the middle of the y axis, and the Min Edge of the X axis... (Should ALWAYS be able to dump here)
                var dumpSpot = new PointFT(GameGrid.MinX - .5f, 0f);

                foreach (var troop in leftoverTroops)
                {
                    Log.Error($"{_tag} Deploying {troop.Count} left over {troop.PrettyName}s to edge of map, to get rid of troops.  This should not happen, but does sometimes when troops are not properly deployed in earlier phases of the algorithm.");
                    foreach (var t in Deploy.AtPoint(troop, dumpSpot, troop.Count))
                        yield return t;
                }
            }
            else
            {
                Log.Info($"{_tag} All Troops Successfully Deployed...");
            }
        }
        #endregion

    }
}

