using System.Text;
using System.Xml.Linq;
using DocToPDF.Models;

namespace DocToPDF.Core;

public static class XmlParser
{
    public static DocumentNode Parse(string filePath)
    {
        var document = LoadDocument(filePath);
        var root = document.Root ?? throw new InvalidOperationException("XML document has no root element.");
        return ConvertElement(root);
    }

    private static XDocument LoadDocument(string filePath)
    {
        if (UsesIso88591Encoding(filePath))
        {
            using var reader = new StreamReader(filePath, Encoding.GetEncoding("iso-8859-1"));
            return XDocument.Load(reader, LoadOptions.None);
        }

        return XDocument.Load(filePath, LoadOptions.None);
    }

    private static bool UsesIso88591Encoding(string filePath)
    {
        using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        for (var i = 0; i < 5 && !reader.EndOfStream; i++)
        {
            var line = reader.ReadLine();
            if (line == null)
                break;

            if (line.Contains("encoding=\"iso-8859-1\"", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("encoding='iso-8859-1'", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (line.Contains("?>"))
                break;
        }

        return false;
    }

    private static DocumentNode ConvertElement(XElement element)
    {
        var node = new DocumentNode
        {
            Key = element.Name.LocalName
        };

        foreach (var attribute in element.Attributes())
        {
            node.Children.Add(new DocumentNode
            {
                Key = $"@{attribute.Name.LocalName}",
                Value = attribute.Value.Trim()
            });
        }

        var childElements = element.Elements().ToList();
        if (childElements.Count == 0)
        {
            node.Value = element.Value.Trim();
            return node;
        }

        var groups = childElements
            .GroupBy(e => e.Name.LocalName)
            .SelectMany(group => BuildChildNodes(group.Key, group.ToList()));

        node.Children.AddRange(groups);
        return node;
    }

    private static IEnumerable<DocumentNode> BuildChildNodes(string tagName, List<XElement> elements)
    {
        var isArray = elements.Count > 1;
        var index = 1;

        foreach (var element in elements)
        {
            var child = ConvertElement(element);
            child.Key = isArray ? string.Empty : tagName;

            if (isArray)
            {
                child.IsArrayItem = true;
                child.ArrayIndex = index++;
            }

            yield return child;
        }
    }
}
