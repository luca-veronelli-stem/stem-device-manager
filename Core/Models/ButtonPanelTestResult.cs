using STEMPM.Core.Enums;

namespace STEMPM.Core.Models
{
    // Modello che rappresenta il risultato di un test su una pulsantiera
    internal class ButtonPanelTestResult
    {
        public ButtonPanelType Type { get; set; }
        public ButtonPanelTestType TestType { get; set; }
        public bool Passed { get; set; }
        public required string Message { get; set; }
    }
}
