using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ARGOS.Setup;

namespace ARGOS.Setup.Tests;

public sealed class WizardTests
{
    [Fact]
    public void CorrectedSenderHasItsOwnCredentialFile()
    {
        Assert.Equal("sistema.argosp@gmail.com",typeof(Welcome).GetField("Sender",BindingFlags.Static|BindingFlags.NonPublic)!.GetRawConstantValue());
        Assert.Equal("sistema.argosp.gmail.dpapi",Path.GetFileName(LocalSecrets.PathName));
    }
    [Fact]
    public void SecretIsEncryptedAndCanBeReplaced()
    {
        var directory = Path.Combine(Path.GetTempPath(), "argos-wizard-test-" + Guid.NewGuid());
        var path = Path.Combine(directory,"mail.dpapi");
        try
        {
            Assert.Null(LocalSecrets.Load(path));
            LocalSecrets.Save("abcdefghijklmnop",path);
            Assert.Equal("abcdefghijklmnop",LocalSecrets.Load(path));
            Assert.DoesNotContain("abcdefghijklmnop",System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)));
            LocalSecrets.Save("ponmlkjihgfedcba",path);
            Assert.Equal("ponmlkjihgfedcba",LocalSecrets.Load(path));
        }
        finally { if(File.Exists(path)) File.Delete(path); if(Directory.Exists(directory)) Directory.Delete(directory); }
    }

    [Fact]
    public void PagesRenderAndNavigationPreservesFieldsWithoutDatabaseOrEmail()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App(); app.InitializeComponent();
                var window = new Welcome(false);
                var content=(FrameworkElement)window.Content;
                window.Content=null;
                var canvas = new Border { Background = new SolidColorBrush(Color.FromRgb(240,246,246)), Child=content, Width=1050, Height=800 };
                var fields = (Dictionary<string,TextBox>)typeof(Welcome).GetField("fields",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
                fields["name"].Text="Persona de prueba";
                var output = Path.Combine(AppContext.BaseDirectory,"visual-check"); Directory.CreateDirectory(output);
                for(var i=0;i<5;i++)
                {
                    typeof(Welcome).GetField("step",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(window,i);
                    typeof(Welcome).GetMethod("ShowStep",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);
                    canvas.Measure(new Size(1050,800)); canvas.Arrange(new Rect(0,0,1050,800)); canvas.UpdateLayout();
                    Assert.True(content.ActualWidth > 500);
                    var bitmap=new RenderTargetBitmap(1050,800,96,96,PixelFormats.Pbgra32); bitmap.Render(canvas);
                    var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file=File.Create(Path.Combine(output,$"step-{i+1}.png")); encoder.Save(file);
                }
                Assert.Equal("Persona de prueba",fields["name"].Text);
                Assert.DoesNotContain("login",fields.Keys);
                var navigate=typeof(Welcome).GetMethod("NavigateAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;
                var stepField=typeof(Welcome).GetField("step",BindingFlags.Instance|BindingFlags.NonPublic)!;
                // Direct backward selection preserves data and does not require database access.
                ((Task)navigate.Invoke(window,[1])!).GetAwaiter().GetResult();
                Assert.Equal(1,stepField.GetValue(window));
                // A forward jump with invalid mail configuration must remain at the failed step.
                ((Task)navigate.Invoke(window,[3])!).GetAwaiter().GetResult();
                Assert.Equal(1,stepField.GetValue(window));
                // Completed enrollment can be reviewed and left again without a second save.
                typeof(Welcome).GetField("registered",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(window,true);
                stepField.SetValue(window,4);
                ((Task)navigate.Invoke(window,[3])!).GetAwaiter().GetResult();
                ((Task)typeof(Welcome).GetMethod("AdvanceAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null)!).GetAwaiter().GetResult();
                Assert.Equal(4,stepField.GetValue(window));
                Assert.Equal("Persona de prueba",fields["name"].Text);
                window.Close(); app.Shutdown();
            }
            catch(Exception exception) { error=exception; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30))); Assert.Null(error);
    }
}
