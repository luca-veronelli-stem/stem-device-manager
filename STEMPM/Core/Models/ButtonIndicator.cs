using STEMPM.Core.ButtonPanelEnums;

namespace STEMPM.Core.Models
{
    public class ButtonIndicator
    {
        public RectangleF Bounds { get; set; }
        public IndicatorState State { get; set; } = IndicatorState.Idle;
    }
}
