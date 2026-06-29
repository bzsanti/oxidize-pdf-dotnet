using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DataFormats;
using OxidizePdf.NET.KernelMemory;

namespace OxidizePdf.NET.KernelMemory.Tests;

public class WithOxidizePdfTests
{
    [Fact]
    public void WithOxidizePdf_registers_a_pdf_content_decoder()
    {
        var builder = new KernelMemoryBuilder(new ServiceCollection());

        var returned = builder.WithOxidizePdf();

        Assert.Same(builder, returned);

        // ServiceCollectionPool.BuildServiceProvider() throws because the pool holds
        // multiple internal IServiceCollections of different sizes (the one we passed
        // plus KM's own) and CopyTo is disallowed in that state.
        // We query the ServiceDescriptor registry directly via GetEnumerator (safe).
        var descriptor = builder.Services
            .FirstOrDefault(d =>
                d.ServiceType == typeof(IContentDecoder) &&
                d.ImplementationInstance is OxidizePdfDecoder);

        Assert.NotNull(descriptor);

        var instance = (IContentDecoder)descriptor!.ImplementationInstance!;
        Assert.True(instance.SupportsMimeType("application/pdf"));
    }
}
