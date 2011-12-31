﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace TESVSnip.Forms
{
    public partial class LoadSettings : Form
    {
        public LoadSettings()
        {
            InitializeComponent();
            //rtfWarning.Text

        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void LoadSettings_Load(object sender, EventArgs e)
        {
            this.chkApplyToAllESM.Checked = TESVSnip.Properties.Settings.Default.ApplyFilterToAllESM;
            this.chkDontAskAboutFiltering.Checked = TESVSnip.Properties.Settings.Default.DontAskUserAboutFiltering;
            this.checkBox1.Checked = TESVSnip.Properties.Settings.Default.EnableESMFilter;
            //this.rtfWarning = TESsnip.Properties.Resources.
            using (var s = new System.IO.MemoryStream())
            {
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(TESVSnip.Properties.Resources.LoadWarning);
                s.Write(bytes, 0, bytes.Length);
                s.Position = 0;
                this.rtfWarning.LoadFile(s, RichTextBoxStreamType.RichText);
            }


            // Groups
            var records = TESVSnip.Properties.Settings.Default.FilteredESMRecords != null
                ? TESVSnip.Properties.Settings.Default.FilteredESMRecords.Trim().Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                : new string[0];
            var allGroups = TESVSnip.Properties.Settings.Default.AllESMRecords != null
                ? TESVSnip.Properties.Settings.Default.AllESMRecords.Trim().Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).ToList()
                : new List<string>();
            foreach (var str in records)
            {
                this.listRecordFilter.Items.Add(str, CheckState.Checked);
                allGroups.Remove(str);
            }
            allGroups.Sort();
            foreach (var str in allGroups)
            {
                this.listRecordFilter.Items.Add(str, CheckState.Unchecked);
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            this.listRecordFilter.Enabled = this.checkBox1.Checked;
        }

        void UpdateState()
        {
            this.listRecordFilter.Enabled = this.checkBox1.Checked;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            TESVSnip.Properties.Settings.Default.ApplyFilterToAllESM = this.chkApplyToAllESM.Checked;
            TESVSnip.Properties.Settings.Default.DontAskUserAboutFiltering = this.chkDontAskAboutFiltering.Checked;
            TESVSnip.Properties.Settings.Default.EnableESMFilter = this.checkBox1.Checked;

            TESVSnip.Properties.Settings.Default.FilteredESMRecords = string.Join(";", this.listRecordFilter.CheckedItems.Cast<string>().ToArray());           
            TESVSnip.Properties.Settings.Default.Save();
        }
    }
}