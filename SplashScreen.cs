using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using StemPC;

namespace STEMPM
{
    public partial class SplashScreen : Form
    {
        public SplashScreen()
        {
            InitializeComponent();
            labelVersion.Text = "Version: " + Form1.Software_Version;

#if PULSANTIERE
            label1.Text = "Button Panel \r\n Manager";
#elif TOPLIFT
            label1.Text = "Top Lift A2 \r\n Manager";
#elif EDEN
            label1.Text = "Eden XP \r\n Manager";
#elif EGICON
            label1.Text = "Spark \r\n Manager";
#else

#endif
        }
    }
}
