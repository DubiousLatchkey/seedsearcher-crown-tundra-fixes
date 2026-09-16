using System;
using System.Windows.Forms;
namespace SeedSearcherGui
{
    static class Program
    {
        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new SeedSearcherGui()); }
            finally { GpuDeviceCatalog.Shutdown(); }
        }
    }
}
