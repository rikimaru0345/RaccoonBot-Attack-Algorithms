using System;

namespace CustomAlgorithmSettings
{
    /// <summary>
    /// Holds possible options for a Custom Algortihm Setting.  (Rendered in the settings UI as a Dropdown.)
    /// </summary>
    public class SettingOption
    {
        //Default constructor
        public SettingOption()
        {
        }

        public SettingOption(string key, int value)
        {
            Key = key;
            Value = value;
        }

        public string Key { get; set; }
        public int Value { get; set; }
    }


}
