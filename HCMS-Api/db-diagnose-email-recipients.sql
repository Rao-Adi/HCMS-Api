-- Run on PROD (read-only). For every notification since 25 Sep, did the recipient have an
-- active employee record with an email address? A day where "no_email" jumps to 100% points at data.
SELECT n.createdat::date AS day,
       COUNT(*)                                   AS notifications,
       COUNT(*) FILTER (WHERE e.email IS NULL)    AS recipient_has_no_email,
       COUNT(*) FILTER (WHERE e.email IS NOT NULL) AS should_have_been_emailed
FROM Notifications n
LEFT JOIN LATERAL (
    SELECT NULLIF(TRIM(emp.Email), '') AS email
    FROM tblEmployee emp
    WHERE LTRIM(RTRIM(emp.empCode::text), '0') = LTRIM(RTRIM(n.EmployeeCode), '0')
      AND emp.CompanyId = n.CompanyId
      AND COALESCE(emp.Active, 1) = 1
    LIMIT 1
) e ON TRUE
WHERE n.CreatedAt >= '2026-09-25'
GROUP BY 1
ORDER BY 1;
