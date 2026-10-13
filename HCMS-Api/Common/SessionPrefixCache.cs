namespace HCMS_Api.Common;

/// <summary>
/// Remembers, for a short while, which session key (the "prefix" every Redis session entry is stored
/// under) belongs to a browser's "login" header.
///
/// That lookup is a query against the Security SQL Server, and almost every request makes it several
/// times over -- once for each of company, employee and so on. Right after login the screen fires a dozen
/// or more calls at once, so the Security database was being hit dozens of times in a few seconds. When it
/// answered slowly the lookup came back empty (the query helper swallows the error), and an empty prefix
/// means "no session": an empty company id that crashed with "The input string '' was not in a correct
/// format", or a 401 session timeout. Only a successful lookup is remembered, so a miss is never cached.
///
/// The entry is short-lived on purpose, and is dropped when that login is renewed or ended.
/// </summary>
public static class SessionPrefixCache
{
    private static readonly PositiveLookupCache Cache = new(TimeSpan.FromSeconds(60));

    public static string GetOrLookup(string loginKey, Func<string> lookup) => Cache.GetOrLookup(loginKey, lookup);

    /// <summary>A new login was recorded for this "login" value.</summary>
    public static void Invalidate(string loginKey) => Cache.Invalidate(loginKey);

    /// <summary>That session was ended (logout): forget every login value that pointed at it.</summary>
    public static void InvalidateByPrefix(string prefix) => Cache.InvalidateByValue(prefix);
}

/// <summary>
/// An employee's code by employee id. It is read from the remote HCMS SQL Server on nearly every request
/// (it is the "CreatedBy"/"approver" identity), and that helper swallows errors -- so a slow answer came back
/// as a blank code and a record was saved with a blank CreatedBy. An employee's code does not change, so a
/// successful answer is kept for a while.
/// </summary>
public static class EmployeeCodeCache
{
    private static readonly PositiveLookupCache Cache = new(TimeSpan.FromMinutes(10));

    public static string GetOrLookup(string empId, Func<string> lookup) => Cache.GetOrLookup(empId, lookup);
}

/// <summary>
/// An employee id worked out for a session whose Redis "OtherData" entry is missing, by session prefix.
/// See DMSUtilities.GetEmpid.
/// </summary>
public static class SessionEmpIdCache
{
    private static readonly PositiveLookupCache Cache = new(TimeSpan.FromMinutes(10));

    public static string GetOrLookup(string prefix, Func<string> lookup) => Cache.GetOrLookup(prefix, lookup);
}
