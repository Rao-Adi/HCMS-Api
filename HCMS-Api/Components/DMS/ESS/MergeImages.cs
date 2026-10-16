using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace HCMS_Api.Components.DMS.ESS;

/// <summary>
/// The pictures that travel with content spliced into a Word template.
///
/// Content reaches the template in one of two forms, and both used to lose every picture:
///
///   * An uploaded .docx: its body elements were cloned into the template, but a picture in a Word body is only
///     a reference ("relationship id") to an image stored in the SOURCE file's package. Cloned into another
///     package that id points at nothing, so Word shows no picture. The image parts have to be copied across
///     and the ids rewritten.
///   * The rich-text editor's HTML: an &lt;img&gt; (a base64 data URI) was not converted at all.
///
/// Both come down to the same thing -- images to be added to the template's main document part, and drawings whose
/// reference is then pointed at them -- so both go through this one class: collect the images, then attach them to
/// the template once it is open.
/// </summary>
public sealed class MergeImageSet
{
    private sealed record Image(byte[] Bytes, string ContentType);

    private readonly Dictionary<string, Image> _images = new();
    private int _nextHtmlKey;

    // Keeps the drawing ids of merged content clear of the signature images (which start at 1) and anything the
    // template already carries -- OOXML wants each drawing's id unique across the document.
    private const uint FirstDrawingId = 10_000;

    private const long EmuPerPixel = 9525; // at 96 dpi
    private const int MaxWidthPixels = 600; // about 6.25 inches: what fits a normal page's text column
    private const int DefaultWidthPixels = 300;
    private const int DefaultHeightPixels = 200;

