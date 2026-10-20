-- A document that has been obsoleted should read "Obsoleted" wherever its status is shown (grids, exports, the AI
-- assistant). The status text everywhere comes from DocumentStates.Name; the code (OBSOLETE) is what the application
-- logic uses and does not change. Nothing compares the name itself, so this is only a wording change.
-- Safe to run more than once.

UPDATE DocumentStates
SET Name = 'Obsoleted'
WHERE Code = 'OBSOLETE' AND Name <> 'Obsoleted';

SELECT Id, Code, Name FROM DocumentStates WHERE Code = 'OBSOLETE';
