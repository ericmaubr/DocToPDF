namespace DocToPDF.Models;

public class DocumentNode
{
    public string Key { get; set; } = "";
    public string? Value { get; set; }
    public List<DocumentNode> Children { get; set; } = new();
    public bool IsArrayItem { get; set; }
    public int ArrayIndex { get; set; }
}
