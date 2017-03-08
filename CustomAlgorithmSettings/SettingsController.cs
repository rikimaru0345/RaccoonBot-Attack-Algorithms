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
        //Contains a Reference to which windows are currently being shown...
        private ConcurrentDictionary<string, SettingsForm> AlgorithmWindowStatus = new ConcurrentDictionary<string, SettingsForm>();

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
                var keyExists = AlgorithmWindowStatus.ContainsKey(algorithmName);

                if (!keyExists)
                {
                    //Show the Settings Form.
                    var settingsForm = new SettingsForm();
                    settingsForm.FormClosed += SettingsForm_FormClosed;
                    settingsForm.ShowSettingsForm(AllAlgorithmSettings[algorithmName]);
                    AlgorithmWindowStatus.TryAdd(algorithmName, settingsForm);
                }
                else
                {
                    //Set Focus on the Existing Form.
                    SettingsForm settingsForm;
                    var success = AlgorithmWindowStatus.TryGetValue(algorithmName, out settingsForm);

                    if (success)
                    {
                        if (!settingsForm.Focused)
                        {
                            settingsForm.Focus();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Custom Algorithm Settings]Error Showing Settings Window: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            }
        }

        private void SettingsForm_FormClosed(object sender, System.Windows.Forms.FormClosedEventArgs e)
        {
            SettingsForm senderForm = (SettingsForm)sender;

            SettingsForm settingsForm;
            var success = AlgorithmWindowStatus.TryRemove(senderForm.AlgorithmName, out settingsForm);

            if (success)
            {
                //Dispose of the Form
                settingsForm.Dispose();
                settingsForm = null;
            }
        }

        /// <summary>
        /// Called to Configure an Algorithm's Custom Settings Definition.
        /// </summary>
        /// <param name="algorithmSettings">Settings defined by the Algorithm Developer.</param>
        public void DefineCustomAlgorithmSettings(AlgorithmSettings algorithmSettings)
        {
            var algorithmName = algorithmSettings.AlgorithmName;

            //Called from each Algorithm at startup, to Define the Custom Settings for each algorithm.
            AllAlgorithmSettings.TryAdd(algorithmName, algorithmSettings);

            //Try to Load and override any settings the user has specified previously.
            LoadCustomUserSettings(algorithmName);
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
	            if (!File.Exists(settingsPath))
	            {
		            Log.Info("[Custom Algorithm Settings] No settings exist yet (first start?)");
		            return;
	            }

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
        /// <summary>
        /// Saves the Custom algorithm Settings in the "Settings" Folder.
        /// </summary>
        /// <param name="algorithmName"></param>
        public void SaveAlgorithmSettings(string algorithmName)
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
