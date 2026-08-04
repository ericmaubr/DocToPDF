namespace DocToPDF.Core;

/// <summary>
/// Envia um PDF externo para o endpoint /converter do conta-tools-pdf,
/// que roda OCR (ocrmypdf) e devolve o PDF com texto normalizado.
/// </summary>
public static class PdfPadronizadorClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public static byte[] Normalizar(string baseUrl, string pdfPath)
    {
        var url = baseUrl.TrimEnd('/') + "/converter";

        using var content = new MultipartFormDataContent();
        using var fileStream = File.OpenRead(pdfPath);
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "arquivo", Path.GetFileName(pdfPath));

        using var response = Http.PostAsync(url, content).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        return response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
    }
}
