namespace XerifeTv.CMS.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Uma instância só: duas brigariam pela mesma porta do CMS local
        using var mutex = new Mutex(initiallyOwned: true, "XerifeTv.CMS.Desktop", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("O XerifeTV CMS já está aberto.", "XerifeTV CMS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
