# Shipping Process Examples

## Create Shipping Policy

```http
POST /api/policies/process-types
Content-Type: application/json

{
  "processType": "shipping",
  "maxRetries": 3,
  "timeoutSeconds": 30,
  "maxConcurrentProcesses": 20,
  "retentionDays": 90
}
```

## Create Shipping Process - UPS

```http
POST /api/processes
Content-Type: application/json

{
  "clientId": "warehouse-client",
  "processType": "shipping",
  "clientProcessId": "ship-001",
  "metadata": {
    "shipmentId": "SHIP-20260218-001",
    "orderId": "ORD-12345",
    "destination": "123 Main St, New York, NY 10001",
    "carrier": "UPS"
  }
}
```

## Create Shipping Process - FedEx

```http
POST /api/processes
Content-Type: application/json

{
  "clientId": "warehouse-client",
  "processType": "shipping",
  "clientProcessId": "ship-002",
  "metadata": {
    "shipmentId": "SHIP-20260218-002",
    "orderId": "ORD-12346",
    "destination": "456 Oak Ave, Los Angeles, CA 90001",
    "carrier": "FEDEX"
  }
}
```

## Create Shipping Process - DHL

```http
POST /api/processes
Content-Type: application/json

{
  "clientId": "warehouse-client",
  "processType": "shipping",
  "clientProcessId": "ship-003",
  "metadata": {
    "shipmentId": "SHIP-20260218-003",
    "orderId": "ORD-12347",
    "destination": "789 Elm St, Chicago, IL 60601",
    "carrier": "DHL"
  }
}
```

## Create Shipping Process - USPS

```http
POST /api/processes
Content-Type: application/json

{
  "clientId": "warehouse-client",
  "processType": "shipping",
  "clientProcessId": "ship-004",
  "metadata": {
    "shipmentId": "SHIP-20260218-004",
    "orderId": "ORD-12348",
    "destination": "321 Pine Rd, Houston, TX 77001",
    "carrier": "USPS"
  }
}
```

## Expected Process Flow

1. **Calculate Shipping Cost** (~150ms)
   - Cost varies by carrier:
     - UPS: ~$15.99 base
     - FedEx: ~$17.99 base
     - DHL: ~$22.99 base
     - USPS: ~$12.99 base
   - Random variation up to $5.00 added to simulate dynamic pricing

2. **Reserve Carrier Capacity** (~200ms)
   - 2% simulated failure rate
   - Retryable error (HttpRequestException)
   - Demonstrates capacity constraints

3. **Generate Shipping Label** (~100ms)
   - Tracking number format: `TRK{timestamp}{last4OfShipmentId}`
   - Example: `TRK20260218123000001`
   - Unique per second, sortable by timestamp

4. **Notify Warehouse** (~50ms)
   - Message sent to warehouse management system
   - Includes shipmentId, orderId, trackingNumber

5. **Update Shipment Status** (~50ms)
   - Status set to "ReadyToShip"
   - Database update simulation

**Total estimated time**: ~550ms (excluding random failures and retries)

## Error Scenarios

### Missing Required Field - Shipment ID

```http
POST /api/processes
Content-Type: application/json

{
  "clientId": "warehouse-client",
  "processType": "shipping",
  "clientProcessId": "ship-error-001",
  "metadata": {
    "orderId": "ORD-ERROR",
    "destination": "Test Address",
    "carrier": "UPS"
  }
}
```

**Result**: Process fails with `InvalidOperationException`: "Shipment ID is required"

### Missing Required Field - Order ID

```http
POST /api/processes
Content-Type: application/json

{
  "clientId": "warehouse-client",
  "processType": "shipping",
  "clientProcessId": "ship-error-002",
  "metadata": {
    "shipmentId": "SHIP-ERROR-001",
    "destination": "Test Address",
    "carrier": "UPS"
  }
}
```

**Result**: Process fails with `InvalidOperationException`: "Order ID is required"

### Missing Required Field - Destination

```http
POST /api/processes
Content-Type: application/json

{
  "clientId": "warehouse-client",
  "processType": "shipping",
  "clientProcessId": "ship-error-003",
  "metadata": {
    "shipmentId": "SHIP-ERROR-001",
    "orderId": "ORD-ERROR",
    "carrier": "UPS"
  }
}
```

**Result**: Process fails with `InvalidOperationException`: "Destination is required"

### Missing Required Field - Carrier

```http
POST /api/processes
Content-Type: application/json

{
  "clientId": "warehouse-client",
  "processType": "shipping",
  "clientProcessId": "ship-error-004",
  "metadata": {
    "shipmentId": "SHIP-ERROR-001",
    "orderId": "ORD-ERROR",
    "destination": "Test Address"
  }
}
```

**Result**: Process fails with `InvalidOperationException`: "Carrier is required"

### Invalid Carrier

