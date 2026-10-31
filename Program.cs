using System.Text;

namespace MohaaBspEditor;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);   // для Windows-1251
        Loc.Load();                                                      // saved UI language
        ApplicationConfiguration.Initialize();
        var form = new MainForm();
        if (args.Length > 0 && File.Exists(args[0]))
            form.LoadFile(args[0]);
        Application.Run(form);
    }
}
