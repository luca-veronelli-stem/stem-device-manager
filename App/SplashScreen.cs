using StemPC;

namespace STEMPM
{
    public partial class SplashScreen : Form
    {
        public SplashScreen()
        {
            InitializeComponent();
            labelVersion.Text = "Version: " + Form1.Software_Version;

#if TOPLIFT
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
