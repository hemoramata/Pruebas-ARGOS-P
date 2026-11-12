using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using ARGOS.API;
using ARGOS.Application.Common.Persistence;
using ARGOS.Application.Security;
using ARGOS.Domain.Aggregates.OrganizationSetup;
using ARGOS.Domain.Aggregates.Personnel;
using ARGOS.Infrastructure;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ARGOS.Setup;

public partial class Welcome : Window
{
    private const string Sender = "sistema.argosp@gmail.com";
    private readonly Dictionary<string, TextBox> fields = new();
    private readonly Dictionary<string, PasswordBox> secrets = new();
    private readonly StackPanel[] pages = new StackPanel[5];
    private readonly ComboBox positions = new();
    private readonly PasswordBox code = new();
    private readonly TextBlock mailState = new();
    private readonly Button send = new() { Content = "Confirmar designación y enviar código" };
    private int step;
    private int furthest;
    private bool busy, registered;
    private string connection = "", mailPassword = "";
    private ServiceProvider? services;
    private OrganizationSetup? setup;
    private SecondFactorChallenge? challenge;
    private Guid binding;
    private InstitutionalPerson? person;
    private AdministratorDesignation? designation;
    private AccessAccount? account;
    private EnrollmentStage stage;
    private Process? server;
    private readonly StackPanel adminFields = new();

