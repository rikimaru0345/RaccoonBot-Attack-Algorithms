using CoC_Bot.API;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CustomAlgorithmSettings
{
    //TODO - implemented this as a singleton, so one instance could always be accessable from the Settings Form,
    // or From the Algorithm at Runtime.
    // This would probably change if integrated into the real bot API.


    /// <summary>
    /// Singleton Controller Class that Manages the Instances of all Custom Algorithm Settings.
    /// </summary>
    public sealed class SettingsController
    {
        #region ********** Singleton Stuff **********
        private static readonly SettingsController instance = new SettingsController();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static SettingsController()
        {
        }

        private SettingsController()
        {
        }

        /// <summary>
        /// Singleton Instance of the SettingsController
        /// </summary>
        public static SettingsController Instance
        {
            get
            {
                return instance;
            }
        }
        #endregion

        #region ********** Public Properties of Singleton **********

        //Array of Custom Settings - one for each Algorithm.
        public ConcurrentDictionary<string, AlgorithmSettings> AllAlgorithmSettings = new ConcurrentDictionary<string, AlgorithmSettings>();

        #endregion

        #region ********** Private Properties **********
        //TODO - can probably refactor this away if this gets added to framework...
        //Contains a record of which Algorithms have been Initialized, and which ones have had their window's displayed... 
        private ConcurrentDictionary<string, bool> AlgorithmConfigStatus = new ConcurrentDictionary<string, bool>();

        #endregion

        #region ********** Public Methods of Singleton **********

        /// <summary>
        /// Show the settings window for the specified algorithm, so settings can be modified. This only can happen once currently.
        /// </summary>
        /// <param name="algorithmName">Name of the algorithm to show the settings window for.</param>
        public void ShowSettingsWindow(string algorithmName)
        {
            try
            {
                var keyExists = AlgorithmConfigStatus.ContainsKey(algorithmName + "_window");

                if (!keyExists)
                {
                    //Show the Settings Form.
                    var settingsForm = new SettingsForm();
                    settingsForm.ShowSettingsForm(AllAlgorithmSettings[algorithmName]);
                    AlgorithmConfigStatus.TryAdd(algorithmName + "_window", true);
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message + Environment.NewLine +  ex.StackTrace);
            }
        }

        /// <summary>
        /// Called to Configure an Algorithm's Custom Settings Definition.
        /// </summary>
        /// <param name="algorithmSettings">Settings defined by the Algorithm Developer.</param>
        public void DefineCustomAlgorithmSettings(AlgorithmSettings algorithmSettings)
        {
            var algorithmName = algorithmSettings.AlgorithmName;
            try
            {
                var keyExists = AlgorithmConfigStatus.ContainsKey(algorithmName + "_init");

                if (!keyExists)
                {
                    //Called from each Algorithm at startup, to Define the Custom Settings for each algorithm.
                    AllAlgorithmSettings.TryAdd(algorithmName, algorithmSettings);

                    //Try to Load and override any settings the user has specified previously.
                    LoadCustomUserSettings(algorithmName);

                    AlgorithmConfigStatus.TryAdd(algorithmName + "_init", true);
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message + Environment.NewLine + ex.StackTrace);
            }
        }

        /// <summary>
        /// Returns the Current Value for the Custom Setting.
        /// </summary>
        /// <param name="algorithmName">Name of the Algorithm the setting belongs to.</param>
        /// <param name="settingName">Name of the setting to return.</param>
        /// <param name="OpponentIsDead">Determines whether ot not to return the Active/Dead Setting instance.</param>
        /// <returns></returns>
        public int GetSetting(string algorithmName, string settingName, bool OpponentIsDead)
        {
            if (OpponentIsDead)
            {
                //Active
                return AllAlgorithmSettings[algorithmName].GetSetting(settingName, OpponentIsDead);
            }
            else
            {
                //Dead
                return AllAlgorithmSettings[algorithmName].GetSetting(settingName, OpponentIsDead);
            }
        }
        #endregion

        #region ********** Loading Custom Settings **********

        internal void LoadCustomUserSettings(string algorithmName)
        {
            try
            {
                //Load Custom algorithm settings from disk, and override the default values.
                Log.Info($"[Custom Algorithm Settings] Loading {algorithmName} Settings.");

                var settingsPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), $@"Settings\{algorithmName}.json");
                var settingsJson = File.ReadAllText(settingsPath);

                List<Setting> settings = JsonConvert.DeserializeObject<List<Setting>>(settingsJson);

                //Now that we have the Settings, Merge the Values into the current values.
                MergeSavedSettings(settings, algorithmName);
            }
            catch (Exception ex)
            {
                Log.Warning($"[Custom Algorithm Settings] Could not load Custom {algorithmName} Settings - Using Defaults.");
                Log.Debug($"[Custom Algorithm Settings] Specific Error Loading {algorithmName} Settings - {ex.Message}");
            }
        }

        private void MergeSavedSettings(List<Setting> settings, string algorithmName)
        {
            foreach (var setting in settings)
            {
                try
                {
                    switch (setting.InstanceType)
                    {
                        case SettingInstanceType.Active:
                            SetIndividualSetting(AllAlgorithmSettings[algorithmName].ActiveSettings.FirstOrDefault(s => s.Name == setting.Name), algorithmName, setting.Value);
                            break;
                        case SettingInstanceType.Dead:
                            SetIndividualSetting(AllAlgorithmSettings[algorithmName].DeadSettings.FirstOrDefault(s => s.Name == setting.Name), algorithmName, setting.Value);
                            break;
                        case SettingInstanceType.Global:
                            SetIndividualSetting(AllAlgorithmSettings[algorithmName].GlobalSettings.FirstOrDefault(s => s.Name == setting.Name), algorithmName, setting.Value);
                            break;
                    }
                }
                catch (Exception)
                {
                    Log.Warning($"[Custom Algorithm Settings] {algorithmName} - {setting.Name} - No longer used. Ignoring...");
                }
            }
        }

        internal void SetIndividualSetting(AlgorithmSetting setting, string algorithmName, int newValue)
        {
            if (setting.PossibleValues.Count > 0)
            {
                //If there are possible Values set, and the new value is not one of them, do NOT set the value.
                if (!setting.PossibleValues.Any(v => v.Value == newValue))
                {
                    Log.Warning($"[Custom Algorithm Settings] {algorithmName} - {setting.Name} - Stored value of {newValue} is no longer in the list of possible values.  Value not set.");
                    return;
                }
            }

            if (newValue > setting.MaxValue)
            {
                //Setting is invalid, set to Maximum.
                Log.Warning($"[Custom Algorithm Settings] {algorithmName} - {setting.Name} - Stored value of {newValue} is higher than the Maximum.  Defaulting to Maximum of {setting.MaxValue}.");
                setting.Value = setting.MaxValue;
            }
            else if (newValue < setting.MinValue)
            {
                //Setting is invalid, set to Minimum.
                Log.Warning($"[Custom Algorithm Settings] {algorithmName} - {setting.Name} - Stored value of {newValue} is lower than the Minimum.  Defaulting to Minimum of {setting.MinValue}.");
                setting.Value = setting.MinValue;
            }
            else
            {
                //Set the value
                setting.Value = newValue;
            }
        }
        #endregion

        #region ********** Saving Custom Settings **********
        internal void SaveCustomUserSettings(string algorithmName)
        {
            try
            {
                //Save ALL the Custom Algorithm Settings to Disk.
                Log.Info($"[Custom Algorithm Settings] Saving {algorithmName} Settings.");

                //Get the Current Settings.
                var settings = AllAlgorithmSettings[algorithmName].GetValuesToSave();
                string serializedSettings = JsonConvert.SerializeObject(settings, Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter());

                //Determine the Path of the Exe's Settings Folder.
                var SettingsFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), $@"Settings\{algorithmName}.json");

                File.WriteAllText(SettingsFolder, serializedSettings);
            }
            catch (Exception ex)
            {
                Log.Error($"[Custom Algorithm Settings] Error Saving {algorithmName} Settings! - {ex.Message}");
            }
        }
        #endregion
    }
}
