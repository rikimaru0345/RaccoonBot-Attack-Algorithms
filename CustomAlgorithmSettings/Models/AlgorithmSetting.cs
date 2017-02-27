using System.Collections.Generic;

namespace CustomAlgorithmSettings
{
    /// <summary>
    /// Represents an individual Algorithm Custom Setting.
    /// </summary>
    public class AlgorithmSetting
    {
        #region ********** Constructors **********
        /// <summary>
        /// Default Constructor
        /// </summary>
        public AlgorithmSetting()
        {
            PossibleValues = new List<SettingOption>();
            HideInUiWhen = new List<SettingOption>();
            MinValue = 0;
            MaxValue = 1000;
        }

        /// <summary>
        /// Alternate Constructor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="defaultValue"></param>
        /// <param name="typeOfSetting"></param>
        public AlgorithmSetting(string name, string description, int defaultValue, SettingType typeOfSetting)
        {
            Name = name;
            Description = description;
            Value = defaultValue;
            TypeOfSetting = typeOfSetting;
            PossibleValues = new List<SettingOption>();
            HideInUiWhen = new List<SettingOption>();
            MinValue = 0;
            MaxValue = 1000;
        }
        #endregion

        #region ********** Public Properties **********

        /// <summary>
        /// Descriptive Name of the Custom Algorithm Setting.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The Value of the Custom Algorithm Setting.
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// Description of the Custom Algorithm Setting. (Shows at Tooltip to the end user in the Value Editor UI)
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Any Setting that contains Values here will be displayed as a Drop Down in the Editor UI - with these options.
        /// </summary>
        public List<SettingOption> PossibleValues { get; set; }

        /// <summary>
        /// Indicates where the setting is available at runtime.  For Active Only, Dead Only, Active or Dead, or Global (Seperated in the UI.)
        /// </summary>
        public SettingType TypeOfSetting { get; set; }

        /// <summary>
        /// Used when serializing the instance Value and saving settings.  Values are Active, Dead, or Global.
        /// </summary>
        public SettingInstanceType InstanceType { get; set; }

        /// <summary>
        /// Allows the user to specify a Maximum Possible Value for this setting. (UI will enforce this rule. Default Max Value is 1000).
        /// </summary>
        public int MaxValue { get; set; }

        /// <summary>
        /// Allows the user to specify a Minimum Possible Value for this setting. (UI will enforce this rule. Default Min Value is 0).
        /// </summary>
        public int MinValue { get; set; }

        /// <summary>
        /// Evaluated at UI Display time.  Allows you to Hide this Setting on the UI, when Another Custom Setting has a specific Value.
        /// </summary>
        public List<SettingOption> HideInUiWhen { get; set; }

        #endregion

        #region ********** Internal Helper Functions **********
        /// <summary>
        /// Returns the minimum properties used to Serialize the setting to JSON, and save to Settings Folder.
        /// </summary>
        /// <returns>Setting object popuplated with current value of InstanceType, Name, and Value.</returns>
        internal Setting GetSettingsToSerialize()
        {
            return new Setting()
            {
                InstanceType = InstanceType,
                Name = Name,
                Value = Value
            };
        }

        /// <summary>
        /// Provides a Clone of the Algorithm Setting
        /// </summary>
        /// <returns></returns>
        internal AlgorithmSetting Clone()
        {
            var clone = new AlgorithmSetting(Name, Description, Value, TypeOfSetting);
            clone.InstanceType = InstanceType;
            clone.MinValue = MinValue;
            clone.MaxValue = MaxValue;
            clone.HideInUiWhen.AddRange(HideInUiWhen);
            clone.PossibleValues.AddRange(PossibleValues); //Since these are only for the UI, we dont care if they are a reference...
            return clone;
        }

        #endregion
    }
}
