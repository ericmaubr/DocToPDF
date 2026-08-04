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

        using var content = BuildContent(pdfPath);
        using var response = Http.PostAsync(url, content).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        return response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Separado de <see cref="Normalizar"/> pra dar pra verificar o Content-Disposition
    /// gerado sem precisar de rede (ver <see cref="Verify.ProcessingVerifier"/>).
    /// </summary>
    internal static MultipartFormDataContent BuildContent(string pdfPath)
    {
        var content = new MultipartFormDataContent();
        var fileStream = File.OpenRead(pdfPath);
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "arquivo");

        // Content-Disposition manual: o overload Add(content, name, fileName) do .NET codifica
        // nomes com acento em MIME encoded-word (RFC 2047) + RFC 5987, e o parser multipart do
        // FastAPI/Starlette não decodifica nenhum dos dois — lê o filename literal (o base64),
        // que não termina em ".pdf", e o servidor rejeita com 400 achando que não é PDF.
        fileContent.Headers.Remove("Content-Disposition");
        fileContent.Headers.TryAddWithoutValidation(
            "Content-Disposition",
            $"form-data; name=\"arquivo\"; filename=\"{Path.GetFileName(pdfPath)}\"");

        return content;
    }
}
