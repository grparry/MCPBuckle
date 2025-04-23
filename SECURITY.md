# Security Policy

## Supported Versions

We currently support the following versions with security updates:

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |

## Reporting a Vulnerability

We take the security of MCPBuckle seriously. If you believe you've found a security vulnerability, please follow these steps:

1. **Do not disclose the vulnerability publicly**
2. **Open a security advisory in the GitHub repository**
   - Provide a detailed description of the vulnerability
   - Include steps to reproduce the issue
   - If possible, include a proof of concept
   - Let us know how you'd like to be credited (if desired)

## What to Expect

- We will acknowledge receipt of your vulnerability report within 3 business days
- We will provide an initial assessment of the report within 10 business days
- We will keep you informed about our progress addressing the issue
- Once the vulnerability is fixed, we will publicly acknowledge your responsible disclosure (unless you prefer to remain anonymous)

## Security Best Practices for Users

When using MCPBuckle in your applications, consider the following security best practices:

1. **API Exposure**: Be mindful of what APIs you expose through MCPBuckle. Consider using controller filtering to limit exposed endpoints.
2. **Sensitive Information**: Avoid exposing sensitive information in API descriptions or documentation that will be included in the MCP context.
3. **Authentication**: Consider implementing authentication for the MCP context endpoint in production environments.
4. **Regular Updates**: Keep MCPBuckle and its dependencies up to date to benefit from security fixes.

Thank you for helping keep MCPBuckle and its community safe!
