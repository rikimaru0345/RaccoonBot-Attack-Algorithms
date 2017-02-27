using CoC_Bot.API;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomAlgorithmSettings
{

    /// <summary>
    /// Defines All Settings for an Algorithm, and also serves as the holder of instance Values...
    /// </summary>
    public class AlgorithmSettings
    {
        #region ********** Constructor **********
        /// <summary>
        /// Default constructor
        /// </summary>
        public AlgorithmSettings()
        {
            ActiveSettings = new List<AlgorithmSetting>();
            DeadSettings = new List<AlgorithmSetting>();
            GlobalSettings = new List<AlgorithmSetting>();
        }
        #endregion

        #region ********** Internal Properties **********
        internal List<AlgorithmSetting> ActiveSettings { get; set; }

        internal List<AlgorithmSetting> DeadSettings { get; set; }

        internal List<AlgorithmSetting> GlobalSettings { get; set; }

        #endregion

        #region ********** Public Properties **********
        /// <summary>
        /// Name of the Algorithm
        /// </summary>
        public string AlgorithmName { get; set; }

        /// <summary>
        /// URL to a Detailed Description of the algorithm
        /// </summary>
        public string AlgorithmDescriptionURL { get; set; }

        /// <summary>
        /// Returns the (Read Only) current values of ALL Settings (Active, Dead, & Global) - Primaryly Used for Logging.
        /// </summary>
        public List<AlgorithmSetting> AllSettings
        {
            get
            {
                //TODO  - Should produce a Deep Copy of each of these here - so the values cannot be corrupted... instead of the unions...
                return GlobalSettings.Union(ActiveSettings.Union(DeadSettings)).ToList();
            }
        }
        #endregion

        #region ********** Public Helper Functions **********
        /// <summary>
        /// Called to define a Setting for the Algorithm that can be used at runtime.
        /// </summary>
        /// <param name="setting"></param>
        public void DefineSetting(AlgorithmSetting setting) {

            //Active
            if (setting.TypeOfSetting == SettingType.ActiveAndDead || setting.TypeOfSetting == SettingType.ActiveOnly)
            {
                var instanceCopy = setting.Clone();
                instanceCopy.InstanceType = SettingInstanceType.Active;
                ActiveSettings.Add(instanceCopy);
            }

            //Dead
            if (setting.TypeOfSetting == SettingType.ActiveAndDead || setting.TypeOfSetting == SettingType.DeadOnly )
            {
                var instanceCopy = setting.Clone();
                instanceCopy.InstanceType = SettingInstanceType.Dead;
                DeadSettings.Add(instanceCopy);
            }

            //Global
            if (setting.TypeOfSetting == SettingType.Global)
            {
                var instanceCopy = setting.Clone();
                instanceCopy.InstanceType = SettingInstanceType.Global;
                GlobalSettings.Add(instanceCopy);
            }
        }
        #endregion

        #region ********** Internal Helper Functions **********
        /// <summary>
        /// Returns the Value For the Setting, when the type is Active/Dead (If not found, it will automatically Search in Global)
        /// </summary>
        /// <param name="settingName">The Name of the setting to return.</param>
        /// <param name="OpponentIsDead">Specify which setting to return, (Active or Dead)</param>
        /// <returns>Returns the Instance Value of the Proper Setting (Active/Dead or Global)</returns>
        internal int GetSetting(string settingName, bool OpponentIsDead)
        {
            AlgorithmSetting foundSetting = null;
            if (GlobalSettings.Any(v => v.Name == settingName))
            {
                //Setting was not found in Active/Dead - try in Global...
                foundSetting = GlobalSettings.FirstOrDefault(v => v.Name == settingName);
            }
            else
            {
                //Get the Value from the Proper Collection.
                if (OpponentIsDead)
                {
                    foundSetting = DeadSettings.FirstOrDefault(v => v.Name == settingName);
                }
                else
                {
                    foundSetting = ActiveSettings.FirstOrDefault(v => v.Name == settingName);
                }
            }

            if (foundSetting == null)
            {
                Log.Error($"[Custom Algorithm Settings]{settingName} Setting Does not exist! - Developer! - Check Algorithm Configuration!");
                return -1;
            }

            return foundSetting.Value;
        }

        /// <summary>
        /// Used from the Settings UI Only - Sets the instance value of the setting.
        /// </summary>
        /// <param name="settingName">Name of Setting to Set a value for.</param>
        /// <param name="type">Type of Setting to Set (Active/Dead/Global)</param>
        /// <param name="value">The New Instance Value of the Setting</param>
        internal void SetSetting(string settingName, SettingInstanceType type,  int value)
        {
            AlgorithmSetting setting;

            //Select the Proper Collection.
            switch (type)
            {
                case SettingInstanceType.Active:
                    setting = ActiveSettings.First(v => v.Name == settingName);
                    break;
                case SettingInstanceType.Dead:
                    setting = DeadSettings.First(v => v.Name == settingName);
                    break;
                case SettingInstanceType.Global:
                    setting = GlobalSettings.First(v => v.Name == settingName);
                    break;
                default:
                    throw new ArgumentException("Invalid SettingInstanceType Enum", "type");
            }

            //Set the Value in the Proper Collection
            setting.Value = value;
        }

        /// <summary>
        /// Returns a List of Settings in their minimal form for Serializing to JSON.
        /// </summary>
        /// <returns></returns>
        internal List<Setting> GetValuesToSave() {

            var values = new List<Setting>();

            GlobalSettings.ForEach(s => values.Add(s.GetSettingsToSerialize()));
            ActiveSettings.ForEach(s => values.Add(s.GetSettingsToSerialize()));
            DeadSettings.ForEach(s => values.Add(s.GetSettingsToSerialize()));

            return values;
        }
        #endregion
    }
}
