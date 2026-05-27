using System.Text.Json;
using DocToPDF.Models;

namespace DocToPDF.Core;

public static class JsonParser
{
    public static DocumentNode Parse(string filePath)
    {
        var json = File.ReadAllText(filePath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Object)
        {
            var properties = root.EnumerateObject().ToList();
            if (properties.Count == 1)
                return ConvertValue(properties[0].Name, properties[0].Value);

            var node = new DocumentNode
            {
                Key = Path.GetFileNameWithoutExtension(filePath)
            };

            foreach (var property in properties)
                node.Children.Add(ConvertValue(property.Name, property.Value));

            return node;
        }

        return ConvertValue(Path.GetFileNameWithoutExtension(filePath), root);
    }

    private static DocumentNode ConvertValue(string key, JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(key, element),
            JsonValueKind.Array => ConvertArray(key, element),
            JsonValueKind.String => CreateLeaf(key, element.GetString() ?? ""),
            JsonValueKind.Number => CreateLeaf(key, element.GetRawText()),
            JsonValueKind.True => CreateLeaf(key, "true"),
            JsonValueKind.False => CreateLeaf(key, "false"),
            JsonValueKind.Null => CreateLeaf(key, "null"),
            _ => CreateLeaf(key, element.ToString())
        };
    }

    private static DocumentNode ConvertObject(string key, JsonElement element)
    {
        var node = new DocumentNode { Key = key };

        foreach (var property in element.EnumerateObject())
            node.Children.Add(ConvertValue(property.Name, property.Value));

        return node;
    }

    private static DocumentNode ConvertArray(string key, JsonElement element)
    {
        var node = new DocumentNode { Key = key };
        var index = 1;

        foreach (var item in element.EnumerateArray())
        {
            var child = ConvertArrayItem(item, index++);
            node.Children.Add(child);
        }

        return node;
    }

    private static DocumentNode ConvertArrayItem(JsonElement element, int index)
    {
        DocumentNode child = element.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject("", element),
            JsonValueKind.Array => ConvertArray("", element),
            JsonValueKind.String => CreateLeaf("", element.GetString() ?? ""),
            JsonValueKind.Number => CreateLeaf("", element.GetRawText()),
            JsonValueKind.True => CreateLeaf("", "true"),
            JsonValueKind.False => CreateLeaf("", "false"),
            JsonValueKind.Null => CreateLeaf("", "null"),
            _ => CreateLeaf("", element.ToString())
        };

        child.IsArrayItem = true;
        child.ArrayIndex = index;
        return child;
    }

    private static DocumentNode CreateLeaf(string key, string value) =>
        new() { Key = key, Value = value };
}
