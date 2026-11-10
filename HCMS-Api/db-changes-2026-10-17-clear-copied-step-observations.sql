-- Approving a step used to copy the observation just given onto the NEXT step as well, so a step that was still
-- waiting (nobody had acted on it) showed the previous approver's words in the Observation History.
--
-- This blanks that text (the column does not allow NULL) on steps that have not acted: no decision and no action date. A step that left a comment
-- (COMMENT) has an action date and is kept. Rows that are cleared are copied to
-- workflowexecutionsteps_observation_bak_20261017 first.
-- Safe to run more than once.

BEGIN;

CREATE TABLE IF NOT EXISTS workflowexecutionsteps_observation_bak_20261017 AS
SELECT Id, WorkflowExecutionId, Observation FROM WorkflowExecutionSteps WHERE FALSE;

INSERT INTO workflowexecutionsteps_observation_bak_20261017 (Id, WorkflowExecutionId, Observation)
SELECT Id, WorkflowExecutionId, Observation
FROM WorkflowExecutionSteps
WHERE Decision IS NULL AND ActionAt IS NULL AND Observation IS NOT NULL AND TRIM(Observation) <> ''
  AND Id NOT IN (SELECT Id FROM workflowexecutionsteps_observation_bak_20261017);

UPDATE WorkflowExecutionSteps
SET Observation = ''
WHERE Decision IS NULL AND ActionAt IS NULL AND Observation IS NOT NULL AND TRIM(Observation) <> '';

SELECT Id, WorkflowExecutionId, Observation FROM workflowexecutionsteps_observation_bak_20261017 ORDER BY Id;

COMMIT;
