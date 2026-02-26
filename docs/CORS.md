# CORS Configuration

This document describes the Cross-Origin Resource Sharing (CORS) configuration for the StarGate API.

## Overview

CORS is a security feature implemented by web browsers that restricts web pages from making requests to a different domain than the one serving the web page. The StarGate API implements CORS middleware to allow controlled access from web browsers.

## Architecture

The CORS implementation consists of:

- **`ApiCorsOptions`**: Type-safe configuration class for CORS settings
- **`CorsExtensions`**: Extension methods to configure CORS policies
- **`CorsHeadersMiddleware`**: Middleware to add custom correlation headers

## Configuration

### Development Environment

In development, CORS is configured to be permissive to facilitate local development:

```json
{
  "Cors": {
    "Enabled": true,
    "AllowedOrigins": [
      "http://localhost:3000",
      "http://localhost:4200"
    ],
    "AllowAnyOrigin": true,
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
    "AllowedHeaders": ["*"],
    "ExposedHeaders": ["X-Correlation-Id", "X-Request-Id"],
    "AllowCredentials": true,
    "PreflightMaxAgeSeconds": 600
  }
}
```

### Production Environment

In production, CORS is configured with strict origin restrictions:

```json
{
  "Cors": {
    "Enabled": true,
    "AllowedOrigins": [
      "https://app.example.com",
      "https://*.example.com"
    ],
    "AllowAnyOrigin": false,
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE"],
    "AllowedHeaders": [
      "Content-Type",
      "Authorization",
      "X-Requested-With",
      "X-Correlation-Id"
    ],
    "ExposedHeaders": [
      "X-Correlation-Id",
      "X-Request-Id",
      "Retry-After"
    ],
    "AllowCredentials": true,
    "PreflightMaxAgeSeconds": 3600
  }
}
```

## Configuration Options

The `ApiCorsOptions` class provides the following configuration options:

### Enabled
- **Type**: `bool`
- **Default**: `true`
- **Description**: Whether CORS is enabled. Set to `false` to disable CORS entirely.

### AllowedOrigins
- **Type**: `List<string>`
- **Default**: `[]`
- **Description**: List of allowed origin URLs. Supports wildcard subdomains (e.g., `https://*.example.com`).
- **Example**: `["https://app.example.com", "https://*.example.com"]`

### AllowAnyOrigin
- **Type**: `bool`
- **Default**: `false`
- **Description**: Whether to allow any origin. **Should only be `true` in development.**
- **Security Note**: Never set to `true` in production.

### AllowedMethods
- **Type**: `List<string>`
- **Default**: `["GET", "POST", "PUT", "DELETE", "OPTIONS"]`
- **Description**: List of allowed HTTP methods. Use `["*"]` to allow all methods.

### AllowedHeaders
- **Type**: `List<string>`
- **Default**: `["*"]`
- **Description**: List of allowed request headers. Use `["*"]` to allow all headers.
- **Production Recommendation**: List specific headers instead of using wildcard.

### ExposedHeaders
- **Type**: `List<string>`
- **Default**: `[]`
- **Description**: List of response headers that browsers are allowed to access.
- **Common Values**: `["X-Correlation-Id", "X-Request-Id", "Retry-After"]`

### AllowCredentials
- **Type**: `bool`
- **Default**: `true`
- **Description**: Whether to allow credentials (cookies, authorization headers) in CORS requests.
- **Important**: Cannot be `true` when `AllowAnyOrigin` is `true`.

### PreflightMaxAgeSeconds
- **Type**: `int`
- **Default**: `600` (10 minutes)
- **Description**: How long (in seconds) the browser can cache preflight request results.
- **Recommended**: `600` for development, `3600` for production.

## CORS Request Types

### Simple Requests

Simple requests do not trigger a preflight request. They meet all these conditions:
- Method: `GET`, `HEAD`, or `POST`
- Only simple headers (e.g., `Accept`, `Content-Type` with specific values)
- No custom headers

For simple requests, the server adds CORS headers directly to the response.

### Preflight Requests

Preflight requests are `OPTIONS` requests sent by the browser before the actual request. They are triggered by:
- Custom HTTP methods (e.g., `PUT`, `DELETE`, `PATCH`)
- Custom headers (e.g., `Authorization`, `X-Correlation-Id`)
- `Content-Type` other than `application/x-www-form-urlencoded`, `multipart/form-data`, or `text/plain`

The server responds with:
- `Access-Control-Allow-Origin`: Allowed origin
- `Access-Control-Allow-Methods`: Allowed HTTP methods
- `Access-Control-Allow-Headers`: Allowed request headers
- `Access-Control-Max-Age`: Preflight cache duration
- `Access-Control-Allow-Credentials`: Whether credentials are allowed

## Wildcard Subdomain Support

The API supports wildcard subdomain patterns:

```json
"AllowedOrigins": [
  "https://*.example.com"
]
```

This allows requests from:
- `https://app.example.com`
- `https://admin.example.com`
- `https://api.example.com`

But not from:
- `https://example.com` (no subdomain)
- `http://*.example.com` (different protocol)

## Security Best Practices

### 1. Never Use `AllowAnyOrigin` in Production
```json
// ❌ Bad (Production)
"AllowAnyOrigin": true

// ✅ Good (Production)
"AllowAnyOrigin": false,
"AllowedOrigins": ["https://app.example.com"]
```

### 2. Always Use HTTPS in Production
```json
// ❌ Bad (Production)
"AllowedOrigins": ["http://app.example.com"]

// ✅ Good (Production)
"AllowedOrigins": ["https://app.example.com"]
```

