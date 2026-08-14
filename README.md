# ScriptureSync

A small, fully local Windows utility for preparing scripture references and
safely adding them to an OpenLP service. Planning Center integration is planned.

## Current status

- .NET 10 WPF staging interface
- Editable and pasteable scripture list
- Resilient scripture-reference parser
- Multiple Bible translations per passage
- Experimental OpenLP Remote API integration
- Automated parser and workflow tests

The OpenLP integration is under active investigation because OpenLP 3.1.7 can
crash when Bible operations overlap. Do not use the current sync path during a
live service.

## Build and test

```powershell
dotnet build ScriptureSync.slnx
dotnet test tests\ScriptureSync.Tests\ScriptureSync.Tests.csproj
```
