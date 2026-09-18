using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;
using A = DocumentFormat.OpenXml.Drawing;

namespace HCMS_Api.Components.DMS.ESS;

/// <summary>
/// Renders an already-merged Word document as HTML, so an approver can read it on screen instead
/// of downloading it.
///
/// The input is the exact byte stream MergeContentIntoTemplateAsync produces -- every placeholder
/// already substituted, signature images already embedded, watermark already stamped. Converting
/// that (rather than rebuilding a preview from the underlying data) is what makes the popup and
/// the downloaded .docx agree: there is only one merge, and the preview is a second rendering of
/// its output. It also means a client re-uploading a template with a different header layout gets
/// a preview that follows it, with no code change.
///
/// This is the mirror of HtmlToOpenXmlConverter, and deliberately covers the same ground the
/// templates actually use: paragraphs, runs with bold/italic/underline/strike/colour, tables with
/// borders and merged cells, and embedded images. Headers and footers are converted through the
/// same code as the body -- in these templates both are a single table, which is exactly where the
/// document number, version, effective date, logo and the approver signature matrix live, and the
/// whole point of showing them.
///
/// Not attempted: page layout (a browser has no pages, so "Page No. x of y" renders as its literal
/// text), tab stops, and Word's own numbering definitions -- list text still appears, just without
/// generated numbers. Anything unrecognised falls through to its plain text so content is never
/// silently dropped.
/// </summary>
public static class OpenXmlToHtmlConverter
{
    /// <summary>Header / body / footer as three separate fragments, so the caller can frame them.</summary>
    public sealed class DocumentHtml
    {
        public string HeaderHtml { get; set; } = "";
        public string BodyHtml { get; set; } = "";
        public string FooterHtml { get; set; } = "";
    }

    public static DocumentHtml Convert(byte[] mergedDocxBytes)
    {
        var result = new DocumentHtml();
        if (mergedDocxBytes == null || mergedDocxBytes.Length == 0)
            return result;

        using var stream = new MemoryStream(mergedDocxBytes);
        using var doc = WordprocessingDocument.Open(stream, false);

        var mainPart = doc.MainDocumentPart;
        if (mainPart == null)
            return result;

        // First header/footer only. A template can define separate first-page/even-page variants,
        // but the ones in use here define a single header and footer that repeat, and a scrolling
        // preview has no page boundaries to switch between them at anyway.
        var headerPart = mainPart.HeaderParts.FirstOrDefault();
        if (headerPart?.Header != null)
            result.HeaderHtml = ConvertContainer(headerPart.Header, headerPart);

        var footerPart = mainPart.FooterParts.FirstOrDefault();
        if (footerPart?.Footer != null)
            result.FooterHtml = ConvertContainer(footerPart.Footer, footerPart);

        if (mainPart.Document?.Body != null)
            result.BodyHtml = ConvertContainer(mainPart.Document.Body, mainPart);

        return result;
    }

    // ---------------------------------------------------------------------
    // Block level
    // ---------------------------------------------------------------------

    private static string ConvertContainer(OpenXmlElement container, OpenXmlPart owningPart)
    {
        var sb = new StringBuilder();
        foreach (var child in container.ChildElements)
            AppendBlock(sb, child, owningPart);
        return sb.ToString();
    }

    private static void AppendBlock(StringBuilder sb, OpenXmlElement element, OpenXmlPart owningPart)
    {
        switch (element)
        {
            case Paragraph paragraph:
                AppendParagraph(sb, paragraph, owningPart);
                break;

            case Table table:
                AppendTable(sb, table, owningPart);
                break;

            // SectionProperties carries page setup, which a scrolling preview has no use for.
            case SectionProperties:
                break;

            default:
                // Containers this converter does not model explicitly (content controls, for one)
                // still hold real paragraphs -- walk into them rather than dropping the content.
                foreach (var child in element.ChildElements)
                    AppendBlock(sb, child, owningPart);
                break;
        }
    }

