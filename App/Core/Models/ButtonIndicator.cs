using Core.Enums;

namespace App.Core.Models
{
    public class ButtonIndicator
    {
        public RectangleF Bounds { get; set; }
        public IndicatorState State { get; set; } = IndicatorState.Idle;
    }
}
