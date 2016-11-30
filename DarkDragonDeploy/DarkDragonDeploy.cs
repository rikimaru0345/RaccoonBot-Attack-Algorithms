using System;
using System.Collections.Generic;
using System.Linq;
using CoC_Bot;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using CoC_Bot.Internals;
using System.Threading;
using SharedCode;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Text;
using System.Reflection;

[assembly: Addon("DarkDragonDeploy Addon", "Contains the Dark Dragon deploy algorithm", "Bert")]

namespace DarkDragonDeploy
{
    [AttackAlgorithm("DarkDragonDeploy", "Deploys Dragons and use Zap Quake To Maximize chance of Getting Dark Elixir Storage.")]
    class DarkDragonDeploy : BaseAttack
    {

        public DarkDragonDeploy(Opponent opponent) : base(opponent) { }

        private Target _darkElixirStorage;
        private PointFT[] _deFunnelPoints;
        private PointFT[] _balloonFunnelPoints;
        private AirDefense[] _airDefenses;
        private TownHall _townHall;

        public override string ToString()
        {
            return "Dark Dragon Deploy";
        }

        public override double ShouldAccept()
        {
            if (!PassesBasicAcceptRequirements())
                return 0;

            //TODO - Check which kind of army we have trained. Calculate an Air Offense Score, and Ground Offense Score.

            //TODO - Find all Base Defenses, and calculate an AIR and Ground Defensive Score.  

            //TODO - From Collector/Storage fill levels, determine if loot is in Collectors, or Storages... (Will help to decide which alg to use.)

            //Verify that the Attacking Army contains at least 6 Dragons.
            var allTroops = Deploy.GetTroops();
            var dragons = allTroops.ExtractOne(u => u.PrettyName.ToLower().Contains("dragon"));
            if (dragons == null || dragons?.Count < 6) {
                Log.Warning($"[Dark Dragon] Army not correct! - Dark Dragon Deploy Requires at least 6 Dragons to function Properly. (You have {dragons?.Count ?? 0} dragons)");
                return 0;
            }

            if (allTroops.Count >= 11) {
                //Possibly Too Many Deployment Elements!  Bot Doesnt Scroll - Change Army Composition to have less than 12 unit types!
                Log.Warning($"[Dark Dragon] Warning! Full Army! - The Bot does not scroll through choices when deploying units... If your army has more than 11 unit types, The bot will not see them all, and cannot deploy everything!)");
            }


#if DEBUG
            //Write out all the unit pretty names we found...
            foreach (var item in allTroops)
            {
                Log.Info($"[Deploy Elements] ElementType:{item.ElementType.ToString()} PrettyName:{item.PrettyName} Count:{item.Count} Level:{item.UnitData?.Level}");
            }
#endif

            Log.Info("[Dark Dragon] Base meets minimum Requirements... Checking DE Storage/Air Defense Locations...");

            //Grab the Locations of the DE Storage
            _darkElixirStorage = HumanLikeAlgorithms.TargetDarkElixirStorage();

            if (!_darkElixirStorage.ValidTarget)
            {
                Log.Warning("[Dark Dragon] No Dark Elixir Storage Found - Skipping");
                return 0;
            }

            //Get the locaiton of all Air Defenses
            _airDefenses = AirDefense.Find(CacheBehavior.Default);

#if DEBUG
            //Make sure ALL Air Defenses are found.
            if (!AllAirDefensesFound(_airDefenses)) {
                return 0;
            }
#endif
            if (_airDefenses.Length == 0) {
                Log.Warning("[Dark Dragon] Could not find ANY air defenses - Skipping");
                return 0;
            }
            
            Log.Info($"[Dark Dragon] Found {_airDefenses.Length} Air Defense Buildings.. Continuing Attack..");

            //Now that we found all Air Defenses, order them in the array with closest AD to Target first.
            Array.Sort(_airDefenses, delegate (AirDefense ad1, AirDefense ad2)
            {
                return HumanLikeAlgorithms.DistanceFromPoint(ad1, _darkElixirStorage.DeployGrunts)
                .CompareTo(HumanLikeAlgorithms.DistanceFromPoint(ad2, _darkElixirStorage.DeployGrunts));
            });
            
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

        private bool PassesBasicAcceptRequirements()
        {

            // check if the base meets ALL the user's requirements (One at a time, and log a warning for WHY its skipping)
            if (!Opponent.MeetsRequirements(BaseRequirements.Elixir))
            {
                Log.Warning("[Dark Dragon] Elixir Requirements not Met - Skipping");
                return false;
            }

            if (!Opponent.MeetsRequirements(BaseRequirements.Gold))
            {
                Log.Warning("[Dark Dragon] Gold Requirements not Met - Skipping");
                return false;
            }

            if (!Opponent.MeetsRequirements(BaseRequirements.DarkElixir))
            {
                Log.Warning("[Dark Dragon] Dark Elixir Requirements not Met - Skipping");
                return false;
            }

            if (!Opponent.MeetsRequirements(BaseRequirements.MaxThLevel))
            {
                Log.Warning("[Dark Dragon] Base Over Town Hall Max - Skipping");
                return false;
            }

            if (!Opponent.MeetsRequirements(BaseRequirements.AvoidStrongBases))
            {
                Log.Warning("[Dark Dragon] Strong Base Detected - Skipping");
                return false;
            }

            //Everything meets requirements...
            return true;
        }

        private bool AllAirDefensesFound(AirDefense[] _airDefenses)
        {
            //Determine what the max number of air defenses are based on the TH level.
            _townHall = TownHall.Find();

            if (_townHall == null)
            {
                Log.Warning("[Dark Dragon] Town Hall Could not be found! - Skipping");
                return false;
            }

            int maxAirDefense = 0;

            if (_townHall.Level < 4)
            {
                maxAirDefense = 0;
            }
            else if (_townHall.Level <= 5)
            {
                maxAirDefense = 1;
            }
            else if (_townHall.Level <= 6)
            {
                maxAirDefense = 2;
            }
            else if (_townHall.Level <= 8)
            {
                maxAirDefense = 3;
            }
            else if (_townHall.Level >= 9)
            {
                maxAirDefense = 4;
            }

            if (_airDefenses.Length != maxAirDefense)
            {
                Log.Warning($"[Dark Dragon] Could not find ALL air Defenses. Skipping Base.");
                return false;
            }

            return true;
        }

        private void CreateDebugImages() {

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

            System.IO.File.WriteAllText($@"C:\RaccoonBot\Debug Screenshots\{debugFileName}_3.txt", sb.ToString());

            Log.Info("[Dark Dragon] Deploy Debug Image Saved!");

        }


        public override IEnumerable<int> AttackRoutine()
        {
            Log.Info($"[Dark Dragon] Deploy start - V.{Assembly.GetExecutingAssembly().GetName().Version.ToString()}");

            //Get all Spells relevant to our Deploy
            var deployElements = Deploy.GetTroops();
            var lightningSpells = deployElements.ExtractOne(u => u.ElementType == DeployElementType.Spell && u.PrettyName.ToLower().Contains("lightning"));
            var rageSpells = deployElements.ExtractOne(u => u.ElementType == DeployElementType.Spell && u.PrettyName.ToLower().Contains("rage"));
            List<DeployElement> earthquakeSpells = deployElements.Where(u => u.ElementType == DeployElementType.Spell && u.PrettyName.ToLower().Contains("quake")).ToList();
            List<DeployElement> skeletonSpells = deployElements.Where(u => u.ElementType == DeployElementType.Spell && u.PrettyName.ToLower().Contains("skeleton")).ToList();
 
            //Get all troops relevant to our Deploy
            var dragons = deployElements.ExtractOne(u => u.PrettyName.ToLower().Contains("dragon"));
            var balloons = deployElements.ExtractOne(u => u.PrettyName.ToLower().Contains("balloon"));
            var hogs = deployElements.ExtractOne(u => u.PrettyName.ToLower().Contains("hog"));
            var lavaHounds = deployElements.ExtractOne(u => u.PrettyName.ToLower().Contains("lava"));
            var minions = deployElements.ExtractOne(u => u.PrettyName.ToLower().Contains("minion"));
            var wallBreakers = deployElements.ExtractOne(u => u.PrettyName.ToLower().Contains("wall"));
            var healers = deployElements.ExtractOne(u => u.PrettyName.ToLower().Contains("healer"));
            var clanCastle = deployElements.ExtractOne(u => u.PrettyName.ToLower().Contains("clan"));

            //TODO Add Support for Other Types of units?

            //Get any available heros
            List<DeployElement> king = deployElements.Where(x => x.IsHero && x.Name.ToLower().Contains("king")).ToList();
            List<DeployElement> queen = deployElements.Where(x => x.IsHero && x.Name.ToLower().Contains("queen")).ToList();
            List<DeployElement> warden = deployElements.Where(x => x.IsHero && x.Name.ToLower().Contains("warden")).ToList();
            List<DeployElement> allHeroes = new List<DeployElement>();
            allHeroes.AddRange(king);
            allHeroes.AddRange(queen);
            allHeroes.AddRange(warden);

            var lightningCount = lightningSpells?.Count ?? 0;
            var zapped1 = false;
            var zapped2 = false;
            var earthquakeCount = 0;

            //Get a count of all earthquake spells... donated, or brewed...
            foreach (var spell in earthquakeSpells.Where(s => s.Count > 0))
            {
                earthquakeCount += spell.Count;
            }

            if (lightningCount < 3 || (lightningCount < 2 && earthquakeCount < 1))
            {
                //We dont have the Spells to take out the Closest Air Defense... Surrender before we drop any Dragons!
                Log.Info($"[Dark Dragon] We dont have the Spells to take out the Closest Air Defense... Surrender");
                Attack.Surrender();
            }
            else
            {
                if (earthquakeCount < 1 && lightningCount >= 3 && _airDefenses.Count() >= 1)
                {
                    zapped1 = true;
                    Log.Info($"[Dark Dragon] Dropping 3 Lightning Spells to take out closest Air Defense...");
                    //Drop 3 Lightning on the closest Air Defense.
                    foreach (var t in Deploy.AtPoint(lightningSpells, _airDefenses.ElementAt(0).Location.GetCenter(), 3, Rand.Int(10, 40), Rand.Int(200, 250)))
                        yield return t;
                }

                if (earthquakeCount >= 1 && lightningCount >= 2 && _airDefenses.Count() >= 1)
                {
                    zapped1 = true;
                    Log.Info($"[Dark Dragon] Dropping 2 Lightning Spells & 1 Earthquake to take out closest Air Defense...");
                    //Drop 2 Lightning on the closest Air Defense.
                    var beforeDrop = lightningSpells.Count;
                    foreach (var t in Deploy.AtPoint(lightningSpells, _airDefenses.ElementAt(0).Location.GetCenter(), 1, Rand.Int(10, 40), Rand.Int(200, 250)))
                        yield return t;

                    lightningSpells.Recount();
                    yield return Rand.Int(500, 1000);

                    //Only Deploy a 2nd lightning spell if we successfully deployed 1... Seems to be sticking and deploying Two on the first Click. - not sure why, but this fixes it.
                    if (beforeDrop - 1 == lightningSpells.Count)
                    {
                        foreach (var t in Deploy.AtPoint(lightningSpells, _airDefenses.ElementAt(0).Location.GetCenter(), 1, Rand.Int(10, 40), Rand.Int(200, 250)))
                            yield return t;
                    }
                    else
                    {
                        Log.Error($"[Dark Dragon] First Drop of Lightning actually dropped {beforeDrop - lightningSpells.Count} Lightning Spells. Attempting to Recover & Continue.");
                    }

                    yield return Rand.Int(500, 1000); // pause a little...

                    //Drop 1 Earthquake on the closest Air Defense.
                    foreach (var spell in earthquakeSpells.Where(s => s.Count > 0))
                    {
                        foreach (var t in Deploy.AtPoint(spell, _airDefenses.ElementAt(0).Location.GetCenter(), 1, Rand.Int(10, 40), Rand.Int(200, 250)))
                            yield return t;

                        break; //Only deploy one.
                    }
                }

                if (earthquakeCount >= 2 && lightningCount >= 4 && _airDefenses.Count() >= 2)
                {
                    zapped2 = true;
                    Log.Info($"[Dark Dragon] Dropping 2 Lightning Spells & 1 Earthquake to take out 2nd closest Air Defense...");
                    //Drop 2 Lightning on the 2nd closest Air Defense.
                    foreach (var t in Deploy.AtPoint(lightningSpells, _airDefenses.ElementAt(1).Location.GetCenter(), 1, Rand.Int(10, 40), Rand.Int(200, 250)))
                        yield return t;

                    yield return Rand.Int(500, 1000);

                    foreach (var t in Deploy.AtPoint(lightningSpells, _airDefenses.ElementAt(1).Location.GetCenter(), 1, Rand.Int(10, 40), Rand.Int(200, 250)))
                        yield return t;

                    yield return Rand.Int(500, 1000); // pause a little...

                    //Drop 1 Earthquake on the 2nd closest Air Defense.
                    foreach (var spell in earthquakeSpells.Where(s => s.Count > 0))
                    {
                        foreach (var t in Deploy.AtPoint(spell, _airDefenses.ElementAt(1).Location.GetCenter(), 1, Rand.Int(10, 40), Rand.Int(200, 250)))
                            yield return t;

                        break; //Only deploy one.
                    }
                }

                yield return Rand.Int(1000, 2000); // pause a little

                if (dragons?.Count > 2)
                {
                    Log.Info($"[Dark Dragon] Deploying two Dragons to Create a funnel to direct main force at Dark Elixer Storage...");
                    //Deploy two dragons - one at each funel point.
                    foreach (var t in Deploy.AtPoint(dragons, _deFunnelPoints[0], 1, Rand.Int(10, 40), Rand.Int(200, 250)))
                        yield return t;

                    yield return Rand.Int(500, 1500);

                    foreach (var t in Deploy.AtPoint(dragons, _deFunnelPoints[1], 1, Rand.Int(10, 40), Rand.Int(200, 250)))
                        yield return t;

                    yield return Rand.Int(1000, 1500); // pause for a little while... - Long enought for dragons to begin to create the funnel.
                }

                if (dragons?.Count > 0)
                {
                    //Deploy our main force of dragons all on one spot...
                    Log.Info($"[Dark Dragon] Deploying Main Force of Dragons...");
                    foreach (var t in Deploy.AtPoint(dragons, _darkElixirStorage.DeployGrunts, dragons.Count, Rand.Int(10, 40), Rand.Int(200, 250)))
                        yield return t;
                }

                if (dragons?.Count > 0)
                {
                    Log.Error($"[Dark Dragon] Main Force of Dragons Not Fully Deployed! Trying to drop them on the Edge of the map...");
                    //Find the edge, by adding an arbitrary large distance to the point, and the function will return a safe point always on the map.
                    var mapEdge = HumanLikeAlgorithms.Origin.PointOnLineAwayFromEnd(_darkElixirStorage.DeployGrunts, 30);

                    foreach (var t in Deploy.AtPoint(dragons, mapEdge, dragons.Count, Rand.Int(10, 40), Rand.Int(200, 250)))
                        yield return t;
                }

                //Deploy Lava Hounds - TODO Make the Drop position better for these guys...
                if (lavaHounds?.Count > 0)
                {
                    Log.Info($"[Dark Dragon] Deploying Lava Hounds...");
                    foreach (var t in Deploy.AtPoint(lavaHounds, _darkElixirStorage.DeployGrunts, lavaHounds.Count, Rand.Int(10, 40), Rand.Int(200, 250)))
                        yield return t;
                }


                yield return Rand.Int(5000, 7000); // pause for a little while... - Long enough for main dragons to begin to enter the base.

                //Add all defense seeeking units to the same list.
                List<DeployElement> defenseSeakers = new List<DeployElement>();
                if (balloons?.Count > 0) {
                    defenseSeakers.Add(balloons);
                }
                if (hogs?.Count > 0)
                {
                    defenseSeakers.Add(hogs);
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
                            Log.Info($"[Dark Dragon] Deploying First Third of {deploymentElement.PrettyName}({firstThirdUnitCount}) on 1st Funnel Point...");
                            foreach (var t in Deploy.AtPoint(deploymentElement, _balloonFunnelPoints[0], firstThirdUnitCount, Rand.Int(20, 40), Rand.Int(200, 250)))
                                yield return t;
                        }

                        var secondThirdUnitCount = int.Parse(Math.Floor((decimal)unitCount / 2).ToString());
                        if (secondThirdUnitCount > 0)
                        {
                            Log.Info($"[Dark Dragon] Deploying Second Third of {deploymentElement.PrettyName}({secondThirdUnitCount}) on Main Deploy Point...");
                            foreach (var t in Deploy.AtPoint(deploymentElement, _darkElixirStorage.DeployGrunts, secondThirdUnitCount, Rand.Int(20, 40), Rand.Int(200, 250)))
                                yield return t;
                        }

                        //Drop the Remainder.
                        var remainder = deploymentElement.Count; //Whats left
                        if (remainder > 0)
                        {
                            Log.Info($"[Dark Dragon] Deploying Remainder of {deploymentElement.PrettyName}({remainder}) on 2nd Funnel Point...");
                            foreach (var t in Deploy.AtPoint(deploymentElement, _balloonFunnelPoints[1], remainder, Rand.Int(20, 40), Rand.Int(200, 250)))
                                yield return t;
                        }

                    }
                }

                bool watchHeroes = false;

                yield return Rand.Int(7000, 9000); // pause a while... - Then drop the Heros. - They should start going through walls towards the center.
                
                //Drop king - (He tanks for wallbreakers a little)
                if (UserSettings.UseKing && king.Any())
                {
                    //Deploy the king
                    Log.Info($"[Dark Dragon] Deploying King at: X:{_darkElixirStorage.DeployGrunts.X} Y:{_darkElixirStorage.DeployGrunts.Y}");
                    foreach (var t in Deploy.AtPoint(king[0], _darkElixirStorage.DeployGrunts))
                        yield return t;

                    watchHeroes = true;
                }

                //Drop All Wallbreakers (Get the king going inside the base)
                if (wallBreakers?.Count > 0)
                {
                    yield return Rand.Int(1000, 1200); //Wait for king to be targeted...

                    Log.Info($"[Dark Dragon] Deploying {wallBreakers.Count} {wallBreakers.PrettyName}...");
                    foreach (var t in Deploy.AtPoint(wallBreakers, _darkElixirStorage.DeployGrunts, wallBreakers.Count, Rand.Int(10, 40), Rand.Int(200, 250)))
                        yield return t;
                }

                //Next Drop the Warden, (if we have one)
                if (UserSettings.UseWarden && warden.Any())
                {
                    Log.Info($"[Dark Dragon] Deploying Warden at: X:{_darkElixirStorage.DeployRanged.X} Y:{_darkElixirStorage.DeployRanged.Y}");
                    foreach (var t in Deploy.AtPoint(warden[0], _darkElixirStorage.DeployRanged))
                        yield return t;
                    yield return Rand.Int(500, 1000); //Wait

                    watchHeroes = true;
                }

                yield return Rand.Int(2000, 4000); // pause a while... - Then drop the Queen, so she starts following the King into the base.

                //Drop the queen so she will follow the king in.
                if (UserSettings.UseQueen && queen.Any())
                {
                    Log.Info($"[Dark Dragon] Deploying Queen at: X:{_darkElixirStorage.DeployGrunts.X} Y:{_darkElixirStorage.DeployGrunts.Y}");
                    foreach (var t in Deploy.AtPoint(queen[0], _darkElixirStorage.DeployGrunts))
                        yield return t;
                    yield return Rand.Int(500, 1000); //Wait

                    watchHeroes = true;
                }

                if (watchHeroes)
                {
                    //Watch Heros and Hit ability when they get low.
                    Log.Info($"[Dark Dragon] Watching Heros to activate Abilities...");
                    Deploy.WatchHeroes(allHeroes);
                }

                //Deploy Baby Drags?
                //TODO Deploy on the Back End - on Air D's 3 & 4'

                //Deploy the Clan Castle if user settings say to.
                if(clanCastle?.Count > 0 && UserSettings.UseClanTroops)
                {
                    Log.Info($"[Dark Dragon] Deploying Clan Castle Behind Heros...");
                    foreach (var t in Deploy.AtPoint(clanCastle, _darkElixirStorage.DeployGrunts, clanCastle.Count, Rand.Int(10, 40), Rand.Int(200, 250)))
                        yield return t;
                }

                //If there is a Rage Spell, Deploy it now - Right in front of the DE Storage!
                if (rageSpells?.Count > 0) {
                    //Point on line between Center of DE Storage, and The Deploy Point of the Dragons... Such that the spell edge is near the DE Storage.
                    var rageDropPoint = _darkElixirStorage.Center.PointOnLineAwayFromStart(_darkElixirStorage.DeployGrunts, 6f);
                    Log.Info($"[Dark Dragon] Deploying ONE Rage Spell Close to DE Storage....");
                    foreach (var t in Deploy.AtPoint(rageSpells, rageDropPoint, 1, Rand.Int(10, 40), Rand.Int(200, 250)))
                        yield return t;
                }

                //Drop healers on the Heros if healers exist.
                if (healers?.Count > 0)
                {
                    Log.Info($"[Dark Dragon] Deploying Healers near Heros...");
                    foreach (var t in Deploy.AtPoint(healers, _darkElixirStorage.DeployGrunts, healers.Count, Rand.Int(10, 40), Rand.Int(200, 250)))
                        yield return t;
                }

                //Deploy Minions & Others?
                List<DeployElement> otherUnits = new List<DeployElement>();
                if (minions?.Count > 0)
                {
                    otherUnits.Add(minions);
                }

                yield return Rand.Int(4000, 6000); // pause for a little while longer...

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
                            Log.Info($"[Dark Dragon] Deploying First Quarter of {deploymentElement.PrettyName}({firstQuarterCount}) on 1st Funnel Point...");
                            foreach (var t in Deploy.AtPoint(deploymentElement, _deFunnelPoints[0], firstQuarterCount, Rand.Int(20, 40), Rand.Int(200, 250)))
                                yield return t;
                        }

