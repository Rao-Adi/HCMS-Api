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
// uploaded template can't be assumed to have). Embedded images (Quill's base64 <img> tags) become inline
// pictures: the converter only registers each image with the MergeImageSet it is given, and the merge adds
// them to the template's package afterwards (the converter has no package to add them to). Known gap: true
// hyperlink relationships (link text renders styled but isn't clickable). Anything unrecognised falls back to
// being read as plain text so content is never silently dropped, just under-formatted.
public static class HtmlToOpenXmlConverter
{
    // The image set of the conversion in progress. The conversion is synchronous and runs start to finish on one
    // thread, so a thread-static avoids threading a parameter through every method below.
    [ThreadStatic]
    private static MergeImageSet? _images;

    public static List<OpenXmlElement> Convert(string? html, MergeImageSet? images = null)
    {
        var elements = new List<OpenXmlElement>();
        if (string.IsNullOrWhiteSpace(html))
        {
            elements.Add(new Paragraph());
            return elements;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        _images = images;
        try
        {
            var root = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
            foreach (var node in root.ChildNodes)
                elements.AddRange(ConvertBlock(node));
        }
        finally
        {
            _images = null;
        }

        if (elements.Count == 0)
            elements.Add(new Paragraph());

        return elements;
    }

    // Width / height an <img> asks for, from its attributes or its inline style (pixels).
    private static int? ImageSize(HtmlNode img, string name)
    {
        var attribute = img.GetAttributeValue(name, "");
        if (int.TryParse(attribute.Replace("px", "").Trim(), out var fromAttribute) && fromAttribute > 0)
            return fromAttribute;

        var match = Regex.Match(img.GetAttributeValue("style", ""), name + @":\s*(\d+(?:\.\d+)?)px", RegexOptions.IgnoreCase);
        return match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var fromStyle) && fromStyle > 0
            ? (int)Math.Round(fromStyle)
            : null;
    }

    private static Run? ConvertImage(HtmlNode img) =>
        _images?.CreateRunForHtmlImage(img.GetAttributeValue("src", ""), ImageSize(img, "width"), ImageSize(img, "height"));

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
            case "ol":
                foreach (var item in ConvertList(node))
                    yield return item;
                break;

            case "table":
                yield return ConvertTable(node);
                break;

            case "br":
                yield return new Paragraph();
                break;

            case "img":
                // A picture sitting directly in the body, not inside a paragraph.
                var blockImage = ConvertImage(node);
                if (blockImage != null)
                    yield return new Paragraph(blockImage);
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

    // A list as the editor writes it. Quill 2 puts EVERY list item -- bulleted or numbered, at any depth -- in one <ol>, and says
    // which it is on the item: data-list="bullet" | "ordered" | "checked" | "unchecked", with the depth as a ql-indent-N class.
    // (A plain <ul>/<ol> from older content or a paste has no such attributes, so the tag decides.) Reading the tag alone turned
    // every bullet into "1. 2. 3." and flattened every nested item to the top level.
    private static IEnumerable<Paragraph> ConvertList(HtmlNode list)
    {
        var tagIsBullet = list.Name.Equals("ul", StringComparison.OrdinalIgnoreCase);
        var counters = new Dictionary<int, int>();
        var types = new Dictionary<int, string>();

        foreach (var li in list.ChildNodes.Where(n => n.Name.Equals("li", StringComparison.OrdinalIgnoreCase)))
        {
            var dataList = li.GetAttributeValue("data-list", "");
            var kind = dataList.Length > 0 ? dataList.ToLowerInvariant() : (tagIsBullet ? "bullet" : "ordered");
            var level = 0;
            var indentMatch = Regex.Match(li.GetAttributeValue("class", ""), @"ql-indent-(\d+)");
            if (indentMatch.Success) level = Math.Min(int.Parse(indentMatch.Groups[1].Value), 8);

            // Going back up, or switching between bullets and numbers at a level, starts that level (and every deeper one) over.
            foreach (var deeper in counters.Keys.Where(k => k > level).ToList()) { counters.Remove(deeper); types.Remove(deeper); }
            var ordered = kind == "ordered";
            if (types.TryGetValue(level, out var previous) && previous != (ordered ? "ordered" : "bullet")) counters.Remove(level);
            types[level] = ordered ? "ordered" : "bullet";

            string prefix;
            if (ordered)
            {
                counters[level] = counters.TryGetValue(level, out var n) ? n + 1 : 1;
                prefix = OrderedMarker(counters[level], level);
            }
            else
            {
                prefix = kind == "checked" ? "☑ " : kind == "unchecked" ? "☐ " : (level % 2 == 0 ? "• " : "◦ ");
            }

            yield return ConvertParagraph(li, listPrefix: prefix, listLevel: level);
        }
    }

