namespace DocToPDF.UI;

internal static class TrayIconFactory
{
    public static Icon Create(Color color)
    {
        try
        {
            const int size = 32;
            using var bitmap = new Bitmap(size, size);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.FillEllipse(brush, 2, 2, size - 4, size - 4);

            var handle = bitmap.GetHicon();
            var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        catch
        {
            return SystemIcons.Application;
        }
    }
}
