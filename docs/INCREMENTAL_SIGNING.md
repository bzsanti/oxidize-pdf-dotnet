# Incremental signing

`PdfSigning.PrepareAsync` appends a signature revision while preserving the
original PDF bytes. It does not accept private keys. Supply the prepared bytes
to your CMS signing provider, then embed its detached DER CMS result:

```csharp
using var prepared = await PdfSigning.PrepareAsync(pdfBytes, new PdfSigningOptions
{
    FieldName = "Approval",
    PlaceholderBytes = 16384,
    Reason = "Approved",
    Rectangle = [30, 30, 300, 120],
    Appearance = new PdfSignatureAppearance
    {
        SignerName = "Example Signer",
        Text = ["Approved"]
    }
}, cancellationToken);

// Provider integration: create detached CMS over these exact bytes.
byte[] cms = await signer.CreateDetachedCmsAsync(prepared.BytesToDigest, cancellationToken);
byte[] signedPdf = prepared.Complete(cms);
```

`signer` in this example represents your external provider, not a library type.
If the provider accepts a digest rather than content, hash `BytesToDigest` with
the algorithm configured for that provider. Do not sign the whole prepared PDF:
the reserved Contents slot is excluded by `ByteRange`.

`Complete` checks DER container shape and reserved capacity, but does not verify
the CMS signature, its digest, or certificate trust. Verify the signed output
using the verification API and the trust policy required by your application.
The integration tests create a real RSA CMS and independently verify it using
.NET SignedCms; their self-signed certificate is not a trust-chain test.

Prepared sessions own native resources and must be disposed. Completion may be
retried with another CMS container; the native preparation is immutable.
`PreparedPdf`, `BytesToDigest`, and `ByteRange` are detached managed snapshots:
editing these arrays does not change the native session. Keep the bytes sent
to your signer unchanged. SafeHandle protects native lifetime during completion.
Cancellation prevents queued preparation; it does not interrupt native work.

For an existing empty signature field, set `Existing = true` and `FieldName`.
`WidgetIndex` selects an existing widget when needed. Page indexes are zero-based
and apply to new fields. Encrypted documents are rejected. Upstream checks
DocMDP and FieldMDP permissions and rejects already occupied signature fields.

`Certification` accepts PDF permission levels 1, 2, or 3. `FieldLock.Mode`
accepts `All`, `Include`, or `Exclude`; use `Fields` for Include/Exclude.
Additional dictionary entries use `PdfObjectValue` with explicit PDF types.
Reserved signing keys cannot be overridden.

Visible artwork requires a positive rectangle `[left, bottom, right, top]`.
`LayoutAppearance` checks fitting before preparing a PDF. Text must be
representable in WinAnsiEncoding; a watermark uses packed RGB bytes and opacity
between zero and one. The artwork is included in the signed byte ranges.
