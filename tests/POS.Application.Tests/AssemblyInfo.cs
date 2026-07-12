using Xunit;

// CatalogImportBootstrapTests mutates the process-wide POS_CATALOG_SOURCE_XLSX env var,
// which races with any other test invoking DatabaseSchemaRollout.EnsureLatestSchemaAsync
// (e.g. LoyaltySchemaRolloutTests) under xUnit's default parallel collection execution.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
