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
- **Authorization**: API Key via `X-API-Key` header (required)
- **Rate Limiting**: 10 requests per minute per IP address
- **Query Parameters**: None
- **Response**: Parquet file as `application/octet-stream` for direct download
- **Filename Format**: `broker_YYYYMMDD_HHmmss_daily_summary_report_{environment}.parquet`
  - Example: `broker_20251014_141120_daily_summary_report_development.parquet`

#### Response Headers

The response includes metadata headers to provide information about the report and rate limiting:

**Report Metadata Headers:**
- `X-Report-Total-Records`: Total number of rows in the parquet file
- `X-Report-Total-FileTransfers`: Sum of all file transfers across all records
- `X-Report-Total-ServiceOwners`: Count of unique service owners in the report
- `X-Report-Generated-At`: ISO 8601 timestamp when the report was generated (UTC)

**Rate Limiting Headers:**
- `X-RateLimit-Limit`: Maximum number of requests allowed per minute (10)
- `X-RateLimit-Remaining`: Number of requests remaining in the current window
- `X-RateLimit-Reset`: Unix timestamp when the rate limit window resets
- `Retry-After`: Seconds to wait before retrying (only included when rate limited - HTTP 429)

**Example Response Headers:**
```
HTTP/1.1 200 OK
Content-Type: application/octet-stream
Content-Disposition: attachment; filename=broker_20251014_141120_daily_summary_report_production.parquet
X-Report-Total-Records: 1523
X-Report-Total-FileTransfers: 45892
X-Report-Total-ServiceOwners: 42
X-Report-Generated-At: 2025-10-14T14:11:20.1234567Z
X-RateLimit-Limit: 10
X-RateLimit-Remaining: 9
X-RateLimit-Reset: 1697293980
```

#### Authentication

The endpoint requires API key authentication:
- **Header**: `X-API-Key: your-api-key-here`
- The API key is configured via Azure Key Vault secret `report-api-key`
- In development, the API key is set in `appsettings.Development.json`

#### Rate Limiting

To prevent abuse, rate limiting is enforced per IP address:
- **Limit**: Maximum 10 requests per minute
- **Scope**: Per IP address (using `X-Forwarded-For` header or `RemoteIpAddress`)
- **Response**: HTTP 429 Too Many Requests when limit exceeded

#### HTTP Status Codes

- **200 OK**: Report generated successfully
- **401 Unauthorized**: Missing API key in request
- **403 Forbidden**: Invalid API key
- **404 Not Found**: No file transfers found for the specified criteria
- **429 Too Many Requests**: Rate limit exceeded
- **500 Internal Server Error**: Failed to generate report

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
  - Protected by API key authentication via `ReportApiKeyFilter`
  - Rate limiting enforced per IP address (10 requests per minute)

## Configuration

### API Key Configuration

The report endpoint requires API key authentication configured via:

1. **Azure Key Vault** (Production/Staging/Test):
   - Secret name: `report-api-key`
   - Configured via GitHub secret `REPORT_API_KEY`
   - Deployed through bicep infrastructure files

2. **Local Development**:
   - Configured in `appsettings.Development.json` under `ReportApiKey.ApiKey`
   - Default development key: `dev-report-api-key-12345`

### Rate Limiting Configuration

Rate limiting is hard-coded in `ReportApiKeyFilter`:
- Maximum requests: 10 per minute
- Scope: Per IP address
- Can be modified by updating constants in the filter class

## Error Handling

- **Error 24** (`NoFileTransfersFoundForReport`): No file transfers found for the specified criteria (404 Not Found)
- **Error 25** (`ReportGenerationFailed`): Failed to generate statistics report (500 Internal Server Error)

## Usage Examples

### Generate and Download Report

**With API Key (Required):**
```bash
curl -X GET "https://broker.altinn.no/broker/api/v1/report/generate-and-download-daily-summary" \
  -H "X-API-Key: your-api-key-here" \
  -i \
  --output broker_20251014_141120_daily_summary_report_production.parquet
```

**View Response Headers:**
```bash
curl -X GET "https://broker.altinn.no/broker/api/v1/report/generate-and-download-daily-summary" \
  -H "X-API-Key: your-api-key-here" \
  -I
```

**Example Response with Headers:**
```
HTTP/1.1 200 OK
Content-Type: application/octet-stream
Content-Disposition: attachment; filename=broker_20251014_141120_daily_summary_report_production.parquet
X-Report-Total-Records: 1523
X-Report-Total-FileTransfers: 45892
X-Report-Total-ServiceOwners: 42
X-Report-Generated-At: 2025-10-14T14:11:20.1234567Z
X-RateLimit-Limit: 10
X-RateLimit-Remaining: 9
X-RateLimit-Reset: 1697293980
```

