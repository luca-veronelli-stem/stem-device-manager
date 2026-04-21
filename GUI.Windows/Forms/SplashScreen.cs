using Core.Interfaces;
using Core.Models;
using StemPC;

namespace STEMPM
{
    public partial class SplashScreen : Form
    {
        public SplashScreen(IDeviceVariantConfig variantConfig)
        {
            InitializeComponent();
            labelVersion.Text = "Version: " + Form1.Software_Version;

            // Label dedicato per variante device-specifiche (parità legacy #if TOPLIFT/EDEN/EGICON).
            // Generic: lascia il testo di default del Designer.
            if (variantConfig.Variant != DeviceVariant.Generic)
            {
                label1.Text = variantConfig.Variant switch
                {
                    DeviceVariant.TopLift => "Top Lift A2 \r\n Manager",
                    DeviceVariant.Eden    => "Eden XP \r\n Manager",
                    DeviceVariant.Egicon  => "Spark \r\n Manager",
                    _                     => label1.Text
                };
            }
        }
    }
}