```http
POST /api/processes
Content-Type: application/json

{
  "clientId": "warehouse-client",
  "processType": "shipping",
  "clientProcessId": "ship-error-005",
  "metadata": {
    "shipmentId": "SHIP-ERROR-002",
    "orderId": "ORD-ERROR",
    "destination": "Test Address",
    "carrier": "INVALID"
  }
}
```

**Result**: Process fails with `InvalidOperationException`: "Invalid carrier 'INVALID'. Valid carriers: UPS, FEDEX, DHL, USPS"

### Carrier Capacity Issue

Due to 2% simulated failure rate, occasionally a process will fail during carrier capacity reservation:

**Error**: `HttpRequestException`: "Carrier UPS has no available capacity"

**Behavior**: Process transitions to `Retrying` state and is automatically retried based on the process type policy (default: 3 retries).

## Testing Instructions

### Run Unit Tests

```bash
# Run all ShippingProcessHandler tests
dotnet test tests/StarGate.Server.Tests --filter "FullyQualifiedName~ShippingProcessHandler"

# Run with detailed output
dotnet test tests/StarGate.Server.Tests --filter "FullyQualifiedName~ShippingProcessHandler" --logger "console;verbosity=detailed"
```

### Test Handler via API

```bash
# Start the application
docker-compose up -d

# Wait for services to be ready
sleep 10

# Create shipping policy
curl -X POST http://localhost:5000/api/policies/process-types \
  -H "Content-Type: application/json" \
  -d '{
    "processType": "shipping",
    "maxRetries": 3,
    "timeoutSeconds": 30,
    "maxConcurrentProcesses": 20,
    "retentionDays": 90
  }'

# Test UPS shipping
curl -X POST http://localhost:5000/api/processes \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "test-client",
    "processType": "shipping",
    "clientProcessId": "ship-ups-001",
    "metadata": {
      "shipmentId": "SHIP-001",
      "orderId": "ORD-001",
      "destination": "New York, NY",
      "carrier": "UPS"
    }
  }'

# Test FedEx shipping
curl -X POST http://localhost:5000/api/processes \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "test-client",
    "processType": "shipping",
    "clientProcessId": "ship-fedex-001",
    "metadata": {
      "shipmentId": "SHIP-002",
      "orderId": "ORD-002",
      "destination": "Los Angeles, CA",
      "carrier": "FEDEX"
    }
  }'

# Test DHL shipping
curl -X POST http://localhost:5000/api/processes \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "test-client",
    "processType": "shipping",
    "clientProcessId": "ship-dhl-001",
    "metadata": {
      "shipmentId": "SHIP-003",
      "orderId": "ORD-003",
      "destination": "Chicago, IL",
      "carrier": "DHL"
    }
  }'

# Test USPS shipping
curl -X POST http://localhost:5000/api/processes \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "test-client",
    "processType": "shipping",
    "clientProcessId": "ship-usps-001",
    "metadata": {
      "shipmentId": "SHIP-004",
      "orderId": "ORD-004",
      "destination": "Houston, TX",
      "carrier": "USPS"
    }
  }'
```

### Verify in Logs

Expected log sequence:

```
[INF] Starting shipping processing: ProcessId=..., ClientId=test-client, ClientProcessId=ship-ups-001
[DBG] Shipping validated: ShipmentId=SHIP-001, OrderId=ORD-001, Destination=New York, NY, Carrier=UPS
[DBG] Shipping cost API called: Destination=New York, NY, Carrier=UPS
[INF] Shipping cost calculated: ShipmentId=SHIP-001, Cost=18.45
[DBG] Carrier capacity API called: Carrier=UPS, ShipmentId=SHIP-001
[INF] Carrier capacity reserved: ShipmentId=SHIP-001, Carrier=UPS
[DBG] Label generation service called: ShipmentId=SHIP-001, Destination=New York, NY
[INF] Shipping label generated: ShipmentId=SHIP-001, TrackingNumber=TRK20260302130000001
[DBG] Warehouse notification sent: ShipmentId=SHIP-001, OrderId=ORD-001, TrackingNumber=TRK20260302130000001
[INF] Warehouse notified: ShipmentId=SHIP-001
[DBG] Shipment status updated: ShipmentId=SHIP-001, Status=ReadyToShip
[INF] Shipment status updated: ShipmentId=SHIP-001, Status=ReadyToShip
[INF] Shipping processing completed successfully: ProcessId=..., ShipmentId=SHIP-001, TrackingNumber=TRK20260302130000001
```

### Test Error Scenarios

```bash
# Test invalid carrier
curl -X POST http://localhost:5000/api/processes \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "test-client",
    "processType": "shipping",
    "clientProcessId": "ship-invalid-001",
    "metadata": {
      "shipmentId": "SHIP-ERR-001",
      "orderId": "ORD-ERR-001",
      "destination": "Chicago, IL",
      "carrier": "INVALID"
    }
  }'

# Verify process fails with validation error
# Expected: Process transitions to Failed state with error message

# Test missing shipment ID
curl -X POST http://localhost:5000/api/processes \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "test-client",
    "processType": "shipping",
    "clientProcessId": "ship-invalid-002",
    "metadata": {
      "orderId": "ORD-ERR-002",
      "destination": "Chicago, IL",
      "carrier": "UPS"
    }
  }'

# Verify process fails with "Shipment ID is required" error
```