**Local Development:**
```bash
curl -X GET "http://localhost:5096/broker/api/v1/report/generate-and-download-daily-summary" \
  -H "X-API-Key: dev-report-api-key-12345" \
  -i \
  --output daily_summary_report.parquet
```

**Error Examples:**

Missing API key:
```bash
# Returns 401 Unauthorized
curl -X GET "https://broker.altinn.no/broker/api/v1/report/generate-and-download-daily-summary"
```

Invalid API key:
```bash
# Returns 403 Forbidden
curl -X GET "https://broker.altinn.no/broker/api/v1/report/generate-and-download-daily-summary" \
  -H "X-API-Key: invalid-key"
```

Rate limit exceeded:
```bash
# After 10 requests in 1 minute, returns 429 Too Many Requests
curl -X GET "https://broker.altinn.no/broker/api/v1/report/generate-and-download-daily-summary" \
  -H "X-API-Key: your-api-key" \
  -I

# Response:
# HTTP/1.1 429 Too Many Requests
# X-RateLimit-Limit: 10
# X-RateLimit-Remaining: 0
# X-RateLimit-Reset: 1697293980
# Retry-After: 45
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

## Files Created/Modified

### Application Layer (`src/Altinn.Broker.Application/GenerateReport/`)
- `ParquetDailySummaryData.cs` - Parquet-serializable model with JsonPropertyName attributes
- `GenerateDailySummaryReportHandler.cs` - Handler that generates the report

### Core Layer (`src/Altinn.Broker.Core/Domain/`)
- `DailySummaryData.cs` - Internal domain model

### API Layer
- `src/Altinn.Broker.API/Controllers/ReportController.cs` - Single GET endpoint with API key authentication
- `src/Altinn.Broker.API/Filters/ReportApiKeyFilter.cs` - API key authentication filter with rate limiting
- `src/Altinn.Broker.API/Program.cs` - Updated to register filter and configuration

### Configuration
- `appsettings.json` - Added `ReportApiKey` configuration section
- `appsettings.Development.json` - Added development API key

### Infrastructure
- `.azure/infrastructure/main.bicep` - Added `report-api-key` secret parameter
- `.azure/infrastructure/params.bicepparam` - Added `REPORT_API_KEY` environment variable
- `.github/actions/update-infrastructure/action.yml` - Added `REPORT_API_KEY` input
- `.github/workflows/deploy-to-environment.yml` - Added `REPORT_API_KEY` secret

### Persistence Layer
- Extended `IFileTransferRepository` with `GetDailySummaryData()` method
- Implemented SQL aggregation query in `FileTransferRepository`

## Implementation Pattern

This implementation follows the pattern established in the Altinn Correspondence project:
1. Single endpoint for generate-and-download (no blob storage upload)
2. Parquet format for efficient data storage with `JsonPropertyName` mapping
3. Proper error handling and logging
4. **API key authentication** via `X-API-Key` header (following [Correspondence PR #1370](https://github.com/Altinn/altinn-correspondence/pull/1370/files))
5. **Rate limiting** per IP address (10 requests per minute)
6. In-memory report generation for immediate download
7. File metadata (filename with environment, content type) in HTTP response headers
8. Raw SQL with Npgsql using `reader.GetOrdinal("column_name")` pattern
9. Clean separation: Domain model (internal names) → Parquet model (external names)
10. API key stored in Azure Key Vault and deployed via GitHub Actions

## Security

### API Key Management

1. **Production/Staging/Test**:
   - API key is stored as a GitHub secret per environment: `REPORT_API_KEY`
   - GitHub Actions deploys the secret to Azure Key Vault as `report-api-key`
   - Container App reads the key from Key Vault and injects it into the configuration

2. **Key Rotation**:
   - Update the GitHub secret `REPORT_API_KEY` for the respective environment
   - Redeploy the application to pick up the new key

3. **Rate Limiting**:
   - Prevents abuse by limiting requests to 10 per minute per IP
   - Uses `X-Forwarded-For` header for proxy/load balancer scenarios
   - Request history is stored in-memory per IP address
   - Rate limit information is included in all response headers
   - When rate limited, `Retry-After` header indicates seconds to wait

## Deployment

To deploy with the new API key authentication:

1. **Add GitHub Secret** (per environment: test, staging, production):
   ```
   Name: REPORT_API_KEY
   Value: <strong-random-api-key>
   ```

2. **Deploy Infrastructure**:
   - The workflow will automatically deploy the secret to Azure Key Vault
   - The secret will be available as `report-api-key` in Key Vault

3. **Verify Deployment**:
   ```bash
   # Test authentication
   curl -X GET "https://broker.altinn.no/broker/api/v1/report/generate-and-download-daily-summary" \
     -H "X-API-Key: your-api-key" \
     --output test_report.parquet
   ```

