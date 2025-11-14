using STEMPM.Core.Enums;

namespace STEMPM.Core.Models
{
    internal class ButtonPanel
    {
        public ButtonPanelType Type { get; set; }
        public int ButtonCount { get; set; }
        public bool HasLed { get; set; }
        public bool HasBuzzer { get; set; } = true;

        public static ButtonPanel GetByType(ButtonPanelType type)
        {
            return type switch
            {
                ButtonPanelType.DIS0025205 => new ButtonPanel { Type = type, 
                    ButtonCount = 4, HasLed = false },
                _ => new ButtonPanel { Type = type, 
                    ButtonCount = 8, HasLed = true }
            };
        }
    }
}