    private static void AppendParagraph(StringBuilder sb, Paragraph paragraph, OpenXmlPart owningPart)
    {
        var inner = new StringBuilder();
        foreach (var child in paragraph.ChildElements)
            AppendInline(inner, child, owningPart);

        var content = inner.ToString();

        // An empty paragraph is vertical space in Word, so it has to survive as something with
        // height rather than collapsing to nothing.
        if (content.Trim().Length == 0)
        {
            sb.Append("<p class=\"dx-p dx-empty\">&nbsp;</p>");
            return;
        }

        var style = new StringBuilder();

        var justification = paragraph.ParagraphProperties?.Justification?.Val?.Value;
        if (justification == JustificationValues.Center) style.Append("text-align:center;");
        else if (justification == JustificationValues.Right) style.Append("text-align:right;");
        else if (justification == JustificationValues.Both) style.Append("text-align:justify;");

        var headingLevel = HeadingLevel(paragraph);
        var tag = headingLevel > 0 ? "h" + headingLevel : "p";

        sb.Append('<').Append(tag).Append(" class=\"dx-p\"");
        if (style.Length > 0) sb.Append(" style=\"").Append(style).Append('"');
        sb.Append('>').Append(content).Append("</").Append(tag).Append('>');
    }

    /// <summary>Word heading styles are named "Heading1".."Heading6" (or "heading 1" with a space).</summary>
    private static int HeadingLevel(Paragraph paragraph)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (string.IsNullOrWhiteSpace(styleId)) return 0;

        var normalized = styleId.Replace(" ", "").ToLowerInvariant();
        if (!normalized.StartsWith("heading")) return 0;

