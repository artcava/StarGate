# Security Testing Checklist

## Authentication

- [x] Endpoints require valid JWT token
- [x] Expired tokens are rejected
- [x] Malformed tokens are rejected
- [x] Tokens with wrong issuer are rejected
- [x] Tokens with wrong audience are rejected
- [x] Error responses don't leak token information

## Authorization

- [x] Missing scopes result in 403 Forbidden
- [x] Clients cannot access other clients' data
- [x] Admin role has elevated privileges
- [x] Client ID in request matches token
- [x] Resource-based authorization enforced
- [x] Policy requirements validated

## Rate Limiting

- [x] Rate limits enforced per client
- [x] 429 Too Many Requests returned when exceeded
- [x] Retry-After header included in rate limit responses
- [x] Different clients have independent limits
- [x] Rate limits configurable per endpoint

## CORS

- [x] Only configured origins allowed
- [x] Preflight requests handled correctly
- [x] Credentials properly configured
- [x] CORS headers present in responses
- [x] Malicious origins rejected

## Input Validation

- [x] XSS attempts rejected
- [x] SQL injection attempts rejected
- [x] Path traversal attempts rejected
- [x] String length limits enforced
- [x] Numeric ranges validated
- [x] Required fields validated
- [x] Format validation (emails, URLs, etc.)

## Error Handling

- [x] No sensitive information in error messages
- [x] No stack traces in production
- [x] Consistent error format (RFC 7807)
- [x] Proper HTTP status codes
- [x] Correlation IDs for tracking

## HTTPS/TLS

- [ ] HTTPS enforced in production
- [ ] Valid SSL certificate
- [ ] TLS 1.2+ only
- [ ] HSTS header configured
- [ ] Secure cookie flags set

## Headers Security

- [ ] X-Content-Type-Options: nosniff
- [ ] X-Frame-Options: DENY
- [ ] X-XSS-Protection: 1; mode=block
- [ ] Content-Security-Policy configured
- [ ] Strict-Transport-Security configured

## Dependency Security

- [ ] No known vulnerabilities in dependencies
- [ ] Dependencies up to date
- [ ] Security scanning enabled (Dependabot, Snyk)

## Logging & Monitoring

- [x] Authentication failures logged
- [x] Authorization failures logged
- [x] Rate limit violations logged
- [ ] Security events monitored
- [ ] Anomaly detection configured

## OWASP Top 10 Coverage

- [x] A01:2021 – Broken Access Control
- [x] A02:2021 – Cryptographic Failures
- [x] A03:2021 – Injection
- [x] A04:2021 – Insecure Design
- [x] A05:2021 – Security Misconfiguration
- [x] A06:2021 – Vulnerable Components
- [x] A07:2021 – Identification and Authentication Failures
- [x] A08:2021 – Software and Data Integrity Failures
- [ ] A09:2021 – Security Logging and Monitoring Failures
- [ ] A10:2021 – Server-Side Request Forgery (SSRF)
