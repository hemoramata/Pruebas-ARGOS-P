using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;

namespace ARGOS.Desktop;

public partial class MainWindow
{
    private async void LoadCaseFiles(object sender, RoutedEventArgs e) => await LoadCaseFilesAsync();

    private async Task LoadCaseFilesAsync()
    {
        if (loginServer is null) return;
        try
        {
            using var response = await client.GetAsync(new Uri(loginServer, "api/case-files/"), lifetime.Token);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized) { CaseFilePanel.Visibility = Visibility.Collapsed; return; }
            response.EnsureSuccessStatusCode();
            var cases = await response.Content.ReadFromJsonAsync<List<CaseFileOption>>(lifetime.Token) ?? [];
            CaseFileSelector.ItemsSource = cases; CaseFilePanel.Visibility = Visibility.Visible;
            CaseFileStatus.Text = cases.Count == 0 ? "No hay expedientes disponibles. Puede crear uno si su puesto tiene permiso." : $"{cases.Count} expediente(s) disponible(s).";
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
        { CaseFilePanel.Visibility = Visibility.Collapsed; }
    }

    private void SelectCaseFile(object sender, SelectionChangedEventArgs e)
    {
        if (CaseFileSelector.SelectedItem is CaseFileOption item) { CaseFileId.Text = item.Id.ToString(); CaseFileStatus.Text = $"Expediente seleccionado: {item.Display}."; }
    }

    private async void CreateCaseFile(object sender, RoutedEventArgs e)
    {
        if (loginServer is null || string.IsNullOrWhiteSpace(NewCaseFileNumber.Text)) return;
        CreateCaseFileButton.IsEnabled = false; CaseFileStatus.Text = "Creando expediente…";
        try
        {
            using var response = await client.PostAsJsonAsync(new Uri(loginServer, "api/case-files/"), new { Number = NewCaseFileNumber.Text.Trim() }, lifetime.Token);
            if (!response.IsSuccessStatusCode) { CaseFileStatus.Text = response.StatusCode == HttpStatusCode.Forbidden ? "Su puesto no puede crear expedientes." : "No se pudo crear el expediente. Use el formato TT-DD-CCCC-AAAA."; return; }
            var created = await response.Content.ReadFromJsonAsync<CreatedCaseFile>(lifetime.Token);
            if (created is null) throw new InvalidOperationException();
            NewCaseFileNumber.Clear(); await LoadCaseFilesAsync();
            var selected = CaseFileSelector.Items.OfType<CaseFileOption>().FirstOrDefault(x => x.Id == created.Id);
            CaseFileSelector.SelectedItem = selected; CaseFileId.Text = created.Id.ToString();
            CaseFileStatus.Text = "Expediente creado y seleccionado.";
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException)
        { CaseFileStatus.Text = "No se pudo confirmar la creación del expediente."; }
        finally { CreateCaseFileButton.IsEnabled = true; }
    }

    private sealed record CaseFileOption(Guid Id, string Number, string Status)
    { public string Display => $"{Number} · {Status}"; }
    private sealed record CreatedCaseFile(Guid Id, string Number, string Status);
}
