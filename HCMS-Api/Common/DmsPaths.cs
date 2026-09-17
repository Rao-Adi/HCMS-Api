using Microsoft.AspNetCore.Hosting;

namespace HCMS_Api.Common;

/// <summary>
/// Resolves the folder that actually backs the URLs this app serves files from.
///
/// Everything under wwwroot/uploads is written by an upload and read back by a download or a
/// template merge, and both sides have to agree on where that folder is. They used to agree only
/// by accident: each call site built the path as
/// <c>Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", ...)</c>, and the process's current
/// directory equals the app's content root only when it is started from its own folder -- which is
/// what happens under <c>dotnet run</c> and Visual Studio, and is why this always worked in
/// development.
///
/// Hosted under IIS or as a Windows Service, the current directory is something else entirely
/// (commonly C:\Windows\System32). Uploads then land in a wwwroot folder next to *that*, while
/// <c>app.UseStaticFiles()</c> keeps serving from the real one -- so a file that uploaded
/// "successfully" 404s on download, and an endpoint that checks File.Exists first reports
/// "Physical file does not exist on the server" for a file sitting right there in wwwroot.
///
/// WebRootPath is the framework's own answer to the same question and is correct under every
/// hosting model, so it is preferred; the fallbacks below only matter if this is ever called
/// before the host is built.
/// </summary>
public static class DmsPaths
{
    /// <summary>Absolute path of the served wwwroot folder.</summary>
    public static string WebRoot
    {
        get
        {
            try
            {
                var env = ServiceLocator.GetService<IWebHostEnvironment>();

                if (!string.IsNullOrWhiteSpace(env?.WebRootPath))
                    return env.WebRootPath;

                if (!string.IsNullOrWhiteSpace(env?.ContentRootPath))
                    return Path.Combine(env.ContentRootPath, "wwwroot");
            }
            catch
            {
                // Called before ServiceLocator.Initialize, or outside the host entirely.
                // Fall through to the directory-based guesses below.
            }

            // AppContext.BaseDirectory is where this assembly was loaded from, which under every
            // normal deployment IS the content root -- a better guess than the current directory.
            var baseDirectoryCandidate = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            if (Directory.Exists(baseDirectoryCandidate))
                return baseDirectoryCandidate;

            return Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }
    }

    /// <summary>
    /// Builds a path under wwwroot. Pass the segments, not a pre-joined string, so this behaves
    /// the same on every platform: <c>WebRootCombine("uploads", "templates")</c>.
    /// </summary>
    public static string WebRootCombine(params string[] segments)
    {
        if (segments == null || segments.Length == 0)
            return WebRoot;

        var parts = new string[segments.Length + 1];
        parts[0] = WebRoot;
        Array.Copy(segments, 0, parts, 1, segments.Length);
        return Path.Combine(parts);
    }

    /// <summary>
    /// Turns a stored relative URL ("/uploads/templates/Foo.docx") into the absolute path of the
    /// file on disk. Leading slashes and forward slashes are normalised, so the same stored value
    /// works whether it was saved with or without them.
    /// </summary>
    public static string ResolveStoredFilePath(string? storedUrl)
    {
        if (string.IsNullOrWhiteSpace(storedUrl))
            return string.Empty;

        var relative = storedUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(WebRoot, relative);
    }
}
