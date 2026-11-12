using System.Windows;
using System.Windows.Controls;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

namespace ARGOS.Desktop;

public partial class MainWindow
{
    private CapturedDocument? capturedDocument;

    private async void ImportDocument(object sender, RoutedEventArgs e)
    {
        ImportDocumentButton.IsEnabled = ScanDocumentButton.IsEnabled = false;
        DocumentStatus.Text = "Analizando el documento temporalmente…";
        try { await ShowCapturedDocumentAsync(await DocumentCapture.ImportAsync()); }
        catch (OperationCanceledException) { DocumentStatus.Text = "Importación cancelada."; }
        catch (Exception error) { DocumentStatus.Text = "No se pudo importar el documento: " + error.Message; }
        finally { ImportDocumentButton.IsEnabled = ScanDocumentButton.IsEnabled = true; }
    }

    private async void ScanDocument(object sender, RoutedEventArgs e)
    {
        ImportDocumentButton.IsEnabled = ScanDocumentButton.IsEnabled = false;
        DocumentStatus.Text = "Abriendo el escáner seleccionado por Windows…";
        try { await ShowCapturedDocumentAsync(await DocumentCapture.ScanAsync()); }
        catch (OperationCanceledException) { DocumentStatus.Text = "Escaneo cancelado."; }
        catch (Exception error) { DocumentStatus.Text = "No se pudo escanear el documento: " + error.Message; }
        finally { ImportDocumentButton.IsEnabled = ScanDocumentButton.IsEnabled = true; }
    }

    private Task ShowCapturedDocumentAsync(CapturedDocument? document)
    {
        if (document is null) { DocumentStatus.Text = "Importación cancelada."; return Task.CompletedTask; }
        DiscardCapturedDocument(); capturedDocument = document;
        DocumentMetadata.Text = $"{document.Name}\n{document.MimeType} · {document.Content.Length / 1024d:0.0} KB\nHuella SHA-256: {document.Hash[..16]}…";
        DocumentPreview.Source = document.Preview; RecognizedText.Text = document.RecognizedText;
        var suggested = DocumentClassification.Items.OfType<DocumentCategoryOption>()
            .FirstOrDefault(item => item.Name.Contains(document.SuggestedClass, StringComparison.OrdinalIgnoreCase)
                || document.SuggestedClass.Contains(item.Name, StringComparison.OrdinalIgnoreCase));
        DocumentClassification.SelectedItem = suggested ?? DocumentClassification.Items.OfType<DocumentCategoryOption>().FirstOrDefault();
        DocumentResult.Visibility = Visibility.Visible; DiscardDocumentButton.IsEnabled = true;
        ConfirmDocumentButton.IsEnabled = DocumentClassification.Items.Count > 0;
        DocumentStatus.Text = "Clasificación sugerida lista. Confirme visualmente el documento y su clase antes de remitirlo.";
        return Task.CompletedTask;
    }

    private void DiscardDocument(object sender, RoutedEventArgs e) => DiscardCapturedDocument();

    private void DiscardCapturedDocument()
    {
        if (capturedDocument is not null) Array.Clear(capturedDocument.Content);
        capturedDocument = null; DocumentPreview.Source = null; RecognizedText.Clear(); DocumentMetadata.Text = string.Empty;
        DocumentResult.Visibility = Visibility.Collapsed; DiscardDocumentButton.IsEnabled = ConfirmDocumentButton.IsEnabled = false;
        DocumentStatus.Text = "El documento temporal fue descartado. ARGOS no conserva una copia en este equipo.";
    }

    private async Task LoadDocumentCategoriesAsync()
    {
        if (loginServer is null) return;
        try
        {
            using var response = await client.GetAsync(new Uri(loginServer, "api/document-categories/"), lifetime.Token);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                DocumentPanel.Visibility = Visibility.Collapsed;
                return;
            }
            response.EnsureSuccessStatusCode();
            var categories = await response.Content.ReadFromJsonAsync<List<DocumentCategoryOption>>(lifetime.Token) ?? [];
            DocumentClassification.ItemsSource = categories;
            if (categories.Count == 0)
            {
                ImportDocumentButton.IsEnabled = ScanDocumentButton.IsEnabled = false;
                DocumentStatus.Text = "No hay categorías documentales activas. Un administrador debe definirlas antes de capturar documentos.";
            }
            else ImportDocumentButton.IsEnabled = ScanDocumentButton.IsEnabled = true;
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
        {
            DocumentPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async void ConfirmDocument(object sender, RoutedEventArgs e)
    {
        if (capturedDocument is null || loginServer is null) return;
        if (!Guid.TryParse(CaseFileId.Text.Trim(), out var caseFileId))
        {
            DocumentStatus.Text = "Indique el identificador válido del expediente de destino.";
            return;
        }
        if (DocumentClassification.SelectedItem is not DocumentCategoryOption finalCategory)
        {
            DocumentStatus.Text = "Seleccione la categoría institucional definitiva.";
            return;
        }
        if (capturedDocument.Content.Length > 10 * 1024 * 1024)
        {
            DocumentStatus.Text = "Este archivo supera el límite seguro de 10 MB para la transmisión actual.";
            return;
        }
        var suggestion = DocumentClassification.Items.OfType<DocumentCategoryOption>().FirstOrDefault(item =>
            item.Name.Contains(capturedDocument.SuggestedClass, StringComparison.OrdinalIgnoreCase)
            || capturedDocument.SuggestedClass.Contains(item.Name, StringComparison.OrdinalIgnoreCase));
        ConfirmDocumentButton.IsEnabled = ImportDocumentButton.IsEnabled = ScanDocumentButton.IsEnabled = false;
        DocumentStatus.Text = "Cifrando y remitiendo el documento al servidor…";
        try
        {
            var request = new
            {
                ContentType = capturedDocument.MimeType,
                Base64Content = Convert.ToBase64String(capturedDocument.Content),
                FinalCategoryId = finalCategory.Id,
                SuggestedCategoryId = suggestion?.Id,
                SuggestionSource = "OCR local y reglas de clasificación",
                Confidence = suggestion is null ? (decimal?)null : 0.50m
            };
            using var response = await client.PostAsJsonAsync(new Uri(loginServer, $"api/case-files/{caseFileId:D}/document-capture"), request, lifetime.Token);
            if (!response.IsSuccessStatusCode)
            {
                DocumentStatus.Text = response.StatusCode switch
                {
                    HttpStatusCode.Forbidden => "Su puesto no tiene permiso para incorporar documentos en este expediente.",
                    HttpStatusCode.NotFound => "No se encontró el expediente autorizado indicado.",
                    HttpStatusCode.Conflict => "El expediente no permite incorporar más contenido en su estado actual.",
                    _ => "No se confirmó la recepción. El documento continúa solo en memoria y puede reintentarlo o descartarlo."
                };
                return;
            }
            DiscardCapturedDocument();
            CaseFileId.Clear();
            DocumentStatus.Text = "Documento cifrado, clasificado y recibido por ARGOS Server. La decisión humana quedó auditada.";
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
        { DocumentStatus.Text = "No se confirmó la recepción. Revise la conexión; el documento sigue temporalmente en memoria."; }
        finally
        {
            ImportDocumentButton.IsEnabled = ScanDocumentButton.IsEnabled = DocumentClassification.Items.Count > 0;
            ConfirmDocumentButton.IsEnabled = capturedDocument is not null && DocumentClassification.Items.Count > 0;
        }
    }

    private sealed record DocumentCategoryOption(Guid Id, string Code, string Name, string? Description);
}
