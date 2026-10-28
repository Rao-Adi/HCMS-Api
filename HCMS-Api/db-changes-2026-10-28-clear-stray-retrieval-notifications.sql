-- "DMS - Task: Retrieve Physical Copies" (NotificationType 15) was sent, by the old Effective-time code, to everyone on the
-- document's distribution list instead of the Document Control Associate. That code is gone; these rows are what it left.
-- Run the SELECT first and check the list, then the DELETE. Rows addressed to a real Document Control Associate stay.

-- 1) Preview
SELECT n.id, n.employeecode, n.relatedentityid AS document_id, n.isread, n.createdat
FROM Notifications n
WHERE n.NotificationType = 15
  AND NOT EXISTS (
        SELECT 1
        FROM tblEmployee e
        JOIN tblempjobprofile j ON j.empid = e.empid AND COALESCE(j.active, TRUE) = TRUE
        JOIN tblsetupsdetail sd ON sd.sdlid = j.roleid AND sd.smsid = 189
        WHERE sd.name ILIKE 'Document Control Associate%'
          AND LTRIM(e.empcode::text, '0') = LTRIM(REPLACE(n.employeecode, '`', ''), '0'))
ORDER BY n.id DESC;

-- 2) Delete (uncomment after checking the preview)
-- DELETE FROM Notifications n
-- WHERE n.NotificationType = 15
--   AND NOT EXISTS (
--         SELECT 1
--         FROM tblEmployee e
--         JOIN tblempjobprofile j ON j.empid = e.empid AND COALESCE(j.active, TRUE) = TRUE
--         JOIN tblsetupsdetail sd ON sd.sdlid = j.roleid AND sd.smsid = 189
--         WHERE sd.name ILIKE 'Document Control Associate%'
--           AND LTRIM(e.empcode::text, '0') = LTRIM(REPLACE(n.employeecode, '`', ''), '0'));
