using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace ResetExplode;

internal static class Program
{
    private static int _activationPending;

    [STAThread]
    public static int Main(string[] args)
    {
        SingleInstance instance;
        try
        {
            instance = new SingleInstance("ResetExplode.Linux", () =>
            {
                Interlocked.Exchange(ref _activationPending, 1);
                Dispatcher.UIThread.Post(ActivateExistingWindow);
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Não foi possível verificar a instância do RESET EXPLODE: {ex.Message}");
            return 1;
        }
        using var lifetime = instance;
        if (!instance.IsPrimary)
        {
            bool notified = instance.RequestActivationAsync().GetAwaiter().GetResult();
            Console.WriteLine(notified ? "Ativação solicitada à janela existente."
                : "O RESET EXPLODE já está aberto, mas não respondeu. Verifique a janela existente e tente novamente.");
            return notified ? 0 : 1;
        }
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    internal static void ActivateExistingWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is not MainWindow window || !window.IsVisible) return;
        if (Interlocked.Exchange(ref _activationPending, 0) == 0) return;
        window.ActivateFromAnotherInstance();
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
}
