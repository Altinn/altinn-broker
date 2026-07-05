function buildUseCaseTestFailurePayload(vars) {
  return {
    schemaId: "azureMonitorCommonAlertSchema",
    data: {
      essentials: {
        alertId: vars.alert_id,
        alertRule: "Broker Use Case Test",
        severity: "Sev1",
        signalType: "Metric",
        monitorCondition: "Fired",
        monitoringService: "Platform",
        alertTargetIDs: [`/Broker/${vars.use_case_test_environment}`],
        firedDateTime: vars.fired_date_time,
        description:
          vars.description ??
          `Broker use case test failed (${vars.use_case_test_environment}) - ${vars.github_run_url}`,
        essentialsVersion: "1.0",
        alertContextVersion: "1.0",
      },
      alertContext: {
        properties: null,
        conditionType: "SingleResourceMultipleMetricCriteria",
        condition: {
          windowSize: "PT5M",
          allOf: [
            {
              metricName: "UseCaseTestFailure",
              operator: "GreaterThan",
              threshold: "0",
              timeAggregation: "Count",
              metricValue: 1,
            },
          ],
        },
      },
      customProperties: {
        repository: vars.github_repository,
        workflow: vars.github_workflow,
        run_id: String(vars.github_run_id),
        run_url: vars.github_run_url,
        environment: vars.use_case_test_environment,
      },
    },
  };
}

module.exports = { buildUseCaseTestFailurePayload };
