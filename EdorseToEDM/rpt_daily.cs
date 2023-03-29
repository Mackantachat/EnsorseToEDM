using DevExpress.XtraReports.UI;
using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;

namespace EdorseToEDM
{
    public partial class rpt_daily : DevExpress.XtraReports.UI.XtraReport
    {
        public rpt_daily(rpt_model[] data,string countfiles)
        {
            InitializeComponent();
            this.objectDataSource1.DataSource = data;
            this.xrLabel1.Text = countfiles;
        }

    }
}