        return int.TryParse(normalized.Substring("heading".Length), out var level) && level >= 1 && level <= 6
            ? level
            : 0;
    }

    private static void AppendTable(StringBuilder sb, Table table, OpenXmlPart owningPart)
    {
        sb.Append("<table class=\"dx-table\">");

        foreach (var row in table.Elements<TableRow>())
        {
            sb.Append("<tr>");
            foreach (var cell in row.Elements<TableCell>())
            {
                var properties = cell.TableCellProperties;

                // A horizontally merged cell is one cell with gridSpan; a vertically merged one is
                // a continuation cell that must not be emitted at all, or the row grows an extra
                // column and the whole table shifts.
                var vMerge = properties?.VerticalMerge;
                if (vMerge != null && (vMerge.Val?.Value == null || vMerge.Val.Value == MergedCellValues.Continue))
                    continue;

                sb.Append("<td");

                var gridSpan = properties?.GridSpan?.Val?.Value ?? 1;
                if (gridSpan > 1) sb.Append(" colspan=\"").Append(gridSpan).Append('"');

                var style = new StringBuilder();
                var shadingFill = properties?.Shading?.Fill?.Value;
                if (!string.IsNullOrWhiteSpace(shadingFill) && !shadingFill.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    style.Append("background-color:#").Append(shadingFill).Append(';');

                var width = properties?.TableCellWidth;
                if (width?.Type != null && width.Type.Value == TableWidthUnitValues.Pct && width.Width?.Value != null
                    && int.TryParse(width.Width.Value, out var pct) && pct > 0)
                {
                    // Word stores table percentages in fiftieths of a percent.
                    style.Append("width:").Append(Math.Round(pct / 50.0, 2)).Append("%;");
                }

                if (style.Length > 0) sb.Append(" style=\"").Append(style).Append('"');
                sb.Append('>');

                foreach (var child in cell.ChildElements)
                {
                    if (child is TableCellProperties) continue;
                    AppendBlock(sb, child, owningPart);
                }

                sb.Append("</td>");
            }
            sb.Append("</tr>");
        }

        sb.Append("</table>");
    }

    // ---------------------------------------------------------------------
    // Inline level
    // ---------------------------------------------------------------------

    private static void AppendInline(StringBuilder sb, OpenXmlElement element, OpenXmlPart owningPart)
    {
        switch (element)
        {
            case Run run:
                AppendRun(sb, run, owningPart);
                break;

            case Hyperlink hyperlink:
                // Rendered as plain styled text: resolving the target needs the owning part's
                // relationship table, and a preview is for reading, not navigating.
                foreach (var child in hyperlink.ChildElements)
                    AppendInline(sb, child, owningPart);
                break;

            case ParagraphProperties:
                break;

            default:
                foreach (var child in element.ChildElements)
                    AppendInline(sb, child, owningPart);
                break;
        }
    }

    private static void AppendRun(StringBuilder sb, Run run, OpenXmlPart owningPart)
    {
        var inner = new StringBuilder();

        foreach (var child in run.ChildElements)
        {
            switch (child)
            {
                case Text text:
                    inner.Append(Escape(text.Text));
                    break;

                case Break:
                    inner.Append("<br/>");
                    break;

                case TabChar:
                    inner.Append("&nbsp;&nbsp;&nbsp;&nbsp;");
                    break;

                case Drawing drawing:
                    AppendImage(inner, drawing, owningPart);
                    break;

                // Field codes (PAGE / NUMPAGES, inserted by the merge for page numbers) have no
                // meaning without pages -- their surrounding literal text still renders.
                case FieldCode:
                case FieldChar:
                    break;
            }
        }

        if (inner.Length == 0) return;

        var properties = run.RunProperties;
        var style = new StringBuilder();

        if (properties != null)
        {
            // In OOXML a Bold element with no Val means "on"; Val="false" means off.
            if (IsOn(properties.Bold)) style.Append("font-weight:600;");
            if (IsOn(properties.Italic)) style.Append("font-style:italic;");

            var underline = properties.Underline?.Val?.Value;
            var strike = IsOn(properties.Strike);
            if (underline != null && underline != UnderlineValues.None && strike) style.Append("text-decoration:underline line-through;");
            else if (underline != null && underline != UnderlineValues.None) style.Append("text-decoration:underline;");
            else if (strike) style.Append("text-decoration:line-through;");

            var color = properties.Color?.Val?.Value;
            if (!string.IsNullOrWhiteSpace(color) && !color.Equals("auto", StringComparison.OrdinalIgnoreCase))
                style.Append("color:#").Append(color).Append(';');

            var highlight = properties.Highlight?.Val;
            if (highlight != null && highlight.Value != HighlightColorValues.None)
                style.Append("background-color:").Append(highlight.Value.ToString().ToLowerInvariant()).Append(';');

            // Word stores font size in half-points.
            if (int.TryParse(properties.FontSize?.Val?.Value, out var halfPoints) && halfPoints > 0)
                style.Append("font-size:").Append(Math.Round(halfPoints / 2.0, 1)).Append("pt;");
        }

        if (style.Length == 0)
        {
            sb.Append(inner);
            return;
        }

        sb.Append("<span style=\"").Append(style).Append("\">").Append(inner).Append("</span>");
    }

    private static bool IsOn(OnOffType? toggle) =>
        toggle != null && (toggle.Val?.Value ?? true);

    /// <summary>
    /// Emits an embedded image as a data: URI.
    ///
    /// Read from the part that OWNS the drawing, not from the main document part: every part
    /// (the body, and each header and footer) has its own relationship id space in the package,
    /// so the logo in a header resolves only through that header's part. Looking it up on the
    /// wrong part silently finds nothing -- which is exactly how a header renders with its logo
    /// missing and no error anywhere.
    /// </summary>
    private static void AppendImage(StringBuilder sb, Drawing drawing, OpenXmlPart owningPart)
    {
        var blip = drawing.Descendants<A.Blip>().FirstOrDefault();
        var embedId = blip?.Embed?.Value;
        if (string.IsNullOrWhiteSpace(embedId)) return;

        if (owningPart.GetPartById(embedId) is not ImagePart imagePart) return;

        byte[] bytes;
        using (var imageStream = imagePart.GetStream(FileMode.Open, FileAccess.Read))
        using (var buffer = new MemoryStream())
        {
            imageStream.CopyTo(buffer);
            bytes = buffer.ToArray();
        }

        if (bytes.Length == 0) return;

        var contentType = string.IsNullOrWhiteSpace(imagePart.ContentType) ? "image/png" : imagePart.ContentType;

        // Width comes from the drawing's own extent, in EMUs (914400 per inch, 96 px per inch),
        // so the image keeps the size the template author gave it.
        var extent = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>().FirstOrDefault();
        var widthAttribute = "";
        if (extent?.Cx?.Value is long cx && cx > 0)
            widthAttribute = " width=\"" + (int)Math.Round(cx / 914400.0 * 96.0) + "\"";

        sb.Append("<img").Append(widthAttribute)
          .Append(" style=\"max-width:100%;height:auto;\" src=\"data:")
          .Append(contentType).Append(";base64,")
          .Append(System.Convert.ToBase64String(bytes))
          .Append("\" alt=\"\" />");
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