    private static readonly Regex DataUri = new(
        @"^data:(?<type>image/[A-Za-z0-9.+\-]+);base64,(?<data>.+)$",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    // ------------------------------------------------------------------ HTML source

    /// <summary>
    /// Registers the image an &lt;img src&gt; points at and returns the Run that shows it, or null when the source
    /// cannot be read (so the caller simply leaves that picture out rather than failing the whole document).
    /// </summary>
    public Run? CreateRunForHtmlImage(string? src, int? widthPx, int? heightPx)
    {
        var image = ReadHtmlImage(src);
        if (image == null)
            return null;

        var key = "html:" + (_nextHtmlKey++);
        _images[key] = image;

        var (cx, cy) = ScaleToEmu(image, widthPx, heightPx);

        // The id is renumbered when the image is attached; the relationship id is the key until then.
        var drawing = new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = FirstDrawingId, Name = "Picture" },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = "Picture" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = key },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = cx, Cy = cy }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
                    )
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
            )
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U,
            });

        return new Run(drawing);
    }

    private static Image? ReadHtmlImage(string? src)
    {
        if (string.IsNullOrWhiteSpace(src))
            return null;

        var match = DataUri.Match(src.Trim());
        if (match.Success)
        {
            try
            {
                return new Image(Convert.FromBase64String(match.Groups["data"].Value), match.Groups["type"].Value.ToLowerInvariant());
            }
            catch (FormatException)
            {
                return null;
            }
        }

        return null;
    }

    // ------------------------------------------------------------------ Word (.docx) source

    /// <summary>
    /// Reads the pictures the given (already cloned) body elements refer to out of the SOURCE document, which must
    /// still be open. Call before that document is disposed.
    /// </summary>
    public void CollectFrom(MainDocumentPart source, IEnumerable<OpenXmlElement> elements)
    {
        foreach (var element in elements)
        {
            foreach (var blip in element.Descendants<A.Blip>())
                Collect(source, blip.Embed?.Value);

            // Older documents draw pictures as VML.
            foreach (var imageData in element.Descendants<DocumentFormat.OpenXml.Vml.ImageData>())
                Collect(source, imageData.RelationshipId?.Value);
        }
    }

    private void Collect(MainDocumentPart source, string? relationshipId)
    {
        if (string.IsNullOrEmpty(relationshipId) || _images.ContainsKey(relationshipId))
            return;

        try
        {
            if (source.GetPartById(relationshipId) is not ImagePart part)
                return;

            using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);

            _images[relationshipId] = new Image(
                buffer.ToArray(),
                string.IsNullOrWhiteSpace(part.ContentType) ? "image/png" : part.ContentType);
        }
        catch (ArgumentOutOfRangeException)
        {
            // A reference to a part that is not there (a damaged source) -- that picture stays missing.
        }
    }

    // ------------------------------------------------------------------ Into the template

    /// <summary>
    /// Adds the collected images to the template's main document part and points the elements' drawings at them.
    /// Call before the elements are inserted into the template body (or after -- they are the same objects).
    /// </summary>
    public void AttachTo(MainDocumentPart target, IEnumerable<OpenXmlElement> elements)
    {
        var elementList = elements.ToList();

        // One image part per distinct source image, however many times it is used.
        var attached = new Dictionary<string, string>();

        string? RelationshipFor(string? key)
        {
            if (string.IsNullOrEmpty(key) || !_images.TryGetValue(key, out var image))
                return null;

            if (attached.TryGetValue(key, out var existing))
                return existing;

            var part = target.AddNewPart<ImagePart>(image.ContentType);
            using (var ms = new MemoryStream(image.Bytes))
                part.FeedData(ms);

            var id = target.GetIdOfPart(part);
            attached[key] = id;
            return id;
        }

        uint nextDrawingId = FirstDrawingId;

        foreach (var element in elementList)
        {
            foreach (var blip in element.Descendants<A.Blip>().ToList())
            {
                var id = RelationshipFor(blip.Embed?.Value);
                if (id != null)
                    blip.Embed = id;
            }

            foreach (var imageData in element.Descendants<DocumentFormat.OpenXml.Vml.ImageData>().ToList())
            {
                var id = RelationshipFor(imageData.RelationshipId?.Value);
                if (id != null)
                    imageData.RelationshipId = id;
            }

            // Drawings brought across from another file keep the ids they had there, which can repeat the
            // template's own or each other's.
            foreach (var properties in element.Descendants<DW.DocProperties>())
                properties.Id = nextDrawingId++;
        }
    }

    // ------------------------------------------------------------------ Sizes

    private static (long Cx, long Cy) ScaleToEmu(Image image, int? widthPx, int? heightPx)
    {
        var (naturalWidth, naturalHeight) = ReadPixelSize(image.Bytes) ?? (DefaultWidthPixels, DefaultHeightPixels);

        double width = widthPx is > 0 ? widthPx.Value : naturalWidth;
        double height = heightPx is > 0 ? heightPx.Value : naturalHeight;

        // Only one of the two given: keep the picture's own proportions.
        if (widthPx is > 0 && !(heightPx is > 0))
            height = width * naturalHeight / Math.Max(1, naturalWidth);
        else if (heightPx is > 0 && !(widthPx is > 0))
            width = height * naturalWidth / Math.Max(1, naturalHeight);

        if (width > MaxWidthPixels)
        {
            height = height * MaxWidthPixels / width;
            width = MaxWidthPixels;
        }

        return ((long)Math.Max(1, width) * EmuPerPixel, (long)Math.Max(1, height) * EmuPerPixel);
    }

    /// <summary>Pixel size of a PNG, JPEG, GIF or BMP, read from its header; null for anything else.</summary>
    private static (int Width, int Height)? ReadPixelSize(byte[] b)
    {
        try
        {
            // PNG
            if (b.Length > 24 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
                return (ReadInt32BigEndian(b, 16), ReadInt32BigEndian(b, 20));

            // GIF
            if (b.Length > 10 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46)
                return (b[6] | (b[7] << 8), b[8] | (b[9] << 8));

            // BMP
            if (b.Length > 26 && b[0] == 0x42 && b[1] == 0x4D)
                return (BitConverter.ToInt32(b, 18), Math.Abs(BitConverter.ToInt32(b, 22)));

            // JPEG: walk the segments to a start-of-frame marker
            if (b.Length > 4 && b[0] == 0xFF && b[1] == 0xD8)
            {
                int i = 2;
                while (i + 9 < b.Length)
                {
                    if (b[i] != 0xFF) { i++; continue; }
                    var marker = b[i + 1];
                    if (marker == 0xFF) { i++; continue; }
                    if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                        return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
                    i += 2 + ((b[i + 2] << 8) | b[i + 3]);
                }
            }
        }
        catch (IndexOutOfRangeException)
        {
        }

        return null;
    }

    private static int ReadInt32BigEndian(byte[] b, int offset) =>
        (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];
}
