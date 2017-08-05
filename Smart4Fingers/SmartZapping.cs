using System.Collections.Generic;
using System.Linq;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;

namespace SmartFourFingersDeploy
{
    class SmartZapping
    {

        /// <summary>
        /// use lighting on drill in smart way to save elixir usage
        /// </summary>
        /// <param name="deAmount">minimum dark elixir to gain per zap (user defined)</param>
        /// <param name="drillLevel">minimum drill level to zap (user defined)</param>
        /// <param name="spells">available spells in unitsbar</param>
        /// <returns>deploy lighting on drills</returns>
        public static IEnumerable<int> SmartZap(int deAmount, int drillLevel, List<DeployElement> spells)
        {
            Log.Info($"{SmartFourFingersDeploy.AttackName} Smart Zap Drills module");
            bool zapDrill = true;

            var zap = spells.Extract(u => u.Id == DeployId.Lightning);
            var zapCount = zap?.Sum(u => u.Count);

            if (zapCount <= 0)
            {
                Log.Warning($"{SmartFourFingersDeploy.AttackName} Smart Zap Drills No lighting Spells found for Smart Zap");
                zapDrill = false;
            }

            var drills = DarkElixirDrill.Find(CacheBehavior.ForceScan, drillLevel);

            if (drills == null)
            {
                Log.Warning("{SmartFourFingersDeploy.AttackName} Smart Zap Drills didn't found Dark Drills matches the requirements");
                zapDrill = false;
            }

            var opponent = new Opponent(0);
            var availableDE = opponent.GetAvailableLoot(false).DarkElixir;
            var availableDEAfterZap = 0;

            if (availableDE < deAmount)
            {
                Log.Warning($"{SmartFourFingersDeploy.AttackName} Smart Zap Drills this base only has {availableDE} DE .. it doesn't match the requirements ({deAmount})");
                zapDrill = false;
            }

            if (zapDrill)
            {
                Log.Info($"{SmartFourFingersDeploy.AttackName} Smart Zap Drills found {zap.Sum(u => u.Count)} Lighting Spell(s)");
                Log.Info($"{SmartFourFingersDeploy.AttackName} Smart Zap Drills found {drills.Count()} Dark drill(s)");

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

                            yield return 7000;

                            zapCount--;
                            if (zapCount > 0)
                            {
                                availableDEAfterZap = opponent.GetAvailableLoot(false).DarkElixir;
                                if (availableDE - availableDEAfterZap < deAmount)
                                {
                                    Log.Warning($"{SmartFourFingersDeploy.AttackName} Smart Zap Drills only {availableDE - availableDEAfterZap} DE from this drill .. you set it to {deAmount} .. will not zap it again ");
                                    drills[i] = null;
                                }
                                else
                                {
                                    Log.Info($"{SmartFourFingersDeploy.AttackName} Smart Zap Drills gain {availableDE - availableDEAfterZap} DE from this drill");
                                    availableDE = availableDEAfterZap;
                                }
                            }
                        }

                        if (zapCount <= 0)
                            break;
                    }
                    yield return 7000;

                    var standingDrills = DarkElixirDrill.Find(CacheBehavior.ForceScan, drillLevel);
                    if (!standingDrills.Any())
                    {
                        Log.Warning($"{SmartFourFingersDeploy.AttackName} no drills to zap");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Drop EQ spells on drills to get DE beacuse max EQ will gain 320 DE from max drill
        /// </summary>
        /// <param name="drillLevel">minmum drill level</param>
        /// <param name="spells">available spells in unitsbar</param>
        /// <returns>Drop EQ spells on drills</returns>
        public static IEnumerable<int> UseEQOnDrills(int drillLevel, List<DeployElement> spells)
        {
            var EQSpell = spells.Extract(u => u.Id == DeployId.Earthquake);
            var EQCount = EQSpell.Sum(u => u.Count);
            if (EQCount > 0)
            {
                Log.Info($"{SmartFourFingersDeploy.AttackName} start use EQ on drills");
                var drills = DarkElixirDrill.Find(CacheBehavior.ForceScan, drillLevel);
                if (drills.Any())
                {
                    foreach (var d in drills)
                    {
                        var eq = EQSpell.FirstOrDefault()?.Count > 0 ? EQSpell.FirstOrDefault() : EQSpell.LastOrDefault();
                        if (eq.Count > 0)
                        {
                            foreach (var t in Deploy.AtPoint(eq, d.Location.GetCenter()))
                                yield return t;
                        }
                    }
                }
                else
                    Log.Warning($"{SmartFourFingersDeploy.AttackName} no Drills found to use EQ on !!");
            }
            else
                Log.Warning($"{SmartFourFingersDeploy.AttackName} no EarthQuake spells found to use on drills");
        }

        /// <summary>
        /// End battle after user defined secs
        /// </summary>
        /// <param name="endBattleTime">secs to end battle after .. NOTE: 0 = don't end battle</param>
        /// <returns></returns>
        public static IEnumerable<int> EndBattle(int endBattleTime)
        {
            if (endBattleTime == 0)
                yield break;
            
            Log.Info($"end battle after {endBattleTime} sec");

            for (var i = endBattleTime; i > 0; i--)
            {
                Log.Info($"{SmartFourFingersDeploy.AttackName} end battle after {i} sec");
                yield return 1000;
            }
            Attack.Surrender();        
        }
    }
}
