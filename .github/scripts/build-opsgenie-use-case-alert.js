#!/usr/bin/env node
"use strict";

const crypto = require("crypto");
const { buildUseCaseTestFailurePayload } = require("./opsgenie-use-case-payload");

const environment = process.env.USE_CASE_ENV ?? "use-case-test";
const runUrl = process.env.GITHUB_RUN_URL ?? "";
const forceAlert = process.env.FORCE_ALERT === "true";

let description = `Broker use case test failed (${environment}) - ${runUrl}`;
if (forceAlert) {
  description = `Broker use case test Opsgenie alert test (${environment}) - ${runUrl}`;
}

const payload = buildUseCaseTestFailurePayload({
  alert_id: crypto.randomUUID(),
  fired_date_time: new Date().toISOString(),
  use_case_test_environment: environment,
  github_repository: process.env.GITHUB_REPOSITORY ?? "",
  github_workflow: process.env.GITHUB_WORKFLOW ?? "",
  github_run_id: process.env.GITHUB_RUN_ID ?? "",
  github_run_url: runUrl,
  description,
});

process.stdout.write(JSON.stringify(payload));
