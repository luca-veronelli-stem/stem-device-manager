using OxyPlot;

namespace GUI.Windows
{
    /// <summary>
    /// Controller personalizzato per lo zoom solo sull'asse X
    /// </summary>
    public class XAxisMouseWheelZoomManipulator : MouseManipulator
    {
        /// <summary>
        /// Il fattore di zoom per ogni incremento della rotella del mouse
        /// </summary>
        public double ZoomFactor { get; set; }

        /// <summary>
        /// Inizializza una nuova istanza della classe XAxisMouseWheelZoomManipulator
        /// </summary>
        /// <param name="plotView">La vista del plot.</param>
        public XAxisMouseWheelZoomManipulator(IPlotView plotView) : base(plotView)
        {
            // Imposta il fattore di zoom predefinito
            this.ZoomFactor = 1.1;
        }

        /// <summary>
        /// Gestisce l'evento della rotella del mouse per zoomare solo sull'asse X
        /// </summary>
        public override void Started(OxyMouseEventArgs e)
        {
            // Questo metodo è richiesto dall'interfaccia ma non è utilizzato in questo caso
            base.Started(e);
        }

        /// <summary>
        /// Gestisce l'evento della rotella del mouse per zoomare solo sull'asse X
        /// </summary>
        public override void Delta(OxyMouseEventArgs e)
        {
            // Questo metodo è richiesto dall'interfaccia ma non è utilizzato in questo caso
            base.Delta(e);
        }

        /// <summary>
        /// Gestisce l'evento della rotella del mouse per zoomare solo sull'asse X
        /// </summary>
        public override void Completed(OxyMouseEventArgs e)
        {
            // Questo metodo è richiesto dall'interfaccia ma non è utilizzato in questo caso
            base.Completed(e);
        }

        /// <summary>
        /// Gestisce l'evento effettivo della rotella del mouse
        /// </summary>
        public void HandleMouseWheel(OxyMouseWheelEventArgs e)
        {
            // Ottiene il punto corrente in coordinate dello schermo
            var position = e.Position;

            // Ottiene il modello attuale
            var model = this.PlotView.ActualModel;
            if (model == null)
            {
                return;
            }

            // Trova l'asse X principale
            var xAxis = model.DefaultXAxis;
            if (xAxis == null)
            {
                return;
            }

            // Converti la posizione del mouse in coordinate di dati
            var dataX = xAxis.InverseTransform(position.X);

            // Determina il fattore di zoom basato sulla direzione del movimento della rotella
            var factor = e.Delta > 0 ? this.ZoomFactor : 1 / this.ZoomFactor;

            // Applica lo zoom solo all'asse X mantenendo lo stesso punto sotto il cursore
            xAxis.ZoomAt(factor, dataX);

            // Aggiorna il plot
            this.PlotView.InvalidatePlot(false);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Estensioni per configurare facilmente il PlotView con lo zoom solo sull'asse X
    /// </summary>
    public static class PlotViewExtensions
    {
        /// <summary>
        /// Configura il PlotView per utilizzare lo zoom solo sull'asse X
        /// </summary>
        /// <param name="plotView">Il PlotView da configurare</param>
        public static void UseXAxisZoomOnly(this IPlotView plotView)
        {
            // In OxyPlot 2.2.0, IPlotView ha un'implementazione che include l'accesso al Controller
            if (plotView == null)
            {
                throw new ArgumentNullException(nameof(plotView));
            }

            var controller = plotView.ActualController;
            if (controller == null)
            {
                throw new InvalidOperationException("Il plotView non ha un controller valido.");
            }

            // Rimuove il gestore dello zoom standard della rotella del mouse
            controller.UnbindMouseWheel();

            // Crea un nuovo manipolatore personalizzato
            var xAxisZoomer = new XAxisMouseWheelZoomManipulator(plotView);

            //// Aggiunge il nuovo manipolatore personalizzato
            //controller.BindMouseWheel(OxyModifierKeys.None, xAxisZoomer.HandleMouseWheel);

            // Opzionalmente, puoi anche legare la rotella con un tasto modificatore
            // controller.BindMouseWheel(OxyModifierKeys.Control, xAxisZoomer.HandleMouseWheel);
        }
    }
}
