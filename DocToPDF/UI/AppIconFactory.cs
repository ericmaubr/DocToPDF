using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace DocToPDF.UI;

/// <summary>
/// Ícone da aplicação (barra de tarefas/janela): uma página de documento com canto dobrado
/// e o rótulo "PDF" em vermelho. Desenhado por código para evitar um asset binário.
/// </summary>
internal static class AppIconFactory
{
    public static Icon Create()
    {
        try
        {
            const int size = 256;
            using var bitmap = new Bitmap(size, size);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);

            // Página: margens e canto dobrado.
            var left = size * 0.18f;
            var right = size * 0.82f;
            var top = size * 0.10f;
            var bottom = size * 0.90f;
            var fold = size * 0.22f;

            using var page = new GraphicsPath();
            page.AddLine(left, top, right - fold, top);
            page.AddLine(right - fold, top, right, top + fold);
            page.AddLine(right, top + fold, right, bottom);
            page.AddLine(right, bottom, left, bottom);
            page.CloseFigure();

            using (var fill = new SolidBrush(Color.White))
                graphics.FillPath(fill, page);
            using (var border = new Pen(Color.FromArgb(120, 120, 120), size * 0.012f))
                graphics.DrawPath(border, page);

            // Canto dobrado.
            using (var foldPath = new GraphicsPath())
            {
                foldPath.AddLine(right - fold, top, right - fold, top + fold);
                foldPath.AddLine(right - fold, top + fold, right, top + fold);
                foldPath.CloseFigure();
                using var foldFill = new SolidBrush(Color.FromArgb(210, 210, 210));
                graphics.FillPath(foldFill, foldPath);
                using var foldBorder = new Pen(Color.FromArgb(120, 120, 120), size * 0.012f);
                graphics.DrawPath(foldBorder, foldPath);
            }

            // Faixa "PDF" em vermelho.
            var bandHeight = size * 0.26f;
            var bandTop = size * 0.55f;
            using (var band = new SolidBrush(Color.FromArgb(197, 40, 40)))
                graphics.FillRectangle(band, left, bandTop, right - left, bandHeight);

            using var font = new Font("Arial", size * 0.16f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var text = new SolidBrush(Color.White);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            graphics.DrawString("PDF", font, text,
                new RectangleF(left, bandTop, right - left, bandHeight), format);

            var handle = bitmap.GetHicon();
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        catch
        {
            return SystemIcons.Application;
        }
    }
}
