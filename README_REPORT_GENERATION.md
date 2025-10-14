# Daily Summary Report Generation

This implementation generates daily summary reports with aggregated file transfer data per service owner and resource per day in Parquet format.

## Features Implemented

### 1. Data Aggregation
- Aggregates file transfers by day, resource ID, and service owner
- Counts the number of file transfers for each combination
- Returns all historical data (no filters or parameters)
- Joins through: `file_transfer` → `storage_provider` → `service_owner`

### 2. Parquet File Generation
- Uses `Parquet.Net` library (version 5.0.2)
- Generates efficient columnar storage format
- Uses `JsonPropertyName` attributes to map C# properties to lowercase parquet column names
- Schema includes:
  - `date`: Date of the file transfers (yyyy-MM-dd format)
  - `year`, `month`, `day`: Date components
  - `serviceownerorgnr`: Service owner organization number
  - `serviceownercode`: Service owner name
  - `serviceresourceid`: Resource identifier
  - `serviceresourcetitle`: Resource title (from Altinn Resource Registry)
  - `recipienttype`: Hardcoded as "Organization"
  - `costcenter`: Hardcoded as "Altinn3" (AltinnVersion)
  - `numberoffiletransfers`: Count of file transfers
  - `databasestoragebytes`: Set to 0 (not tracked)
  - `attachmentstoragebytes`: Set to 0 (not tracked)

### 3. Direct Download Endpoint

#### GET `/broker/api/v1/report/generate-and-download-daily-summary`
Generates and downloads a daily summary report as a Parquet file.
- **Method**: GET
- **Authorization**: None (publicly accessible)
- **Query Parameters**: None
- **Response**: Parquet file as `application/octet-stream` for direct download
- **Filename Format**: `broker_YYYYMMDD_HHmmss_daily_summary_report_{environment}.parquet`
  - Example: `broker_20251014_141120_daily_summary_report_development.parquet`

## Architecture

### Application Layer
- **`GenerateDailySummaryReportHandler`**: Main handler with single method:
  - `Process()`: Returns `OneOf<Stream, Error>` - just the parquet file stream
  - `GetResourceTitle()`: Helper method to fetch resource titles from Altinn Resource Registry
- **Data Models**:
  - `DailySummaryData`: Internal data model with clean property names
    - `Date`, `ServiceOwnerId`, `ServiceOwnerName`, `ResourceId`, `FileTransferCount`
  - `ParquetDailySummaryData`: Parquet-serializable model with `JsonPropertyName` attributes
    - Maps internal C# names to lowercase parquet column names

### Core Layer
- **Domain**:
  - `DailySummaryData`: Domain entity representing aggregated data from database
- **Repository Extension**:
  - `IFileTransferRepository.GetDailySummaryData()`: Database query for aggregated data
  - Uses `reader.GetOrdinal("column_name")` for type-safe column access

### Persistence Layer
- **SQL Query**:
  ```sql
  SELECT 
      DATE(f.created) as date,
      so.service_owner_id_pk,
      so.service_owner_name,
      f.resource_id,
      COUNT(*) as count
  FROM broker.file_transfer f
  INNER JOIN broker.storage_provider sp ON sp.storage_provider_id_pk = f.storage_provider_id_fk
  INNER JOIN broker.service_owner so ON so.service_owner_id_pk = sp.service_owner_id_fk
  GROUP BY DATE(f.created), so.service_owner_id_pk, so.service_owner_name, f.resource_id
  ```

### API Layer
- **`ReportController`**: Single GET endpoint for generate-and-download
  - Handles filename generation with environment name
  - Returns file with metadata in HTTP headers
  - No authentication required

## Configuration

No additional configuration needed. The report is generated in-memory and returned directly to the client.

## Error Handling

- **Error 24** (`NoFileTransfersFoundForReport`): No file transfers found for the specified criteria (404 Not Found)
- **Error 25** (`ReportGenerationFailed`): Failed to generate statistics report (500 Internal Server Error)

## Usage Examples

### Generate and Download Report
```bash
curl -X GET "https://broker.altinn.no/broker/api/v1/report/generate-and-download-daily-summary" \
  --output broker_20251014_141120_daily_summary_report_production.parquet
```

Or via browser:
```
https://broker.altinn.no/broker/api/v1/report/generate-and-download-daily-summary
```

## Reading Parquet Files

### Python
```python
import pandas as pd

df = pd.read_parquet('file-transfer-summary_20250110_143022.parquet')
print(df.head())
```

### C# with Parquet.Net
```csharp
using Parquet.Serialization;

var data = await ParquetSerializer.DeserializeAsync<ParquetDailySummaryData>(
    File.OpenRead("file-transfer-summary_20250110_143022.parquet"));
    
foreach (var row in data)
{
    Console.WriteLine($"{row.Date}: {row.FileTransferCount} transfers");
}
```

## Parquet File Schema

The generated parquet file contains the following columns (lowercase names):

| Column Name | Type | Description | Source |
|------------|------|-------------|--------|
| `date` | string | Date in yyyy-MM-dd format | `DATE(f.created)` |
| `year` | int | Year component | Derived from date |
| `month` | int | Month component | Derived from date |
| `day` | int | Day component | Derived from date |
| `serviceownerorgnr` | string | Service owner org number | `service_owner.service_owner_id_pk` |
| `serviceownercode` | string | Service owner name | `service_owner.service_owner_name` |
| `serviceresourceid` | string | Resource ID | `file_transfer.resource_id` |
| `serviceresourcetitle` | string | Resource title | Altinn Resource Registry API |
| `recipienttype` | string | Recipient type | Hardcoded: "Organization" |
| `costcenter` | string | Altinn version | Hardcoded: "Altinn3" |
| `numberoffiletransfers` | int | Count of file transfers | `COUNT(*)` |
| `databasestoragebytes` | long | Database storage | Set to 0 |
| `attachmentstoragebytes` | long | File storage | Set to 0 |

## Dependencies

- **Parquet.Net** (5.0.2): Parquet file generation and serialization

## Files Created

### Application Layer (`src/Altinn.Broker.Application/GenerateReport/`)
- `ParquetDailySummaryData.cs` - Parquet-serializable model with JsonPropertyName attributes
- `GenerateDailySummaryReportHandler.cs` - Handler that generates the report

### Core Layer (`src/Altinn.Broker.Core/Domain/`)
- `DailySummaryData.cs` - Internal domain model

### API Layer
- `src/Altinn.Broker.API/Controllers/ReportController.cs` - Single GET endpoint

### Persistence Layer
- Extended `IFileTransferRepository` with `GetDailySummaryData()` method
- Implemented SQL aggregation query in `FileTransferRepository`

## Implementation Pattern

This implementation follows the pattern established in the [Altinn Correspondence project](https://github.com/Altinn/altinn-correspondence/pull/1298/files), providing:
1. Single endpoint for generate-and-download (no blob storage upload)
2. Parquet format for efficient data storage with `JsonPropertyName` mapping
3. Proper error handling and logging
4. No authentication (publicly accessible for now)
5. In-memory report generation for immediate download
6. File metadata (filename with environment, content type) in HTTP response headers
7. Raw SQL with Npgsql using `reader.GetOrdinal("column_name")` pattern
8. Clean separation: Domain model (internal names) → Parquet model (external names)