    // 1. / a. / i. by depth, as Word's own outline numbering does.
    private static string OrderedMarker(int n, int level)
    {
        switch (level % 3)
        {
            case 1:
                var letters = "";
                for (var v = n; v > 0; v = (v - 1) / 26) letters = (char)('a' + (v - 1) % 26) + letters;
                return letters + ". ";
            case 2:
                var romans = new[] { (1000, "m"), (900, "cm"), (500, "d"), (400, "cd"), (100, "c"), (90, "xc"), (50, "l"), (40, "xl"), (10, "x"), (9, "ix"), (5, "v"), (4, "iv"), (1, "i") };
                var sb = new System.Text.StringBuilder();
                var rest = n;
                foreach (var (value, symbol) in romans) while (rest >= value) { sb.Append(symbol); rest -= value; }
                return sb + ". ";
            default:
                return n + ". ";
        }
    }

    private static Paragraph ConvertParagraph(HtmlNode node, int headingLevel = 0, bool indent = false, string? listPrefix = null, int listLevel = 0)
    {
        var paragraph = new Paragraph();
        var pPr = new ParagraphProperties();

        var align = GetAlignment(node);
        if (align != null)
            pPr.Justification = new Justification { Val = align };

        if (indent || headingLevel > 0 || listLevel > 0)
            pPr.Indentation = new Indentation { Left = indent ? "720" : (listLevel > 0 ? (listLevel * 720).ToString() : "0") };

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
        bool forceBold = false, uint? forceSize = null, uint? size = null, VerticalPositionValues? vPos = null)
    {
        foreach (var child in node.ChildNodes)
        {
            // Whatever formatting this element carries itself (colour, background, size from its style / Quill class) is
            // added to what its ancestors already gave. Quill puts it on a <span>, but a pasted or older document can
            // carry it on any element, so every element is read, not just <span>.
            var (ownColor, ownHighlight, ownSize) = child.NodeType == HtmlNodeType.Element ? GetInlineFormatting(child) : (null, null, null);
            var cColor = ownColor ?? color;
            var cHighlight = ownHighlight ?? highlight;
            var cSize = ownSize ?? size;
            var childName = child.Name.ToLowerInvariant();
            VerticalPositionValues? cVPos = childName == "sub" ? VerticalPositionValues.Subscript
                : childName == "sup" ? VerticalPositionValues.Superscript : vPos;

            switch (childName)
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
                    // w:val is required on a shading element; without it Word ignores the whole thing, so the background
                    // the writer picked never showed.
                    if (highlight != null) rPr.Shading = new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = highlight };
                    var runSize = forceSize ?? size;
                    if (runSize.HasValue)
                    {
                        rPr.FontSize = new FontSize { Val = runSize.Value.ToString() };
                        rPr.FontSizeComplexScript = new FontSizeComplexScript { Val = runSize.Value.ToString() };
                    }
                    if (vPos.HasValue) rPr.VerticalTextAlignment = new VerticalTextAlignment { Val = vPos.Value };
                    if (rPr.HasChildren) run.RunProperties = rPr;

                    run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
                    yield return run;
                    break;

                case "br":
                    yield return new Run(new Break());
                    break;

                case "img":
                    var imageRun = ConvertImage(child);
                    if (imageRun != null)
                        yield return imageRun;
                    break;

                case "strong":
                case "b":
                    foreach (var r in ConvertInline(child, true, italic, underline, strike, cColor, cHighlight, forceBold, forceSize, cSize, cVPos)) yield return r;
                    break;

                case "em":
                case "i":
                    foreach (var r in ConvertInline(child, bold, true, underline, strike, cColor, cHighlight, forceBold, forceSize, cSize, cVPos)) yield return r;
                    break;

                case "u":
                    foreach (var r in ConvertInline(child, bold, italic, true, strike, cColor, cHighlight, forceBold, forceSize, cSize, cVPos)) yield return r;
                    break;

                case "s":
                case "strike":
                case "del":
                    foreach (var r in ConvertInline(child, bold, italic, underline, true, cColor, cHighlight, forceBold, forceSize, cSize, cVPos)) yield return r;
                    break;

                case "a":
                    // No relationship-backed hyperlink (see file header) -- rendered as styled
                    // (underlined) text so the link target text is at least still visible.
                    foreach (var r in ConvertInline(child, bold, italic, true, strike, cColor, cHighlight, forceBold, forceSize, cSize, cVPos)) yield return r;
                    break;

                default:
                    // <span>, <sub>, <sup>, <mark> and anything else: the formatting it carries was merged above.
                    foreach (var r in ConvertInline(child, bold, italic, underline, strike, cColor, cHighlight, forceBold, forceSize, cSize, cVPos)) yield return r;
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

    // "color:" must not be read out of "background-color:", and the value is whatever the editor wrote: Quill writes
    // rgb(230, 0, 0), a paste can bring #f00 or a colour name. Only #rrggbb used to be understood, so the colour (and the
    // background) the writer picked was dropped for everything the editor actually produces.
    private static readonly Regex ColorRegex = new(@"(?<![\w-])color\s*:\s*([^;]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BackgroundRegex = new(@"(?<![\w-])background(?:-color)?\s*:\s*([^;]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FontSizeRegex = new(@"(?<![\w-])font-size\s*:\s*([\d.]+)\s*(px|pt|em|rem|%)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RgbRegex = new(@"rgba?\(\s*(\d{1,3})\s*[, ]\s*(\d{1,3})\s*[, ]\s*(\d{1,3})\s*(?:[,/]\s*([\d.]+%?))?\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, string> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "000000", ["white"] = "FFFFFF", ["red"] = "FF0000", ["green"] = "008000", ["blue"] = "0000FF",
        ["yellow"] = "FFFF00", ["orange"] = "FFA500", ["purple"] = "800080", ["gray"] = "808080", ["grey"] = "808080",
        ["navy"] = "000080", ["teal"] = "008080", ["maroon"] = "800000", ["olive"] = "808000", ["silver"] = "C0C0C0",
        ["lime"] = "00FF00", ["aqua"] = "00FFFF", ["cyan"] = "00FFFF", ["fuchsia"] = "FF00FF", ["magenta"] = "FF00FF",
        ["brown"] = "A52A2A", ["pink"] = "FFC0CB",
    };

    // A CSS colour as the six hex digits Word wants, or null when it is not a colour Word can show (transparent, inherit,
    // an unknown name).
    private static string? ParseCssColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim().TrimEnd('!', ' ').Replace("!important", "", StringComparison.OrdinalIgnoreCase).Trim();

        var hex = Regex.Match(value, @"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$");
        if (hex.Success)
        {
            var h = hex.Groups[1].Value;
            if (h.Length == 3) h = string.Concat(h.Select(c => new string(c, 2)));
            return h.ToUpperInvariant();
        }

        var rgb = RgbRegex.Match(value);
        if (rgb.Success)
        {
            var alpha = rgb.Groups[4].Value;
            if (alpha.Length > 0 && double.TryParse(alpha.TrimEnd('%'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var a)
                && (alpha.EndsWith("%") ? a : a * 100) < 1) return null; // fully transparent: nothing to show
            string Two(int g) => Math.Clamp(int.Parse(rgb.Groups[g].Value), 0, 255).ToString("X2");
            return Two(1) + Two(2) + Two(3);
        }

        return NamedColors.TryGetValue(value, out var named) ? named : null;
    }

    // Quill's own size classes are relative to the normal size (0.75em / 1.5em / 2.5em of an 11pt body); returned in
    // half-points, which is what Word's size property counts in.
    private static uint? ParseFontSize(string style, string cls)
    {
        if (cls.Contains("ql-size-small")) return 17;
        if (cls.Contains("ql-size-large")) return 33;
        if (cls.Contains("ql-size-huge")) return 55;

        var m = FontSizeRegex.Match(style ?? string.Empty);
        if (!m.Success || !double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) || n <= 0)
            return null;

        double points = m.Groups[2].Value.ToLowerInvariant() switch
        {
            "pt" => n,
            "em" or "rem" => n * 11,
            "%" => n / 100 * 11,
            _ => n * 0.75, // px (and a bare number)
        };
        var half = (uint)Math.Round(points * 2);
        return half is >= 2 and <= 400 ? half : null; // Word accepts 1 to 1638 pt; anything outside a sane range is a typo
    }

    private static (string? color, string? highlight, uint? size) GetInlineFormatting(HtmlNode node)
    {
        var style = node.GetAttributeValue("style", "");
        var cls = node.GetAttributeValue("class", "");

        string? color = null, highlight = null;
        if (!string.IsNullOrEmpty(style))
        {
            var colorMatch = ColorRegex.Match(style);
            var bgMatch = BackgroundRegex.Match(style);
            color = colorMatch.Success ? ParseCssColor(colorMatch.Groups[1].Value) : null;
            highlight = bgMatch.Success ? ParseCssColor(bgMatch.Groups[1].Value) : null;
        }
        if (node.Name.Equals("mark", StringComparison.OrdinalIgnoreCase) && highlight == null) highlight = "FFFF00";

        return (color, highlight, ParseFontSize(style, cls));
    }

    // Word's own default cell margin (0.08" = 108 twentieths of a point). Used for both the cell
    // margin and the table indent so the two always cancel -- see ConvertTable.
    private const short DefaultCellMarginTwips = 108;

    // The border look of a table from an uploaded Word file travels in the data-row of its cells ("dxh-3-0": h = horizontal lines only,
    // v = vertical only, n = none). Anything else is the full grid, as before.
    private static TableBorders BuildTableBorders(HtmlNode tableNode)
    {
        var id = tableNode.Descendants().FirstOrDefault(n =>
            n.Name.Equals("td", StringComparison.OrdinalIgnoreCase) || n.Name.Equals("th", StringComparison.OrdinalIgnoreCase))
            ?.GetAttributeValue("data-row", "") ?? "";
        var look = Regex.Match(id, @"^dx([hvn])-\d+-\d+$");
        var mode = look.Success ? look.Groups[1].Value : "";

        BorderType Line<T>(bool on) where T : BorderType, new() => new T
        {
            Val = on ? BorderValues.Single : BorderValues.Nil,
            Size = on ? 4u : 0u,
            Space = 0,
            Color = "auto",
        };

        var horizontal = mode == "" || mode == "h";
        var vertical = mode == "" || mode == "v";
        return new TableBorders(
            Line<TopBorder>(mode == ""),
            Line<LeftBorder>(vertical),
            Line<BottomBorder>(horizontal),
            Line<RightBorder>(vertical),
            Line<InsideHorizontalBorder>(horizontal),
            Line<InsideVerticalBorder>(vertical));
    }

    private static Table ConvertTable(HtmlNode tableNode)
    {
        var table = new Table();

        // Word positions a table by its cell BORDER, then insets each cell's text by the cell
        // margin -- so a table with no explicit indent has its text sitting CellMargin further
        // right than the body text, which reads as the whole table hanging out to the left of the
        // page's text column. Setting the table indent to the same value as the left cell margin
        // cancels that out, so cell text lines up with the paragraph text above and below it.
        // Browsers and Google Docs lay tables out from the text edge instead, which is why the
        // same HTML looks correctly aligned there and only drifts in Word.
        //
        // Both values are stated explicitly rather than left to Word's defaults, since the whole
        // point is that the indent must match the cell margin -- inheriting either one from a
        // template's table style would let them disagree again.
        // Child order follows the CT_TblPrBase schema sequence (tblW, tblInd, tblBorders,
        // tblCellMar); Word tolerates other orders, but OpenXML validation does not.
        table.AppendChild(new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Auto },
            new TableIndentation { Width = DefaultCellMarginTwips, Type = TableWidthUnitValues.Dxa },
            BuildTableBorders(tableNode),
            new TableCellMarginDefault(
                new TopMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                new TableCellLeftMargin { Width = DefaultCellMarginTwips, Type = TableWidthValues.Dxa },
                new BottomMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                new TableCellRightMargin { Width = DefaultCellMarginTwips, Type = TableWidthValues.Dxa }
            )
        ));

        // Rows of THIS table only (a table nested in a cell has its own). colspan -> GridSpan, rowspan -> vertical merge: without them
        // a merged cell shifted every cell after it one column to the left, so the values sat under the wrong headings.
        var rows = tableNode.Descendants("tr").Where(tr => ReferenceEquals(tr.Ancestors("table").FirstOrDefault(), tableNode)).ToList();
        var pendingRowSpan = new List<int>();   // per grid column: rows still to be covered by a cell above that spans down

        foreach (var rowNode in rows)
        {
            var row = new TableRow();
            var col = 0;

            // A cell above that spans down already occupies this column: write its continuation cell and move on.
            void FillSpannedColumns()
            {
                while (col < pendingRowSpan.Count && pendingRowSpan[col] > 0)
                {
                    var continuation = new TableCell(new TableCellProperties(
                        new TableCellWidth { Type = TableWidthUnitValues.Auto },
                        new VerticalMerge()));
                    continuation.AppendChild(new Paragraph());
                    row.AppendChild(continuation);
                    pendingRowSpan[col]--;
                    col++;
                }
            }

            var cells = rowNode.ChildNodes.Where(n =>
                n.Name.Equals("td", StringComparison.OrdinalIgnoreCase) ||
                n.Name.Equals("th", StringComparison.OrdinalIgnoreCase));

            foreach (var cellNode in cells)
            {
                FillSpannedColumns();

                var colSpan = Math.Clamp(ParseSpan(cellNode, "colspan"), 1, 50);
                var rowSpan = Math.Clamp(ParseSpan(cellNode, "rowspan"), 1, 200);

                var cellProps = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Auto });
                if (colSpan > 1) cellProps.AppendChild(new GridSpan { Val = colSpan });
                if (rowSpan > 1) cellProps.AppendChild(new VerticalMerge { Val = MergedCellValues.Restart });

                var cell = new TableCell();
                cell.AppendChild(cellProps);
                foreach (var block in ConvertCellContent(cellNode))
                    cell.AppendChild(block);

                row.AppendChild(cell);

                while (pendingRowSpan.Count < col + colSpan) pendingRowSpan.Add(0);
                for (var i = 0; i < colSpan; i++) pendingRowSpan[col + i] = rowSpan > 1 ? rowSpan - 1 : 0;
                col += colSpan;
            }

            FillSpannedColumns();   // spanned columns at the end of the row

            if (row.HasChildren)
                table.AppendChild(row);
        }