                        if (firstQuarterCount > 0) //Second quarter should always be same as first quarter... no need to recalc...
                        {
                            Log.Info($"[Dark Dragon] Deploying Second Quarter of {deploymentElement.PrettyName}({firstQuarterCount}) on 2nd Funnel Point...");
                            foreach (var t in Deploy.AtPoint(deploymentElement, _balloonFunnelPoints[0], firstQuarterCount, Rand.Int(20, 40), Rand.Int(200, 250)))
                                yield return t;
                        }

                        //Drop the third Quarter. - recalc to be safe.
                        var thirdQuarterCount = int.Parse(Math.Floor((decimal)deploymentElement.Count / 2).ToString());
                        if (thirdQuarterCount > 0)
                        {
                            Log.Info($"[Dark Dragon] Deploying Third Quarter of {deploymentElement.PrettyName}({thirdQuarterCount}) on 3st Funnel Point...");
                            foreach (var t in Deploy.AtPoint(deploymentElement, _balloonFunnelPoints[1], thirdQuarterCount, Rand.Int(20, 40), Rand.Int(200, 250)))
                                yield return t;
                        }

                        //Drop the Remainder.
                        var remainder = deploymentElement.Count; //Whats left after Dropping on the First AD.
                        if (remainder > 0)
                        {
                            Log.Info($"[Dark Dragon] Deploying Remainder of {deploymentElement.PrettyName}({remainder}) on Last Funnel Point...");
                            foreach (var t in Deploy.AtPoint(deploymentElement, _deFunnelPoints[1], remainder, Rand.Int(20, 40), Rand.Int(200, 250)))
                                yield return t;
                        }

                    }
                }

                //TODO If there is a Heal Spell... Deploy it here... (This one will be harder to predict where to drop...) Meh, skipping for now.

                //Use ANY other Spells at this point... (So they are ALL GONE!)
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
                    if (zapped1)
                        adToDistract = 1;
                    if(zapped2)
                        adToDistract = 2;

                    PointFT dropPoint = new PointFT();
                    string locationDesc = string.Empty;

                    if (_airDefenses.Length < adToDistract + 1) {
                        //Only two Air Defenses were found? No Third to use spells on... drop them on A DE Collector.

                        //Put them on any Elixir Collector still up.
                        var deDrill = DarkElixirDrill.Find(CacheBehavior.ForceScan);

                        if (deDrill.Any())
                        {
                            dropPoint = deDrill[0].Location.GetCenter();
                            locationDesc = "Dark Elixir Drill";
                        }
                        else {
                            //Give up and just drop them in the middle of the map to get rid of them.
                            dropPoint = HumanLikeAlgorithms.Origin;
                            locationDesc = "Center of map - (no other air Defenses or DE Drills Could be found)";
                        }
                    }
                    else {
                        //There were aditional air defenses found... drop them on the next one.
                        dropPoint = _airDefenses[adToDistract].Location.GetCenter();
                        locationDesc = $"Air Defense #{adToDistract + 1}";
                    }

                    foreach (var spell in leftoverSpells)
                    {
                        Log.Info($"[Dark Dragon] Deploying {spell.Count} left over {spell.PrettyName} Spell(s) to {locationDesc}...");
                        foreach (var t in Deploy.AtPoint(spell, dropPoint, spell.Count, Rand.Int(10, 40), Rand.Int(200, 250)))
                            yield return t;
                    }
                }

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
                        Log.Error($"[Dark Dragon] Deploying {troop.Count} left over {troop.PrettyName}s to edge of map, to get rid of troops.  This should not happen, but does sometimes when troops are not properly deployed in earlier phases of the algorithm.");
                        foreach (var t in Deploy.AtPoint(troop, dumpSpot, troop.Count, Rand.Int(10, 40), Rand.Int(200, 250)))
                            yield return t;
                    }
                }

                //At this point the attack is fully deployed... just waiting for the timer to run out, or base to be 100% destroyed.

            }
        }
    }
}