### Test All Carriers in Loop

```bash
# Test all valid carriers
for carrier in UPS FEDEX DHL USPS; do
  echo "Testing carrier: $carrier"
  curl -X POST http://localhost:5000/api/processes \
    -H "Content-Type: application/json" \
    -d '{
      "clientId": "test-client",
      "processType": "shipping",
      "clientProcessId": "ship-'$carrier'-001",
      "metadata": {
        "shipmentId": "SHIP-'$carrier'-001",
        "orderId": "ORD-'$carrier'-001",
        "destination": "Test City",
        "carrier": "'$carrier'"
      }
    }'
  echo ""
  sleep 1
done
```

## Production Considerations

### Remove Simulation Code

For production deployment:

1. **Real Carrier API Integration**: Replace `Task.Delay` with actual HTTP calls to carrier APIs
2. **Cost Calculator Service**: Integrate with actual shipping cost calculation service
3. **Real Tracking Numbers**: Obtain tracking numbers from carrier APIs instead of generating them
4. **Database Persistence**: Store shipment records in database
5. **Remove Random Failures**: Replace simulated failures with real error handling

### Add Production Features

1. **Weight and Dimensions**: Add package weight/dimensions to metadata and cost calculation
2. **International Shipping**: Support international destinations with customs data
3. **Insurance Options**: Add insurance selection and cost calculation
4. **Delivery Date Estimation**: Calculate estimated delivery dates
5. **Multi-Package Support**: Handle shipments with multiple packages
6. **Address Validation**: Validate destination addresses before processing
7. **Rate Shopping**: Compare rates across carriers and select best option

### Handler Extension Examples

#### Add Package Weight

```csharp
private static void ValidateShippingData(
    string? shipmentId,
    string? orderId,
    string? destination,
    string? carrier,
    string? weight)
{
    // ... existing validations ...

    if (string.IsNullOrWhiteSpace(weight) || !decimal.TryParse(weight, out var parsedWeight) || parsedWeight <= 0)
    {
        throw new InvalidOperationException("Valid weight is required");
    }
}

private async Task<decimal> CalculateShippingCostAsync(
    string destination,
    string carrier,
    decimal weight,
    CancellationToken cancellationToken)
{
    // ... API call ...

    var baseCost = carrier.ToUpperInvariant() switch
    {
        "UPS" => 15.99m,
        "FEDEX" => 17.99m,
        "DHL" => 22.99m,
        "USPS" => 12.99m,
        _ => 15.00m
    };

    // Add weight-based pricing
    var weightCost = weight * 0.50m; // $0.50 per pound

    return baseCost + weightCost + variation;
}
```

#### Add New Carrier

```csharp
private static void ValidateShippingData(...)
{
    // ... existing validations ...

    var validCarriers = new[] { "UPS", "FEDEX", "DHL", "USPS", "AMAZON" };
    // ...
}

private async Task<decimal> CalculateShippingCostAsync(...)
{
    // ...
    var baseCost = carrier.ToUpperInvariant() switch
    {
        "UPS" => 15.99m,
        "FEDEX" => 17.99m,
        "DHL" => 22.99m,
        "USPS" => 12.99m,
        "AMAZON" => 14.99m,
        _ => 15.00m
    };
    // ...
}
```

## Comparison with OrderProcessHandler

| Aspect | OrderProcessHandler | ShippingProcessHandler |
|--------|---------------------|------------------------|
| **Process Type** | "order" | "shipping" |
| **Primary Focus** | Payment and fulfillment | Logistics and carriers |
| **Validation** | Amount format (decimal) | Carrier whitelist |
| **External Services** | Payment gateway, inventory | Carrier API, warehouse |
| **Return Value** | None (void) | Tracking number |
| **Failure Rate** | 5% inventory, 3% payment | 2% capacity |
| **Steps** | 4 steps | 5 steps |
| **Average Duration** | ~400ms | ~550ms |

## References

- [TECHNICAL-ANALYSIS.md - Phase 7.2](https://github.com/artcava/StarGate/blob/develop/docs/TECHNICAL-ANALYSIS.md)
- [Handler Development Guide](../HANDLER-DEVELOPMENT-GUIDE.md)
- [IProcessHandler Interface](../../src/StarGate.Core/Abstractions/IProcessHandler.cs)
- [OrderProcessHandler Example](../../src/StarGate.Server/Handlers/OrderProcessHandler.cs)
- [CODING-CONVENTIONS.md](https://github.com/artcava/StarGate/blob/main/docs/CODING-CONVENTIONS.md)
