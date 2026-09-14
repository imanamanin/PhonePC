Phase 1 integration tests live in `windows-client/src/PhoneControl.Tests/Integration`.

They start a loopback TCP agent on `127.0.0.1` (ephemeral port), complete `session.hello` / `session.hello_ack`, and exchange ping/pong. No physical phone is required.

Channel C framing (length-prefix + video header + raw BGRA decode) is covered by `VideoFramingTests`.

```powershell
dotnet test windows-client\src\PhoneControl.Tests\PhoneControl.Tests.csproj --filter "FullyQualifiedName~Integration"
```