        return table;
    }

    private static int ParseSpan(HtmlNode cell, string name) =>
        int.TryParse(cell.GetAttributeValue(name, "1").Trim(), out var n) && n > 0 ? n : 1;

    private static readonly HashSet<string> CellBlockNames = new(StringComparer.OrdinalIgnoreCase)
        { "p", "div", "ul", "ol", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "table" };

    // What goes inside one table cell. A cell holding paragraphs, bullet/numbered lists or headings (as a pasted or uploaded Word table
    // does) keeps each of them as its own paragraph; before, everything in the cell was run together into ONE paragraph, so a
    // "Key Changes" cell with three bullets became one line with no bullets. A cell with only inline content is one paragraph, as before.
    private static List<OpenXmlElement> ConvertCellContent(HtmlNode cellNode)
    {
        var result = new List<OpenXmlElement>();
        if (!cellNode.ChildNodes.Any(n => CellBlockNames.Contains(n.Name)))
        {
            var single = new Paragraph();
            foreach (var run in ConvertInline(cellNode))
                single.AppendChild(run);
            if (!single.HasChildren)
                single.AppendChild(new Run(new Text("")));
            result.Add(single);
            return result;
        }

        // Inline pieces between the blocks (loose text, <br>) are gathered into a paragraph of their own.
        var loose = new HtmlNode(HtmlNodeType.Element, cellNode.OwnerDocument, -1) { Name = "span" };
        void FlushLoose()
        {
            if (loose.ChildNodes.Count == 0) return;
            if (!string.IsNullOrWhiteSpace(loose.InnerText) || loose.Descendants("img").Any())
            {
                var para = new Paragraph();
                foreach (var run in ConvertInline(loose)) para.AppendChild(run);
                result.Add(para);
            }
            loose = new HtmlNode(HtmlNodeType.Element, cellNode.OwnerDocument, -1) { Name = "span" };
        }

        foreach (var child in cellNode.ChildNodes)
        {
            if (CellBlockNames.Contains(child.Name))
            {
                FlushLoose();
                result.AddRange(ConvertBlock(child));
            }
            else
            {
                loose.AppendChild(child.CloneNode(true));
            }
        }
        FlushLoose();

        // A cell must end with a paragraph (also after a nested table), and must not be empty.
        if (result.Count == 0 || result[^1] is not Paragraph)
            result.Add(new Paragraph());
        return result;
    }
}
