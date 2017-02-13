namespace CustomAlgorithmSettings
{
    partial class SettingsForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsForm));
            this.lnkDescription = new System.Windows.Forms.LinkLabel();
            this.lblAlgorithmName = new System.Windows.Forms.Label();
            this.flpGlobal = new System.Windows.Forms.FlowLayoutPanel();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabGlobal = new System.Windows.Forms.TabPage();
            this.tabActive = new System.Windows.Forms.TabPage();
            this.flpActive = new System.Windows.Forms.FlowLayoutPanel();
            this.tabDead = new System.Windows.Forms.TabPage();
            this.flpDead = new System.Windows.Forms.FlowLayoutPanel();
            this.tabControl1.SuspendLayout();
            this.tabGlobal.SuspendLayout();
            this.tabActive.SuspendLayout();
            this.tabDead.SuspendLayout();
            this.SuspendLayout();
            // 
            // lnkDescription
            // 
            this.lnkDescription.AutoSize = true;
            this.lnkDescription.Location = new System.Drawing.Point(355, 9);
            this.lnkDescription.Name = "lnkDescription";
            this.lnkDescription.Size = new System.Drawing.Size(106, 13);
            this.lnkDescription.TabIndex = 0;
            this.lnkDescription.TabStop = true;
            this.lnkDescription.Text = "Algorithm Description";
            this.lnkDescription.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkDescription_LinkClicked);
            // 
            // lblAlgorithmName
            // 
            this.lblAlgorithmName.AutoSize = true;
            this.lblAlgorithmName.Font = new System.Drawing.Font("Verdana", 15F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblAlgorithmName.Location = new System.Drawing.Point(3, -1);
            this.lblAlgorithmName.Name = "lblAlgorithmName";
            this.lblAlgorithmName.Size = new System.Drawing.Size(217, 25);
            this.lblAlgorithmName.TabIndex = 1;
            this.lblAlgorithmName.Text = "[Algorithm Name]";
            // 
            // flpGlobal
            // 
            this.flpGlobal.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.flpGlobal.AutoScroll = true;
            this.flpGlobal.Location = new System.Drawing.Point(3, 3);
            this.flpGlobal.Name = "flpGlobal";
            this.flpGlobal.Size = new System.Drawing.Size(460, 199);
            this.flpGlobal.TabIndex = 2;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabGlobal);
            this.tabControl1.Controls.Add(this.tabActive);
            this.tabControl1.Controls.Add(this.tabDead);
            this.tabControl1.Location = new System.Drawing.Point(-1, 27);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(474, 231);
            this.tabControl1.TabIndex = 3;
            // 
            // tabGlobal
            // 
            this.tabGlobal.Controls.Add(this.flpGlobal);
            this.tabGlobal.Location = new System.Drawing.Point(4, 22);
            this.tabGlobal.Name = "tabGlobal";
            this.tabGlobal.Padding = new System.Windows.Forms.Padding(3);
            this.tabGlobal.Size = new System.Drawing.Size(466, 205);
            this.tabGlobal.TabIndex = 0;
            this.tabGlobal.Text = "Global";
            this.tabGlobal.UseVisualStyleBackColor = true;
            // 
            // tabActive
            // 
            this.tabActive.Controls.Add(this.flpActive);
            this.tabActive.Location = new System.Drawing.Point(4, 22);
            this.tabActive.Name = "tabActive";
            this.tabActive.Padding = new System.Windows.Forms.Padding(3);
            this.tabActive.Size = new System.Drawing.Size(466, 205);
            this.tabActive.TabIndex = 1;
            this.tabActive.Text = "Active";
            this.tabActive.UseVisualStyleBackColor = true;
            // 
            // flpActive
            // 
            this.flpActive.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.flpActive.AutoScroll = true;
            this.flpActive.Location = new System.Drawing.Point(3, 3);
            this.flpActive.Name = "flpActive";
            this.flpActive.Size = new System.Drawing.Size(460, 199);
            this.flpActive.TabIndex = 3;
            // 
            // tabDead
            // 
            this.tabDead.Controls.Add(this.flpDead);
            this.tabDead.Location = new System.Drawing.Point(4, 22);
            this.tabDead.Name = "tabDead";
            this.tabDead.Size = new System.Drawing.Size(466, 205);
            this.tabDead.TabIndex = 2;
            this.tabDead.Text = "Dead";
            this.tabDead.UseVisualStyleBackColor = true;
            // 
            // flpDead
            // 
            this.flpDead.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.flpDead.AutoScroll = true;
            this.flpDead.Location = new System.Drawing.Point(3, 3);
            this.flpDead.Name = "flpDead";
            this.flpDead.Size = new System.Drawing.Size(460, 199);
            this.flpDead.TabIndex = 4;
            // 
            // SettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(473, 259);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.lblAlgorithmName);
            this.Controls.Add(this.lnkDescription);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(489, 298);
            this.MinimumSize = new System.Drawing.Size(489, 298);
            this.Name = "SettingsForm";
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Advanced Settings";
            this.tabControl1.ResumeLayout(false);
            this.tabGlobal.ResumeLayout(false);
            this.tabActive.ResumeLayout(false);
            this.tabDead.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion

        private System.Windows.Forms.LinkLabel lnkDescription;
        private System.Windows.Forms.Label lblAlgorithmName;
        private AlgorithmSettings algorithmSettings;

        public SettingsForm(AlgorithmSettings algorithmSettings)
        {
            this.algorithmSettings = algorithmSettings;
        }

        private System.Windows.Forms.FlowLayoutPanel flpGlobal;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabGlobal;
        private System.Windows.Forms.TabPage tabActive;
        private System.Windows.Forms.FlowLayoutPanel flpActive;
        private System.Windows.Forms.TabPage tabDead;
        private System.Windows.Forms.FlowLayoutPanel flpDead;
    }
}