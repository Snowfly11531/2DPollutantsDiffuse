using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DamBreakModelApplication
{
    public partial class ProcessWindow : Form
    {
        int second, minutes, hours;
        public ProcessWindow()
        {
            InitializeComponent();
        }
        //设置进度条最大、最小值
        public void setMinAndMaxValue(int min, int max)
        {
            this.progressBar1.Minimum = min;
            this.progressBar1.Maximum = max;
            this.timer1.Enabled = true;
        }

        private void ProcessWindow_Load(object sender, EventArgs e)
        {

            second = 0;
            minutes = 0;
            hours = 0;
        }

        //进度条滚动
        public void setProcess(string text, int num)
        {
            //if (num > progressBar1.Maximum)
            //    return;
            this.label1.Text = text;
            this.progressBar1.Value = num;
            if (num >= this.progressBar1.Maximum)
            { this.Close(); }
            label1.Refresh();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (++second == 60)
            {
                second = 0;
                if (++minutes == 60)
                {
                    minutes = 0;
                    hours++;
                }
            }
            string timeUsed = "已用时间：";
            if (hours != 0)
                timeUsed += string.Format(" {0} 小时", hours);
            if (minutes != 0)
                timeUsed += string.Format(" {0} 分钟", minutes);
            if (second != 0)
                timeUsed += string.Format(" {0} 秒", second);
            label2.Text = timeUsed;
            label2.Refresh();
        }

        public void close()
        {
            this.Close();
        }

    }
}
