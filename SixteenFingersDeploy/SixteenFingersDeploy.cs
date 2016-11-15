using System;
using System.Collections.Generic;
using System.Linq;
using CoC_Bot;
using CoC_Bot.API;
using System.Drawing;
using CoC_Bot.Modules.Helpers;

[assembly: Addon("SixteenFingersDeploy", "4-fingers deploy simultaneously at all 4 sides", "AngryDog")]
namespace SixteenFingersDeploy
{

    [AttackAlgorithm("Sixteen Fingers Deploy", "Deploys units, with 16 fingers")]
    public class SixteenFingersDeploy : BaseAttack
    {

        public SixteenFingersDeploy(Opponent opponent) : base(opponent)
        {

        }

        public override double ShouldAccept()
        {
            // check if the base meets the user's requirements
            if (!Opponent.MeetsRequirements(BaseRequirements.All))
            {
                Log.Debug($"[SixteenFingersDeploy] Skipping this base because it doesn't meet the requirements.");
                return 0;
            }
            return 0.7;
        }

        public override IEnumerable<int> AttackRoutine()
        {
            Log.Info("[16 Fingers] Attack start");

            // Get all the units available
            Log.Debug("Scanning troops");

            var unitDeployElements = Deploy.GetTroops();

            // remove spells
            unitDeployElements.Extract(DeployElementType.Spell);

            var heroesAndClanCastle = unitDeployElements.Extract(u => u.IsHero || u.Id == DeployId.ClanCastle);

            var unitGroups = new Dictionary<string, DeployElement[]>
            {
                {"tank units", unitDeployElements.Extract(AttackType.Tank).ToArray()},
                {"attack units", unitDeployElements.Extract(AttackType.Damage).ToArray()},
                {"heal units", unitDeployElements.Extract(AttackType.Heal).ToArray()},
                {"wallbreak units", unitDeployElements.Extract(AttackType.Wallbreak).ToArray()},
            };

            PointFT left = new PointFT(PointFT.MinRedZoneX, PointFT.MaxRedZoneY);
            PointFT top = new PointFT(PointFT.MaxRedZoneX, PointFT.MaxRedZoneY);
            PointFT right = new PointFT(PointFT.MaxRedZoneX, PointFT.MinRedZoneY);
            PointFT bottom = new PointFT(PointFT.MinRedZoneX, PointFT.MinRedZoneY);

            Tuple<PointFT, PointFT>[] lines =
            {
                new Tuple<PointFT, PointFT>(left, top),
                new Tuple<PointFT, PointFT>(right, top),
                new Tuple<PointFT, PointFT>(left, bottom),
                new Tuple<PointFT, PointFT>(right, bottom),
            };

            foreach (var unitGroup in unitGroups)
            {
                Logger.Info("[16 Fingers] Deploying " + unitGroup.Key);
                foreach (var y in Deploy.AlongLines(unitGroup.Value, lines, 4))
                    yield return y;
            }

            Logger.Info("[16 Fingers] Deploying heroes");
            var heroPoint = new Container<PointFT> {Item = new PointFT((lines[0].Item1.X + lines[0].Item2.X)/2, (lines[0].Item1.Y + lines[0].Item2.Y)/2)};
            foreach (var delay in Deploy.AtPoint(heroesAndClanCastle.Where(d => d?.Count > 0).ToArray(), heroPoint, 1, 0, (int)(UserSettings.WaveDelay * 1000)))
                yield return delay;

            // Remove clan castle before watching heroes
            heroesAndClanCastle.ExtractOne(x => x.ElementType == DeployElementType.ClanTroops);
            Deploy.WatchHeroes(heroesAndClanCastle);

            Logger.Info("[16 Fingers] Deploy done");
        }

        public override string ToString()
        {
            return "Sixteen Fingers Deploy ©AngryDog";
        }
    }
}
