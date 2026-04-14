using App.Core.Enums;

namespace App.Core.Models
{
    // Modello che rappresenta il risultato di un test su una pulsantiera
    public class ButtonPanelTestResult
    {
        // Tipo di pulsantiera testata
        public ButtonPanelType PanelType { get; set; }

        // Tipo di test eseguito
        public ButtonPanelTestType TestType { get; set; }

        // Indica se il test è passato o fallito
        public bool Passed { get; set; }

        // Messaggio associato al risultato del test
        public required string Message { get; set; }

        // Indica se il test è stato interrotto
        public bool Interrupted { get; set; } = false;
    }
}
