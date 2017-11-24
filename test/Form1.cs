using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Saver;
using System.Reflection;



namespace test
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        private void Run()
        {
            { 
                DontSave.DisableGlobaly = true;
                SaveAs.DisableGlobaly = true;
                SaveStaticFields.DisableGlobaly = true;
                Settings.FormatedXML = true;
                //var sw = new System.Diagnostics.Stopwatch();
                //sw.Start(); 
                var x = new A();
                var str1 = x.Save("D:\\1.xml");
                //MessageBox.Show("" + sw.ElapsedMilliseconds);
                //  Close();
                var y = A.Load<A>("D:\\1.xml");
                var str2 = y.Save("D:\\2.xml");
                //MessageBox.Show(str1 + "\r\n" + str2);
                if (str1 != str2)
                    textBox2.BackColor = Color.Red;
                else
                    textBox2.BackColor = Color.LightGreen;

                textBox1.Text = System.IO.File.ReadAllText("D:\\1.xml").Replace("  ", "      ");
                textBox2.Text = System.IO.File.ReadAllText("D:\\2.xml").Replace("  ", "      ");
            }
        }


        #region classes
        class B
        {
            int x = 23;
            object z = 34;
            object y = DateTime.Now;
        }
        [SaveStaticFields]
        class A : SaveAble
        {
            internal int y = 89;
            internal int x { get; set; } = 6;
            public List<B> b = new List<B> { new B(), new B(), new B() };
        }
        #endregion

        private void Form1_Shown(object sender, EventArgs e)
        {
            Run();
            textBox1.SelectionLength = 0;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            Close();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            Run();
        }

    }
}
