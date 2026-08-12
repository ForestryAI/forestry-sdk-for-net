# Third-Party Notices

This repository's own code is MIT licensed (see [LICENSE](LICENSE)). Some packages adapt design
patterns and, in places, specific logic from Microsoft's open-source .NET runtime — most notably
`Forestry.Deserialize`/`Forestry.Deserialize.Xml`, whose reader/buffering shape deliberately
mirrors `System.Text.Json.Utf8JsonReader`/`JsonReaderState` (see those packages' own
ARCHITECTURE.md for why: no other primitive in the BCL offers a buffer-in/token-out contract that
works across an async refill boundary). Where a specific method or algorithm is adapted from a
specific Microsoft source file rather than just following the same general shape, look for an
inline comment pointing back to this file.

## dotnet/runtime

- **Source:** https://github.com/dotnet/runtime
- **License:** MIT License
- **Copyright:** Copyright (c) .NET Foundation and Contributors

> MIT License
>
> Permission is hereby granted, free of charge, to any person obtaining a copy
> of this software and associated documentation files (the "Software"), to deal
> in the Software without restriction, including without limitation the rights
> to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
> copies of the Software, and to permit persons to whom the Software is
> furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all
> copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
> IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
> FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
> AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
> LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
> OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
> SOFTWARE.

## Inline attribution convention

When a method or algorithm is adapted directly from a specific Microsoft source file (not just
following the same general API shape), mark it in place:

```csharp
// Adapted from System.Text.Json.Utf8JsonReader (dotnet/runtime), MIT licensed.
// See /THIRD-PARTY-NOTICES.md.
```
