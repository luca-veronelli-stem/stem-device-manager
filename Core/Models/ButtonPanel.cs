using DocumentFormat.OpenXml.Office2010.Drawing.Charts;
using STEMPM.Core.ButtonPanelEnums;

namespace STEMPM.Core.Models
{
    // Modello che rappresenta una pulsantiera con le sue caratteristiche
    internal class ButtonPanel
    {
        public ButtonPanelType Type { get; set; }
        public int ButtonCount { get; set; }
        public bool HasLed { get; set; }
        // Tutte le pulsantiere hanno il buzzer, questo campo è per estensibilità futura
        public bool HasBuzzer { get; set; } = true;
        public string[] Buttons { get; set; } = [];

        // Metodo factory per creare una pulsantiera in base al tipo
        public static ButtonPanel GetByType(ButtonPanelType type)
        {
            return type switch
            {
                // La pulsantiera di tipo DIS0025205 (Optimus-XP) ha 4 pulsanti senza LED
                ButtonPanelType.DIS0025205 => new ButtonPanel { Type = type, 
                    ButtonCount = 4, HasLed = false, Buttons = GetButtonsByType(type) },
                // Le altre pulsantiere hanno tutte 8 pulsanti con LED
                _ => new ButtonPanel { Type = type, 
                    ButtonCount = 8, HasLed = true, Buttons = GetButtonsByType(type) }
            };
        }

        private static string[] GetButtonsByType(ButtonPanelType type)
        {
            return type switch
            {
                ButtonPanelType.DIS0025205 => Enum.GetNames(typeof(OptimusButtons)),
                ButtonPanelType.DIS0026166 => Enum.GetNames(typeof(R3LXPButtons)),
                _ => Enum.GetNames( typeof(EdenButtons)),
            };
        }
    }
}