### 3. Specify Exact Headers in Production
```json
// ❌ Less secure
"AllowedHeaders": ["*"]

// ✅ More secure
"AllowedHeaders": [
  "Content-Type",
  "Authorization",
  "X-Requested-With"
]
```

### 4. Limit Allowed Methods
```json
// ❌ Over-permissive
"AllowedMethods": ["*"]

// ✅ Restricted to necessary methods
"AllowedMethods": ["GET", "POST", "PUT", "DELETE"]
```

### 5. Credentials and Origins
```json
// ❌ Invalid combination
"AllowAnyOrigin": true,
"AllowCredentials": true

// ✅ Valid combinations
"AllowAnyOrigin": false,
"AllowedOrigins": ["https://app.example.com"],
"AllowCredentials": true
```

## Testing CORS

### Test Preflight Request

```bash
curl -X OPTIONS http://localhost:5000/api/processes \
  -H "Origin: http://localhost:3000" \
  -H "Access-Control-Request-Method: POST" \
  -H "Access-Control-Request-Headers: authorization,content-type" \
  -i
```

**Expected Response Headers:**
```
HTTP/1.1 204 No Content
Access-Control-Allow-Origin: http://localhost:3000
Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS
Access-Control-Allow-Headers: authorization, content-type
Access-Control-Max-Age: 600
Access-Control-Allow-Credentials: true
```

### Test Actual Request

```bash
curl http://localhost:5000/health/live \
  -H "Origin: http://localhost:3000" \
  -i
```

**Expected Response Headers:**
```
HTTP/1.1 200 OK
Access-Control-Allow-Origin: http://localhost:3000
X-Correlation-Id: 0HMVQJQ7V8QQD:00000001
X-Request-Id: 550e8400-e29b-41d4-a716-446655440000
```

### Test from Browser Console

Open browser developer tools on `http://localhost:3000` and run:

```javascript
fetch('http://localhost:5000/api/processes', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': 'Bearer token'
  },
  body: JSON.stringify({
    clientId: 'test-client',
    processType: 'order',
    clientProcessId: 'order-123',
    idempotencyKey: 'key-123'
  })
})
.then(r => r.json())
.then(console.log)
.catch(console.error);
```

## Common CORS Errors

### Error: No 'Access-Control-Allow-Origin' header

**Browser Console:**
```
Access to fetch at 'http://localhost:5000/api/processes' from origin 
'http://localhost:3000' has been blocked by CORS policy: No 
'Access-Control-Allow-Origin' header is present on the requested resource.
```

**Cause:** The origin is not in the `AllowedOrigins` list.

**Solution:** Add the origin to `AllowedOrigins` or set `AllowAnyOrigin: true` (development only).

### Error: Credential is not supported with wildcard origin

**Browser Console:**
```
Access to fetch at 'http://localhost:5000/api/processes' from origin 
'http://localhost:3000' has been blocked by CORS policy: The value of 
the 'Access-Control-Allow-Origin' header in the response must not be 
the wildcard '*' when the request's credentials mode is 'include'.
```

**Cause:** Using `AllowAnyOrigin: true` with `AllowCredentials: true`.

**Solution:** Set `AllowAnyOrigin: false` and specify exact origins.

### Error: Method not allowed

**Browser Console:**
```
Access to fetch at 'http://localhost:5000/api/processes' from origin 
'http://localhost:3000' has been blocked by CORS policy: Method PUT is 
not allowed by Access-Control-Allow-Methods in preflight response.
```

**Cause:** The HTTP method is not in the `AllowedMethods` list.

**Solution:** Add the method to `AllowedMethods`.

### Error: Request header not allowed

**Browser Console:**
```
Access to fetch at 'http://localhost:5000/api/processes' from origin 
'http://localhost:3000' has been blocked by CORS policy: Request header 
field authorization is not allowed by Access-Control-Allow-Headers in 
preflight response.
```

**Cause:** A request header is not in the `AllowedHeaders` list.

**Solution:** Add the header to `AllowedHeaders` or use `["*"]` (development only).

## Middleware Ordering

CORS middleware must be positioned correctly in the middleware pipeline:

```csharp
app.UseGlobalExceptionHandling();
app.UseHttpsRedirection();
app.UseApiCors();              // ← Must be before authentication
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
```

**Why?** Preflight requests (`OPTIONS`) do not include authentication headers. If CORS middleware comes after authentication, preflight requests will be rejected.

## Custom Headers

The API automatically adds custom correlation headers to all responses:

- **X-Correlation-Id**: Trace identifier for request tracking across distributed systems
- **X-Request-Id**: Unique identifier for each request

These headers must be listed in `ExposedHeaders` to be accessible by browser JavaScript:

```json
"ExposedHeaders": [
  "X-Correlation-Id",
  "X-Request-Id"
]
```

## Implementation Details

### ApiCorsOptions Class

The `ApiCorsOptions` class provides type-safe configuration:

```csharp
public class ApiCorsOptions
{
    public const string SectionName = "Cors";
    public bool Enabled { get; init; } = true;
    public List<string> AllowedOrigins { get; init; } = new();
    public bool AllowAnyOrigin { get; init; } = false;
    // ... other properties
}
```

### CorsExtensions

Extension methods for easy configuration:

```csharp
// In Program.cs
builder.Services.AddApiCors(builder.Configuration, builder.Environment);
app.UseApiCors();
```

## References

- [MDN CORS Documentation](https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS)
- [ASP.NET Core CORS](https://learn.microsoft.com/en-us/aspnet/core/security/cors)
- [CORS Specification](https://www.w3.org/TR/cors/)
- [Issue #73: Configure CORS](https://github.com/artcava/StarGate/issues/73)