    public Welcome() : this(true) { }
    internal Welcome(bool loadSecrets)
    {
        InitializeComponent();
        BuildPages();
        try { mailPassword = loadSecrets ? LocalSecrets.Load() ?? "" : ""; }
        catch { Status.Text = "No se pudo recuperar la clave de correo. Puede reemplazarla en el paso 2."; }
        mailState.Text = mailPassword.Length == 16 ? "Clave guardada cifrada en Windows. Deje el campo vacío para conservarla." : "Ingrese la clave una vez; se guardará cifrada en este usuario de Windows.";
        ShowStep();
        Closing += (_, e) => { if (busy) { e.Cancel = true; return; } challenge?.Dispose(); services?.Dispose(); };
    }
    private TextBox Field(Panel panel, string key, string label, string initial = "")
    {
        panel.Children.Add(new TextBlock { Text = label });
        var box = new TextBox { Text = initial, MaxLength = 200 };
        System.Windows.Automation.AutomationProperties.SetName(box, label);
        panel.Children.Add(box); fields.Add(key, box); return box;
    }
    private PasswordBox Secret(Panel panel, string key, string label)
    {
        panel.Children.Add(new TextBlock { Text = label });
        var box = new PasswordBox { MaxLength = 1024 };
        System.Windows.Automation.AutomationProperties.SetName(box, label);
        panel.Children.Add(box); secrets.Add(key, box); return box;
    }
    private static Button ActionButton(Panel panel, string caption, RoutedEventHandler handler)
    {
        var button = new Button { Content = caption }; button.Click += handler; panel.Children.Add(button); return button;
    }
    private void BuildPages()
    {
        for (var i = 0; i < pages.Length; i++) pages[i] = new StackPanel();
        pages[0].Children.Add(new TextBlock { Text = "Servidor: localhost:5432\nBase: ARGOS_PRUEBAS_20260917\nSólo comprobaremos la conexión; no se crean ni eliminan bases.", Margin = new Thickness(0,0,0,20) });
        Field(pages[0], "dbUser", "Usuario de PostgreSQL", "postgres");
        Secret(pages[0], "db", "Contraseña de PostgreSQL (se conserva sólo mientras esté abierto el asistente)");
        pages[1].Children.Add(new TextBlock { Text = "Remitente: " + Sender });
        pages[1].Children.Add(mailState);
        Secret(pages[1], "mail", "Contraseña de APLICACIÓN de Gmail (no la contraseña normal)");
        Field(pages[1], "testRecipient", "Correo real donde recibirá el mensaje de prueba");
        ActionButton(pages[1], "Guardar y enviar correo de prueba", async (_, _) => await Run(async () =>
        {
            SaveMail();
            var destination = Email("testRecipient");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var test = await SecondFactorChallenge.IssueAsync(Guid.NewGuid(), Guid.NewGuid(), "setup-mail-test",
                VerificationChannel.InstitutionalEmail, TimeSpan.FromMinutes(3), 5,
                (otp, ct) => GmailVerificationDelivery.SendToEmailAsync(Sender, mailPassword, destination, otp, ct), cancellationToken: timeout.Token);
            Status.Text = "Gmail aceptó el envío. Revise su buzón y spam. Este código es sólo de prueba; no crea cuentas.";
        }, EnrollmentStage.SendingEmail));
        pages[2].Children.Add(new TextBlock { Text = "Abra la configuración institucional y complete sus seis pasos. Si ya está publicada, la recuperaremos sin repetirla. El navegador mantiene el asistente institucional existente." });
        ActionButton(pages[2], "Abrir configuración institucional", async (_, _) => await Run(async () => { await StartServer(); Open("https://localhost:7193/setup/"); }, EnrollmentStage.Preparation));
        pages[2].Children.Add(new TextBlock { Text = "Después de publicar, vuelva a esta ventana y pulse Continuar. Si ya existe una cuenta, puede ir al inicio de sesión.", Margin = new Thickness(0,20,0,0) });
        ActionButton(pages[2], "Ya tengo cuenta: abrir inicio de sesión", async (_, _) => await Run(async () => { await StartServer(); Open("https://localhost:7193/login/"); }, EnrollmentStage.Preparation));
        pages[3].Children.Add(adminFields);
        adminFields.Children.Add(new TextBlock { Text = "Puesto institucional designado (no superusuario)" });
        adminFields.Children.Add(positions);
        Field(adminFields, "name", "Nombre completo"); Field(adminFields,"issuer","Emisor del documento", "TSE");
        Field(adminFields,"document","Tipo de documento", "CEDULA"); Field(adminFields,"number","Número de documento");
        adminFields.Children.Add(new TextBlock { Text = "Ingresará a ARGOS con el número de documento indicado arriba. No necesita crear un nombre de usuario." });
        Field(adminFields,"authorization","Referencia del acuerdo o autorización (en pruebas: PRUEBA-SIN-VALIDEZ-INSTITUCIONAL)");
        Field(adminFields,"authority","Autoridad que aprobó la designación"); Field(adminFields,"registrar","Responsable del registro");
        Field(adminFields,"start","Inicio de vigencia (DD/MM/AAAA)",DateTime.Today.ToString("dd/MM/yyyy"));
        Field(adminFields,"end","Fin de vigencia exclusivo (opcional, DD/MM/AAAA)");
        Field(adminFields,"email","Correo real del administrador: aquí recibirá el código");
        Secret(adminFields,"password","Contraseña temporal ARGOS: mínimo 8 caracteres, mayúscula, minúscula, número y símbolo");
        Secret(adminFields,"repeat","Repita la contraseña ARGOS");
        pages[3].Children.Add(send); send.Click += async (_, _) => await Run(SendEnrollment, EnrollmentStage.Data);
        pages[3].Children.Add(new TextBlock { Text = "Código recibido (vence en tres minutos, cinco intentos)" });
        pages[3].Children.Add(code);
        ActionButton(pages[3], "Corregir datos / solicitar un código nuevo", (_, _) => ResetChallenge());
        pages[4].Children.Add(new TextBlock { Text = "Administrador registrado y correo verificado. Al iniciar sesión deberá cambiar la contraseña temporal. Esta cuenta pertenece exclusivamente a la base de pruebas.", FontSize = 18 });
    }
    private string Value(string key) => fields[key].Text.Trim();
    private string Required(string key)
    { var value = Value(key); if (value.Length == 0) { fields[key].BringIntoView(); throw new ArgumentException("Complete: " + System.Windows.Automation.AutomationProperties.GetName(fields[key])); } return value; }
    private string Email(string key)
    { var value = Required(key); if (!MailAddress.TryCreate(value, out var email) || email.Address != value) throw new ArgumentException("Ingrese un correo válido y accesible."); return value; }
    private void ShowStep()
    {
        string[] titles = ["Conectemos ARGOS", "Preparemos el correo", "Su institución", "Administrador institucional", "Todo listo para ingresar"];
        TitleText.Text = titles[step]; Subtitle.Text = $"Paso {step + 1} de 5 · Puede regresar sin perder los campos ingresados.";
        furthest = Math.Max(furthest,step);
        Steps.Children.Clear();
        for(var i=0;i<titles.Length;i++)
        {
            var target=i;
            var button=new Button { Content=new TextBlock { Text=$"{i+1}. {titles[i]}" }, IsEnabled=i<=furthest,
                HorizontalContentAlignment=HorizontalAlignment.Left, Padding=new Thickness(10),
                Opacity=i==step?1:0.75, ToolTip=i<=furthest?"Seleccionar este paso":"Complete los pasos anteriores" };
            button.Click += async (_,_) => await NavigateAsync(target);
            Steps.Children.Add(button);
        }
        Body.Children.Clear(); Body.Children.Add(pages[step]); Back.IsEnabled = step > 0;
        if(registered) { adminFields.IsEnabled=false; send.IsEnabled=false; code.IsEnabled=false; }
        Next.Content = step == 3 && !registered ? "Verificar código y registrar" : step == 4 ? "Abrir ARGOS" : "Continuar";
    }
    private async void BackClick(object sender, RoutedEventArgs e) => await NavigateAsync(step-1);
    private async Task NavigateAsync(int target)
    {
        if(busy || target<0 || target>furthest || target==step) return;
        await Run(async () =>
        {
            if(target<step) { if(!registered) ResetChallenge(); step=target; ShowStep(); return; }
            // Forward jumps validate every intervening step; never bypass publication or MFA.
            while(step<target) await AdvanceAsync();
        },EnrollmentStage.Preparation);
    }
    private async void NextClick(object sender, RoutedEventArgs e) => await Run(AdvanceAsync,EnrollmentStage.Preparation);
    private async Task AdvanceAsync()
    {
        if (step == 0)
        {
            var candidate = new NpgsqlConnectionStringBuilder { Host="localhost",Port=5432,Database="ARGOS_PRUEBAS_20260917",Username=Required("dbUser"),Password=secrets["db"].Password,Timeout=5 }.ConnectionString;
            if (server is { HasExited:false } && candidate != connection) throw new ArgumentException("Detenga el servidor propio antes de cambiar la conexión.");
            await using var check = new NpgsqlConnection(candidate); await check.OpenAsync();
            connection = candidate; services?.Dispose(); services = new ServiceCollection().AddInfrastructure(connection).BuildServiceProvider();
        }
        else if (step == 1) SaveMail();
        else if (step == 2)
        {
            await using var scope = services!.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrganizationSetupRepository>();
            var current = await repo.GetCurrentAsync(); setup = current is null ? null : await repo.GetPublishedAsync(current.OrganizationId);
            if (setup is null || setup.Positions.Count == 0) throw new ArgumentException("Primero complete y publique la estructura institucional. Sus otros datos siguen guardados en esta ventana.");
            var previous=(positions.SelectedItem as PositionChoice)?.Id;
            var choices=setup.Positions.Select(p => new PositionChoice(p.Id, $"{p.Code} — {p.Name}")).ToArray();
            positions.ItemsSource = choices;
            positions.DisplayMemberPath = "Label"; positions.SelectedItem = choices.FirstOrDefault(p=>p.Id==previous) ?? choices[0];
        }
        else if (step == 3)
        {
            if(registered) { step++; ShowStep(); return; }
            stage = EnrollmentStage.VerifyingEmail;
            if (challenge is null || person is null || account is null || designation is null) throw new ArgumentException("Confirme la designación y envíe el código antes de registrar.");
            var result = challenge.Verify(person.Id,binding,"enrollment",code.Password); code.Clear();
            if (result != VerificationResult.Accepted)
            { if (result != VerificationResult.Rejected) ResetChallenge(); throw new ArgumentException(result == VerificationResult.Rejected ? "Código incorrecto. Intente de nuevo." : "El código venció o se agotaron los intentos. Solicite uno nuevo."); }
            account.ConfirmEnrollmentEmail($"email-challenge:{challenge.Id}"); stage = EnrollmentStage.Saving;
            try { await AdministratorEnrollment.SaveAsync(connection,person,designation,account); }
            catch { ResetChallenge(); throw; }
            registered = true; challenge.Dispose(); challenge = null;
        }
        else { await StartServer(); Open("https://localhost:7193/login/"); return; }
        step++; ShowStep();
    }
    private void SaveMail()
    {
        var entered = string.Concat(secrets["mail"].Password.Where(c => !char.IsWhiteSpace(c)));
        if (server is { HasExited:false } && entered.Length != 0 && entered != mailPassword)
            throw new ArgumentException("Detenga el servidor propio antes de reemplazar su clave de correo.");
        if (entered.Length != 0) mailPassword = entered;
        if (mailPassword.Length != 16) throw new ArgumentException("La clave de aplicación de Gmail debe tener 16 caracteres. No use la contraseña normal.");
        LocalSecrets.Save(mailPassword); secrets["mail"].Clear(); mailState.Text = "Clave guardada cifrada en Windows. No volverá a solicitarse en este asistente.";
    }
    private static DateTimeOffset Date(string text)
    {
        if (!DateTime.TryParseExact(text,["dd/MM/yyyy","yyyy-MM-dd"],CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)) throw new ArgumentException("Fecha inválida. Use DD/MM/AAAA.");
        return new DateTimeOffset(DateTime.SpecifyKind(date,DateTimeKind.Local));
    }
    private async Task SendEnrollment()
    {
        if(registered) return;
        ResetChallenge();
        if (positions.SelectedItem is not PositionChoice position || setup is null) throw new ArgumentException("Seleccione un puesto.");
        var destination = Email("email"); var password = secrets["password"].Password;
        AccessAccount.ValidatePasswordPolicy(password);
        if (password != secrets["repeat"].Password) throw new ArgumentException("Las contraseñas ARGOS no coinciden.");
        var start = Date(Required("start")); DateTimeOffset? end = Value("end").Length == 0 ? null : Date(Value("end"));
        if (end <= start) throw new ArgumentException("El fin de vigencia debe ser posterior al inicio.");
        person = new InstitutionalPerson(setup.OrganizationId,new OfficialIdentity(Required("issuer"),Required("document"),Required("number")),Required("name"));
        var assignment = person.Assign(setup,position.Id,start,end,Required("authorization"));
        designation = AdministratorDesignation.Create(person,assignment.Id,Required("authorization"),Required("authority"),Required("registrar"),DateTimeOffset.UtcNow,start,end);
        account = AccessAccount.Provision(person.Id,person.Identity.Number,password,VerificationChannel.InstitutionalEmail,destination,"pending");
        using (var scope = services!.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
            if (await db.Accounts.AnyAsync(a => a.Identification == account.Identification)) throw new ArgumentException("Ese usuario ya existe. Utilice el inicio de sesión o corrija el identificador.");
        }
        if (MessageBox.Show(this,$"Persona: {person.FullName}\nUsuario: {account.Identification}\nPuesto: {position.Label}\nCorreo: {destination}\nAutorización: {Value("authorization")}\n\nSe enviará un código real. La cuenta sólo se creará al verificarlo. ¿Continuar?",
            "Confirmar alta en la copia de pruebas",MessageBoxButton.YesNo,MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        stage = EnrollmentStage.SendingEmail; binding=Guid.NewGuid();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        challenge = await SecondFactorChallenge.IssueAsync(person.Id,binding,"enrollment",VerificationChannel.InstitutionalEmail,TimeSpan.FromMinutes(3),5,
            (otp,ct) => GmailVerificationDelivery.SendToEmailAsync(Sender,mailPassword,destination,otp,ct),cancellationToken:timeout.Token);
        adminFields.IsEnabled=false; send.IsEnabled=false;
        Status.Text="Código enviado. Revise el correo y escríbalo debajo. Aún no se creó la cuenta."; code.BringIntoView();
    }
    private void ResetChallenge()
    { if(registered) { Status.Text="La cuenta ya fue registrada. Sus datos no se modifican desde este asistente."; return; } challenge?.Dispose(); challenge=null; person=null; account=null; designation=null; code.Clear(); adminFields.IsEnabled=true; send.IsEnabled=true; }
    private async Task Run(Func<Task> action, EnrollmentStage initial)
    {
        if (busy) return; busy=true; stage=initial; Status.Text="Procesando…"; Next.IsEnabled=false; Back.IsEnabled=false; Body.IsEnabled=false; Steps.IsEnabled=false;
        try { await action(); if (Status.Text=="Procesando…") Status.Text=""; }
        catch (ArgumentException error) { Status.Text=error.Message; }
        catch { Status.Text=stage == EnrollmentStage.SendingEmail
            ? "No se pudo enviar el código. Revise la conexión y el destinatario. Si necesita cambiar la clave, detenga el servidor propio, vuelva al paso 2 y guarde la contraseña de APLICACIÓN de " + Sender + ". Los datos del formulario se conservan."
            : EnrollmentDiagnostics.Describe(stage); }
        finally { busy=false; Next.IsEnabled=true; Back.IsEnabled=step>0; Body.IsEnabled=true; Steps.IsEnabled=true; StopServer.Visibility=server is {HasExited:false}?Visibility.Visible:Visibility.Collapsed; }
    }
    private async Task StartServer()
    {
        if (IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(p => p.Port is 5193 or 7193))
        {
            if (server is not null && !server.HasExited) return;
            throw new ArgumentException("Ya hay un servidor en los puertos de pruebas. Cierre únicamente esa instancia y vuelva a intentar para aplicar esta configuración de correo.");
        }
        var app = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","app-home"));
        if (!Directory.Exists(app)) app = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","app-current"));
        if (!Directory.Exists(app)) app = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","app"));
        if (!File.Exists(Path.Combine(app,"ARGOS.API.dll"))) throw new ArgumentException("No se encontró la aplicación de pruebas junto al asistente.");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=app };
        foreach (var arg in new[]{Path.Combine(app,"ARGOS.API.dll"),"--contentRoot",app,"--urls","https://localhost:7193;http://localhost:5193"}) start.ArgumentList.Add(arg);
        start.Environment["ConnectionStrings__DefaultConnection"]=connection; start.Environment["Gmail__Sender"]=Sender;
        start.Environment["Gmail__AppPassword"]=mailPassword; start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";
        foreach(var key in new[]{"ARGOS_KMS_TOKEN","ARGOS_KMS_URL","ARGOS_KMS_KEY","ARGOS_ENCRYPTION_PROVIDER"}) start.Environment.Remove(key);
        server=Process.Start(start);
        using var client = new HttpClient { Timeout=TimeSpan.FromSeconds(2) };
        for(var i=0;i<20;i++)
        {
            if(server is null || server.HasExited) throw new InvalidOperationException("Server failed");
            try { if((await client.GetAsync("http://localhost:5193/health")).IsSuccessStatusCode) return; } catch(HttpRequestException) {} catch(TaskCanceledException) {}
            await Task.Delay(500);
        }
        throw new ArgumentException("El servidor tarda en responder. Espere unos segundos y vuelva a intentar.");
    }
    private static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute=true });
    private async void StopClick(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (server is null || server.HasExited) { Status.Text="Este asistente no tiene un servidor activo. No se detuvo ningún otro proceso."; return; }
        await Run(async () => { server.Kill(); await server.WaitForExitAsync(); server.Dispose(); server=null; Status.Text="Servidor de pruebas detenido. Puede corregir la configuración y volver a abrir ARGOS."; },EnrollmentStage.Preparation);
    }
    private sealed record PositionChoice(Guid Id,string Label);
}
