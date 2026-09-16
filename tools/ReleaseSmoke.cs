using System;
using System.Reflection;
using System.Windows.Forms;
using SeedSearcherGui;

internal static class ReleaseSmoke
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
            Assembly app = typeof(SeedSearcher).Assembly;
            foreach (string resource in app.GetManifestResourceNames())
                if (resource.IndexOf("alea", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new Exception("Alea runtime is embedded in the release.");
            Application.EnableVisualStyles();
            using (var form = (Form)Activator.CreateInstance(app.GetType("SeedSearcherGui.SeedSearcherGui")))
            {
                form.CreateControl();
                if (form.Text != "Seed Searcher 1.3") throw new Exception("Incorrect window title: " + form.Text);
                Console.WriteLine("WinForms initialization and title passed: " + form.Text);
            }
            var devices = (Array)app.GetType("SeedSearcherGui.SeedSearcherGPU").GetMethod("UseableGPU").Invoke(null, null);
            foreach (object device in devices) Console.WriteLine("CUDA: " + device);
            int backend = args.Length > 0 && args[0] == "--cpu" ? -1 : 0;
            if (backend == 0 && devices.Length == 0) throw new Exception("CUDA device not discovered.");
            var search = new SeedSearcher(SeedSearcher.Mode.Star35);
            search.RegisterLSB(1);
            search.RegisterPokemon1(7,31,14,31,16,17,2,1,6,4,1,0,0,false,false);
            search.RegisterPokemon2(31,31,17,31,29,29,3,1,16,4,1,0,0,false,false);
            search.RegisterPokemon3(26,31,2,31,23,9,2,0,21,4,2,0,0,false,false);
            search.RegisterPokemon4(21,6,31,20,31,9,2,0,18,2,3,0,0,false,false);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            search.Calculate(backend, 0, 0, new[] {7,14,16,17,29,29}, null, null);
            if (search.Result.Count != 1 || search.Result[0] != 0x1fa0517d9f60fc44UL) throw new Exception("Seed mismatch.");
            Console.WriteLine("Packaged search passed: " + search.Result[0].ToString("X16") + "; total ms: " + watch.ElapsedMilliseconds);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
