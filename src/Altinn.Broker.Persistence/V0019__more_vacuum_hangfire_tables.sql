ALTER TABLE hangfire.state SET (
  autovacuum_vacuum_scale_factor = 0.01,
  autovacuum_vacuum_threshold = 1000,
  autovacuum_vacuum_cost_delay = 10,
  autovacuum_vacuum_cost_limit = 1000
);

ALTER TABLE hangfire.job SET (
  autovacuum_vacuum_scale_factor = 0.01,
  autovacuum_vacuum_threshold = 1000,
  autovacuum_vacuum_cost_delay = 10,
  autovacuum_vacuum_cost_limit = 1000
);

ALTER TABLE hangfire.jobparameter SET (
  autovacuum_vacuum_scale_factor = 0.01,
  autovacuum_vacuum_threshold = 1000,
  autovacuum_vacuum_cost_delay = 10,
  autovacuum_vacuum_cost_limit = 1000
);

ALTER TABLE hangfire.lock SET (
  autovacuum_vacuum_scale_factor = 0,
  autovacuum_vacuum_threshold = 100,
  autovacuum_vacuum_cost_delay = 10
);

ALTER TABLE hangfire.counter SET (
  autovacuum_vacuum_scale_factor = 0,
  autovacuum_vacuum_threshold = 100
);

ALTER TABLE hangfire.jobqueue SET (
  autovacuum_vacuum_scale_factor = 0,
  autovacuum_vacuum_threshold = 100
);
