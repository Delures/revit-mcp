# Security policy

## What counts as a vulnerability

- Escaping the `execute_csharp` sandbox out of the Revit process.
- Running code with higher privileges than the user (path substitution, DLL hijacking, path traversal in `User Plugins` or `tools`).
- Bypassing the Named Pipe checks: reaching another Revit instance, spoofing `sessionId`.
- Forging an `.addin` manifest that makes Revit load an arbitrary assembly at startup.
- Leaking model content or parameters off the machine (the server must never talk to the network).

## By design

The MCP server deliberately lets an AI agent execute arbitrary C# inside the current Revit session — that is its core feature, not a vulnerability. Protection relies on the server listening only on local named pipes, opening no TCP ports and sending no data outside.

## Reporting

Please do not open a public issue. Use GitHub Security Advisories ("Report a vulnerability" on the Security tab) instead.

Include the server version, Revit version, reproduction steps and impact assessment. You can expect a reply within 7 days.

## Supported versions

Fixes land in the latest major version. Older major versions are not supported.
