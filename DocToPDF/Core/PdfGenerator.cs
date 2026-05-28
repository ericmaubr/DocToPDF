using DocToPDF.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DocToPDF.Core;

public static class PdfGenerator
{
    private const string ContentFont = "Liberation Mono";
    private const string FallbackFont = "Courier New";

    private static readonly object LicenseLock = new();
    private static bool _licenseSet;

    public static void Generate(DocumentNode root, string outputPath)
    {
        EnsureLicense();
        var lines = new List<PdfLine>();
        RenderNode(root, lines, nestingLevel: 0);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20, Unit.Millimetre);
                page.DefaultTextStyle(style => style
                    .FontSize(9)
                    .FontFamily(ContentFont, FallbackFont));

                page.Content().Column(column =>
                {
                    foreach (var line in lines)
                    {
                        if (line.IsSectionHeader)
                        {
                            column.Item().Text(line.Text)
                                .FontSize(10)
                                .Bold()
                                .FontFamily(ContentFont, FallbackFont);
                        }
                        else
                        {
                            column.Item().Text(line.Text)
                                .FontSize(9)
                                .FontFamily(ContentFont, FallbackFont);
                        }
                    }
                });
            });
        }).GeneratePdf(outputPath);
    }

    private readonly record struct PdfLine(string Text, bool IsSectionHeader);

    private static void RenderNode(DocumentNode node, List<PdfLine> lines, int nestingLevel)
    {
        var indent = new string(' ', nestingLevel * 4);

        if (node.IsArrayItem)
        {
            var arrayLine = string.IsNullOrEmpty(node.Key)
                ? $"{indent}[{node.ArrayIndex}]"
                : $"{indent}[{node.ArrayIndex}] {FormatKey(node.Key)}";
            lines.Add(new PdfLine(arrayLine.TrimEnd(), false));

            if (!string.IsNullOrEmpty(node.Value))
            {
                lines.Add(new PdfLine($"{indent}    {FormatKey(node.Key)}: {node.Value}", false));
                return;
            }

            foreach (var child in node.Children)
                RenderNode(child, lines, nestingLevel + 1);

            return;
        }

        if (node.Value == null)
        {
            if (!string.IsNullOrEmpty(node.Key))
            {
                lines.Add(new PdfLine(
                    $"{indent}{FormatKey(node.Key).ToUpperInvariant()}",
                    IsSectionHeader: true));
            }

            foreach (var child in node.Children)
                RenderNode(child, lines, nestingLevel + (string.IsNullOrEmpty(node.Key) ? 0 : 1));

            return;
        }

        lines.Add(new PdfLine($"{indent}{FormatKey(node.Key)}: {node.Value}", false));
    }

    private static void EnsureLicense()
    {
        if (_licenseSet)
            return;

        lock (LicenseLock)
        {
            if (_licenseSet)
                return;

            QuestPDF.Settings.License = LicenseType.Community;
            _licenseSet = true;
        }
    }

    public static string FormatKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        if (key.StartsWith('@'))
            key = key[1..];

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < key.Length; i++)
        {
            var c = key[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(key[i - 1]))
                sb.Append(' ');
            sb.Append(c);
        }

        var result = sb.ToString();
        if (result.Length == 0)
            return result;

        return char.ToUpper(result[0]) + result[1..];
    }
}
