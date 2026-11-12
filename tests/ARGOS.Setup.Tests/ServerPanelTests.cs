using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public class ServerPanelTests
{
    [Fact]
    public void InstallRequestsWindowsAuthorizationAndPreservesRequestedAction()
    {
        var info = ARGOS.Server.MainWindow.InstallationElevation(@"C:\ARGOS\ARGOS.Server.exe");
        Assert.Equal("runas", info.Verb);
        Assert.True(info.UseShellExecute);
        Assert.Equal(new[] { "--install" }, info.ArgumentList);
        Assert.Equal(@"C:\ARGOS\ARGOS.Server.exe", info.FileName);
    }
    [Fact]
    public void PanelRendersWithoutStartingOrStoppingServices()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new ARGOS.Server.MainWindow();
                window.Measure(new Size(850, 590));
                window.Arrange(new Rect(0, 0, 850, 590));
                window.UpdateLayout();
                var bitmap = new RenderTargetBitmap(850, 590, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                Assert.NotNull(window.FindName("Services"));
                Assert.False(string.IsNullOrWhiteSpace(((TextBlock)window.FindName("Status")).Text));
                window.Close();
                var installer = new ARGOS.Server.InstallWindow();
                installer.Measure(new Size(720, 780));
                installer.Arrange(new Rect(0, 0, 720, 780));
                installer.UpdateLayout();
                Assert.IsType<PasswordBox>(installer.FindName("Database"));
                Assert.IsType<PasswordBox>(installer.FindName("MailPassword"));
                installer.Close();
                var backup = new ARGOS.Server.BackupWindow();
                backup.Measure(new Size(700, 760));
                backup.Arrange(new Rect(0, 0, 700, 760));
                backup.UpdateLayout();
                Assert.IsType<PasswordBox>(backup.FindName("Password"));
                backup.Close();
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        Assert.Null(failure);
    }
    [Fact]
    public void InstallerRejectsInvalidNameAndDoesNotExposeSecretsInDescription()
    {
        var request = new ARGOS.Server.ServerInstallation("Other", "", "", "", "", "cert-secret", "db-secret", "", "mail-secret");
        Assert.Throws<InvalidOperationException>(() => ARGOS.Server.NativeServerInstaller.Validate(request));
        Assert.DoesNotContain("secret", request.ToString());
    }
}
