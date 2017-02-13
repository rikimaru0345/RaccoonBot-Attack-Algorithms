using CoC_Bot.API;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;

namespace CustomAlgorithmSettings
{
    public partial class SettingsForm : Form
    {
        private AlgorithmSettings _settings;

        #region Constructor
        public SettingsForm()
        {
            InitializeComponent();
        }
        #endregion

        #region Public Properties

        public string AlgorithmName { get; set; }

        #endregion

        #region ********** Set up and show the Form **********

        internal void ShowSettingsForm(AlgorithmSettings settings) {

            //Set a reference to the Settings.
            _settings = settings;

            //Set the Name Label, and the Titlebar.
            AlgorithmName = _settings.AlgorithmName;
            lblAlgorithmName.Text = _settings.AlgorithmName;
            this.Text = $"{_settings.AlgorithmName} Settings";

            //Create the Controls on each of the Dynamic Flow Panels.
            BuildSettingsPanel(_settings.ActiveSettings, flpActive, SettingInstanceType.Active);
            BuildSettingsPanel(_settings.DeadSettings, flpDead, SettingInstanceType.Dead);
            BuildSettingsPanel(_settings.GlobalSettings, flpGlobal, SettingInstanceType.Global);

            //Set Visibility for all Settings based on current values.
            SetAllVisibility();

            //Show the form.
            this.Show();
        }

        #endregion

        #region ********** Dynamically Create the Settings Form Controls **********

        private void BuildSettingsPanel(IEnumerable<AlgorithmSetting> settings, FlowLayoutPanel flowPanel, SettingInstanceType type) {
            //Dynamically Build the form Controls.
            var index = 0;
            var tabCounter = 3;

            foreach (var setting in settings)
            {
                var panel = new Panel();
                panel.Size = new Size(210, 40);
                panel.Name = $"panel{index}";
                panel.Tag = setting.Name;

                var newLabel = new Label();
                newLabel.AutoSize = true;
                newLabel.Location = new Point(0, 0);
                newLabel.Name = $"label{index}";
                newLabel.Size = new Size(210, 13);
                newLabel.TabIndex = tabCounter;
                newLabel.Text = $"{setting.Name}:";

                panel.Controls.Add(newLabel);

                tabCounter++;
                if (setting.PossibleValues.Count > 0)
                {
                    //Create Drop Down
                    var newDropDown = new ComboBox();

                    newDropDown.DropDownStyle = ComboBoxStyle.DropDownList;
                    newDropDown.Location = new Point(0, 15);
                    newDropDown.FormattingEnabled = true;
                    newDropDown.Name = $"dropDown{index}";
                    newDropDown.Size = new Size(210, 21);
                    newDropDown.TabIndex = tabCounter;
                    newDropDown.Tag = setting.Name;

                    newDropDown.DataSource = setting.PossibleValues;
                    newDropDown.DisplayMember = "Key";
                    newDropDown.ValueMember = "Value";
                    
                    //Data binding only takes place AFTER control is rendered... this is a workaround to have the initial value set.
                    EventHandler visibleChangedHandler = null;
                    visibleChangedHandler = delegate {
                        newDropDown.SelectedValue = setting.Value;
                        newDropDown.VisibleChanged -= visibleChangedHandler; // Only do this once!
                    };
                    newDropDown.VisibleChanged += visibleChangedHandler;

                    //assign the correct event handler (active or Dead/Global)
                    switch (type)
                    {
                        case SettingInstanceType.Active:
                            newDropDown.SelectedValueChanged += DropDown_SelectedActiveValueChanged;
                            break;
                        case SettingInstanceType.Dead:
                            newDropDown.SelectedValueChanged += DropDown_SelectedDeadValueChanged;
                            break;
                        case SettingInstanceType.Global:
                            newDropDown.SelectedValueChanged += DropDown_SelectedGlobalValueChanged;
                            break;
                    }

                    ToolTip newToolTip = new ToolTip();
                    newToolTip.SetToolTip(newDropDown, setting.Description);

                    panel.Controls.Add(newDropDown);
                }
                else
                {
                    //Create TextBox for adding Int type Values.
                    var newNumericUpDown = new NumericUpDown();

                    newNumericUpDown.Location = new Point(0, 15);
                    newNumericUpDown.Name = $"numericUpDown{index}";
                    newNumericUpDown.Size = new Size(210, 21);
                    newNumericUpDown.TabIndex = tabCounter;
                    newNumericUpDown.Tag = setting.Name;
                    newNumericUpDown.Maximum = setting.MaxValue;
                    newNumericUpDown.Minimum = setting.MinValue;

                    newNumericUpDown.Value = setting.Value;

                    //assign the correct event handler (active or Dead/Global)
                    switch (type)
                    {
                        case SettingInstanceType.Active:
                            newNumericUpDown.ValueChanged += NumericUpDown_ActiveValueChanged;
                            break;
                        case SettingInstanceType.Dead:
                            newNumericUpDown.ValueChanged += NumericUpDown_DeadValueChanged;
                            break;
                        case SettingInstanceType.Global:
                            newNumericUpDown.ValueChanged += NumericUpDown_GlobalValueChanged;
                            break;
                    }

                    ToolTip newToolTip = new ToolTip();
                    newToolTip.SetToolTip(newNumericUpDown, setting.Description);

                    panel.Controls.Add(newNumericUpDown);
                }

                flowPanel.Controls.Add(panel);

                index++;
                tabCounter++;
            }

        }
        #endregion

