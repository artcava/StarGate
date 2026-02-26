# StarGate Security Tests

Comprehensive security testing for the StarGate API.

## Test Categories

### 1. Authentication Tests
- JWT token validation
- Token expiration
- Token malformation
- Issuer/audience validation

### 2. Authorization Tests
- Scope validation
- Role-based access control
- Resource-based authorization
- Client ID validation

### 3. Rate Limiting Tests
- Per-client rate limits
- Rate limit responses (429)
- Independent client limits
- Retry-After headers

### 4. CORS Tests
- Origin validation
- Preflight requests
- CORS headers
- Malicious origin rejection

### 5. Input Validation Tests
- XSS prevention
- SQL injection prevention
- Path traversal prevention
- Data type validation
- Range validation

## Running Security Tests

```bash
# Run all security tests
dotnet test tests/StarGate.Security.Tests

# Run specific category
dotnet test tests/StarGate.Security.Tests --filter "FullyQualifiedName~Authentication"

# Run with detailed output
dotnet test tests/StarGate.Security.Tests --logger "console;verbosity=detailed"
```

## Security Scanning Tools

### OWASP ZAP
```bash
# Run ZAP baseline scan
docker run -v $(pwd):/zap/wrk:rw -t owasp/zap2docker-stable \
  zap-baseline.py -t http://localhost:5000 -r security-report.html
```

### Dependency Scanning
```bash
# Check for vulnerable packages
dotnet list package --vulnerable

# Update packages
dotnet outdated
```

## Manual Security Testing

See [SECURITY-CHECKLIST.md](./SECURITY-CHECKLIST.md) for manual verification steps.
