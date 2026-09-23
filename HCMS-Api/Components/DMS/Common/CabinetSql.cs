namespace HCMS_Api.Components.DMS.Common;

/// <summary>
/// Writing cabinet codes into the hand-built SQL these setup screens use.
///
/// The cabinet is configurable: a client may file against a Division alone, a Division and
/// Department, all three levels, or all four including a Business Domain. Whatever the user does
/// not pick arrives as an empty string.
///
/// That matters because the cabinet tables are referenced by composite foreign keys, for example
///     fk_documents_subdepartment (companyid, departmentcode, subdepartmentcode)
///         -> subdepartments (companyid, departmentcode, code)
/// Postgres skips a MATCH SIMPLE composite key when any of its columns is NULL -- which is
/// precisely what makes a partially-filled cabinet legal. An empty string is a value, not a
/// NULL, so the key is enforced, finds no such row, and the insert fails with 23503.
///
/// These queries interpolate their values directly rather than binding parameters, so the choice
/// between NULL and '' has to be made here, when the literal is built.
/// </summary>
public static class CabinetSql
{
    /// <summary>
    /// A cabinet code as a SQL literal: the quoted, escaped value, or the keyword NULL when the
    /// level was left unset.
    ///
    /// Use it WITHOUT surrounding quotes -- it supplies its own:
    ///     $"... VALUES ({CabinetSql.Literal(input.DivisionCode)}, ...)"
    /// </summary>
    public static string Literal(string? code) =>
        string.IsNullOrWhiteSpace(code) || code.Trim() == "0" || code.Trim().ToLower() == "null"
            ? "NULL"
            : "'" + code.Trim().Replace("'", "''") + "'";

    /// <summary>
    /// A cabinet code for a parameterised query: the trimmed value, or null when the level was
    /// left unset, so the caller can bind DBNull rather than an empty string.
    /// </summary>
    public static string? NullIfBlank(string? code) =>
        string.IsNullOrWhiteSpace(code) || code.Trim() == "0" || code.Trim().ToLower() == "null"
            ? null
            : code.Trim();
}