        #region ********** Visibility Settings **********
        /// <summary>
        /// Sets the Visibility of ALL settings according to current Values, and HideInUiWhen Rules.
        /// </summary>
        private void SetAllVisibility()
        {
            SetViewVisibility(SettingInstanceType.Global);
            SetViewVisibility(SettingInstanceType.Active);
            SetViewVisibility(SettingInstanceType.Dead);
        }

        /// <summary>
        /// Sets the Visibility for Settings within a specific Panel (Global, Active, or Dead)
        /// </summary>
        /// <param name="type">The Type of pannel to set visibility settings for</param>
        private void SetViewVisibility(SettingInstanceType type) {
            switch (type)
            {
                case SettingInstanceType.Active:
                    foreach (var setting in _settings.ActiveSettings.Where(s => s.HideInUiWhen.Count > 0))
                    {
                        SetIndividualVisibility(setting, flpActive, false);
                    }
                    break;
                case SettingInstanceType.Dead:
                    foreach (var setting in _settings.DeadSettings.Where(s => s.HideInUiWhen.Count > 0))
                    {
                        SetIndividualVisibility(setting, flpDead, true);
                    }
                    break;
                case SettingInstanceType.Global:
                    foreach (var setting in _settings.GlobalSettings.Where(s => s.HideInUiWhen.Count > 0))
                    {
                        SetIndividualVisibility(setting, flpGlobal, false);
                    }
                    break;
            }
        }

        /// <summary>
        /// Sets the Visibility for an individual Setting - by evaluating conditions in HideInUiWhen Collection.
        /// </summary>
        private void SetIndividualVisibility(AlgorithmSetting setting, FlowLayoutPanel flowLayoutPanel, bool deadSetting)
        {
            //Get a reference to the Control for this setting, and set it to visible by default.
            var control = GetControl(flowLayoutPanel, setting.Name);
            control.Visible = true;

            foreach (var settingCondition in setting.HideInUiWhen)
            {
                //Get the current Setting, and compare it against the condition.
                if (_settings.GetSetting(settingCondition.Key, deadSetting) == settingCondition.Value)
                {
                    //Conditions are met to Hide this Setting! (once a single condition is met, no need to compare other conditions)
                    control.Visible = false;
                    break;
                }
            }
        }

        /// <summary>
        /// Returns the Panel Control that has the matching Tag.
        /// </summary>
        /// <param name="flowPanel">pannel to search</param>
        /// <param name="settingName">Name of the Setting</param>
        /// <returns>Returns a reference to the Panel Containing this setting.(Label and Edit Control)</returns>
        private Panel GetControl(FlowLayoutPanel flowPanel, string settingName) {
            foreach (var item in flowPanel.Controls)
            {
                if (item.GetType() == typeof(Panel)) {
                    var panel = (Panel)item;
                    if ((string)panel.Tag == settingName) {
                        return panel;
                    }
                }
            }
            return null;
        }
        #endregion

        #region ********** Dynamic Control Event Handlers **********

        //Active Panel Event Handlers.
        private void DropDown_SelectedActiveValueChanged(object sender, EventArgs e)
        {
            var control = (ComboBox)sender;
            _settings.SetSetting((string)control.Tag, SettingInstanceType.Active, (int)control.SelectedValue);
            SetViewVisibility(SettingInstanceType.Active);
        }

        private void NumericUpDown_ActiveValueChanged(object sender, EventArgs e)
        {
            var control = (NumericUpDown)sender;
            _settings.SetSetting((string)control.Tag, SettingInstanceType.Active, (int)control.Value);
            SetViewVisibility(SettingInstanceType.Active);
        }

        //Dead Panel Event Handlers.
        private void DropDown_SelectedDeadValueChanged(object sender, EventArgs e)
        {
            var control = (ComboBox)sender;
            _settings.SetSetting((string)control.Tag, SettingInstanceType.Dead, (int)control.SelectedValue);
            SetViewVisibility(SettingInstanceType.Dead);
        }

        private void NumericUpDown_DeadValueChanged(object sender, EventArgs e)
        {
            var control = (NumericUpDown)sender;
            _settings.SetSetting((string)control.Tag, SettingInstanceType.Dead, (int)control.Value);
            SetViewVisibility(SettingInstanceType.Dead);
        }

        //Global Panel Event Handlers.
        private void DropDown_SelectedGlobalValueChanged(object sender, EventArgs e)
        {
            var control = (ComboBox)sender;
            _settings.SetSetting((string)control.Tag, SettingInstanceType.Global, (int)control.SelectedValue);
            SetViewVisibility(SettingInstanceType.Global);
        }

        private void NumericUpDown_GlobalValueChanged(object sender, EventArgs e)
        {
            var control = (NumericUpDown)sender;
            _settings.SetSetting((string)control.Tag, SettingInstanceType.Global, (int)control.Value);
            SetViewVisibility(SettingInstanceType.Global);
        }
        #endregion

        #region ********** Algorithm Description URL Click Handler **********
        private void lnkDescription_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            //Call the Process.Start method to open the default browser 
            //with a URL:
            System.Diagnostics.Process.Start(_settings.AlgorithmDescriptionURL);
        }
        #endregion
        
    }
}
