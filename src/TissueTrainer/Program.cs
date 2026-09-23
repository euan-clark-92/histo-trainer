using Microsoft.UI.Xaml;

namespace TissueTrainer;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeStaticComWrappers();
        Application.Start(_ => new App());
    }
}
