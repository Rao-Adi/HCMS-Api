using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace HCMS_Api.Components.DMS.ESS;

// Converts Quill-editor HTML (see DMSRichTextEdit on the frontend) into the same
// List<OpenXmlElement> shape DocumentComponent.MergeDocumentTemplateAsync already splices into a
// Word template at {{DocumentContent}} for uploaded-file content -- letting the merge use either
// source through the same insertion code. Deliberately scoped to what Quill's own toolbar can
// produce, not general HTML: paragraphs, headings, bold/italic/underline/strike, text
// color/highlight, alignment, blockquotes, simple tables, and ordered/unordered lists (rendered
// as plain "1. "/"- " prefixed paragraphs, not native Word list numbering -- a real numbered list
// needs a numbering.xml part with matching AbstractNum/Num definitions, which the arbitrary
// uploaded template can't be assumed to have). Known gaps: embedded images (Quill can produce
// base64 <img> tags; wiring those into a package's image relationships is real additional work,
// not attempted here) and true hyperlink relationships (link text renders styled but isn't
// clickable). Anything unrecognised falls back to being read as plain text so content is never
// silently dropped, just under-formatted.
public static class HtmlToOpenXmlConverter
{
    public static List<OpenXmlElement> Convert(string? html)
    {
        var elements = new List<OpenXmlElement>();
        if (string.IsNullOrWhiteSpace(html))
        {
            elements.Add(new Paragraph());
            return elements;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var root = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
        foreach (var node in root.ChildNodes)
            elements.AddRange(ConvertBlock(node));

        if (elements.Count == 0)
            elements.Add(new Paragraph());

        return elements;
    }

    private static IEnumerable<OpenXmlElement> ConvertBlock(HtmlNode node)
    {
        switch (node.Name.ToLowerInvariant())
        {
            case "p":
            case "div":
                yield return ConvertParagraph(node);
                break;

            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
                yield return ConvertParagraph(node, headingLevel: int.Parse(node.Name.Substring(1)));
                break;

            case "blockquote":
                yield return ConvertParagraph(node, indent: true);
                break;

            case "ul":
                foreach (var li in node.ChildNodes.Where(n => n.Name.Equals("li", StringComparison.OrdinalIgnoreCase)))
                    yield return ConvertParagraph(li, listPrefix: "- ");
                break;

            case "ol":
                int i = 1;
                foreach (var li in node.ChildNodes.Where(n => n.Name.Equals("li", StringComparison.OrdinalIgnoreCase)))
                    yield return ConvertParagraph(li, listPrefix: $"{i++}. ");
                break;

            case "table":
                yield return ConvertTable(node);
                break;

            case "br":
                yield return new Paragraph();
                break;

            case "#text":
                if (!string.IsNullOrWhiteSpace(node.InnerText))
                    yield return ConvertParagraph(node);
                break;

            case "#comment":
                break;

            default:
                // Unrecognised wrapper (Quill sometimes emits plain <div>/<span> containers) --
                // recurse so its content is still captured rather than silently dropped.
                foreach (var child in node.ChildNodes)
                    foreach (var el in ConvertBlock(child))
                        yield return el;
                break;
        }
    }

    private static Paragraph ConvertParagraph(HtmlNode node, int headingLevel = 0, bool indent = false, string? listPrefix = null)
    {
        var paragraph = new Paragraph();
        var pPr = new ParagraphProperties();

        var align = GetAlignment(node);
        if (align != null)
            pPr.Justification = new Justification { Val = align };

        if (indent || headingLevel > 0)
            pPr.Indentation = new Indentation { Left = indent ? "720" : "0" };

        if (pPr.HasChildren)
            paragraph.ParagraphProperties = pPr;

        if (!string.IsNullOrEmpty(listPrefix))
            paragraph.AppendChild(new Run(new Text(listPrefix) { Space = SpaceProcessingModeValues.Preserve }));

        bool boldHeading = headingLevel > 0;
        uint headingSize = headingLevel switch
        {
            1 => 32u, // half-points: 16pt
            2 => 28u, // 14pt
            3 => 26u, // 13pt
            _ => 24u, // 12pt for h4-h6
        };

        foreach (var run in ConvertInline(node, forceBold: boldHeading, forceSize: boldHeading ? headingSize : null))
            paragraph.AppendChild(run);

        return paragraph;
    }

    // Walks inline descendants (text + formatting tags), emitting one Run per text node with the
    // formatting accumulated from its ancestor chain within this block.
    private static IEnumerable<Run> ConvertInline(HtmlNode node, bool bold = false, bool italic = false,
        bool underline = false, bool strike = false, string? color = null, string? highlight = null,
        bool forceBold = false, uint? forceSize = null)
    {
        foreach (var child in node.ChildNodes)
        {
            switch (child.Name.ToLowerInvariant())
            {
                case "#text":
                    var text = HtmlEntity.DeEntitize(child.InnerText);
                    if (string.IsNullOrEmpty(text)) continue;

                    var run = new Run();
                    var rPr = new RunProperties();
                    if (bold || forceBold) rPr.Bold = new Bold();
                    if (italic) rPr.Italic = new Italic();
                    if (underline) rPr.Underline = new Underline { Val = UnderlineValues.Single };
                    if (strike) rPr.Strike = new Strike();
                    if (color != null) rPr.Color = new Color { Val = color };
                    if (highlight != null) rPr.Shading = new Shading { Fill = highlight };
                    if (forceSize.HasValue)
                    {
                        rPr.FontSize = new FontSize { Val = forceSize.Value.ToString() };
                        rPr.FontSizeComplexScript = new FontSizeComplexScript { Val = forceSize.Value.ToString() };
                    }
                    if (rPr.HasChildren) run.RunProperties = rPr;

                    run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
                    yield return run;
                    break;

                case "br":
                    yield return new Run(new Break());
                    break;

                case "strong":
                case "b":
                    foreach (var r in ConvertInline(child, true, italic, underline, strike, color, highlight, forceBold, forceSize)) yield return r;
                    break;

                case "em":
                case "i":
                    foreach (var r in ConvertInline(child, bold, true, underline, strike, color, highlight, forceBold, forceSize)) yield return r;
                    break;

                case "u":
                    foreach (var r in ConvertInline(child, bold, italic, true, strike, color, highlight, forceBold, forceSize)) yield return r;
                    break;

                case "s":
                case "strike":
                case "del":
                    foreach (var r in ConvertInline(child, bold, italic, underline, true, color, highlight, forceBold, forceSize)) yield return r;
                    break;

                case "a":
                    // No relationship-backed hyperlink (see file header) -- rendered as styled
                    // (underlined) text so the link target text is at least still visible.
                    foreach (var r in ConvertInline(child, bold, italic, true, strike, color, highlight, forceBold, forceSize)) yield return r;
                    break;

                case "span":
                    var (spanColor, spanHighlight) = GetInlineColors(child);
                    foreach (var r in ConvertInline(child, bold, italic, underline, strike, spanColor ?? color, spanHighlight ?? highlight, forceBold, forceSize)) yield return r;
                    break;

                default:
                    foreach (var r in ConvertInline(child, bold, italic, underline, strike, color, highlight, forceBold, forceSize)) yield return r;
                    break;
            }
        }
    }

    private static JustificationValues? GetAlignment(HtmlNode node)
    {
        var style = node.GetAttributeValue("style", "");
        var cls = node.GetAttributeValue("class", "");

        if (style.Contains("center", StringComparison.OrdinalIgnoreCase) || cls.Contains("ql-align-center"))
            return JustificationValues.Center;
        if (style.Contains("right", StringComparison.OrdinalIgnoreCase) || cls.Contains("ql-align-right"))
            return JustificationValues.Right;
        if (style.Contains("justify", StringComparison.OrdinalIgnoreCase) || cls.Contains("ql-align-justify"))
            return JustificationValues.Both;
        return null;
    }

    private static readonly Regex ColorRegex = new(@"color:\s*(#[0-9a-fA-F]{3,6})", RegexOptions.Compiled);
    private static readonly Regex BackgroundRegex = new(@"background(?:-color)?:\s*(#[0-9a-fA-F]{3,6})", RegexOptions.Compiled);

    private static (string? color, string? highlight) GetInlineColors(HtmlNode node)
    {
        var style = node.GetAttributeValue("style", "");
        if (string.IsNullOrEmpty(style)) return (null, null);

        var colorMatch = ColorRegex.Match(style);
        var bgMatch = BackgroundRegex.Match(style);

        string? color = colorMatch.Success ? colorMatch.Groups[1].Value.TrimStart('#') : null;
        string? highlight = bgMatch.Success ? bgMatch.Groups[1].Value.TrimStart('#') : null;
        return (color, highlight);
    }

    private static Table ConvertTable(HtmlNode tableNode)
    {
        var table = new Table();
        table.AppendChild(new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }
            ),
            new TableWidth { Type = TableWidthUnitValues.Auto }
        ));

        var rows = tableNode.Descendants("tr");
        foreach (var rowNode in rows)
        {
            var row = new TableRow();
            var cells = rowNode.ChildNodes.Where(n =>
                n.Name.Equals("td", StringComparison.OrdinalIgnoreCase) ||
                n.Name.Equals("th", StringComparison.OrdinalIgnoreCase));

            foreach (var cellNode in cells)
            {
                var cell = new TableCell();
                cell.AppendChild(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Auto }));

                var cellParagraph = new Paragraph();
                foreach (var run in ConvertInline(cellNode))
                    cellParagraph.AppendChild(run);
                if (!cellParagraph.HasChildren)
                    cellParagraph.AppendChild(new Run(new Text("")));

                cell.AppendChild(cellParagraph);
                row.AppendChild(cell);
            }

            if (row.HasChildren)
                table.AppendChild(row);
        }

        return table;
    }
}
