using System;
using Xunit;

namespace Firmeza.Tests.Domain;

public class ReceiptCalculationsTests
{
    [Fact]
    public void CalcularDesgloseIva_DebeCalcularSubtotalEIvaCorrectamente()
    {
        // Arrange: Una venta total con IVA del 19% incluido
        decimal totalVenta = 119000m;

        // Act: En Colombia, el subtotal se obtiene dividiendo entre 1.19
        decimal subtotal = Math.Round(totalVenta / 1.19m, 2);
        decimal iva = Math.Round(totalVenta - subtotal, 2);

        // Assert
        Assert.Equal(100000m, subtotal);
        Assert.Equal(19000m, iva);
        Assert.Equal(totalVenta, subtotal + iva);
    }

    [Theory]
    [InlineData(119000.0, 100000.0, 19000.0)]
    [InlineData(59500.0, 50000.0, 9500.0)]
    [InlineData(0.0, 0.0, 0.0)]
    public void CalcularDesgloseIva_MultiplesValores_CalculaCorrectamente(double totalVentaD, double subtotalEsperadoD, double ivaEsperadoD)
    {
        // Arrange
        decimal totalVenta = (decimal)totalVentaD;
        decimal subtotalEsperado = (decimal)subtotalEsperadoD;
        decimal ivaEsperado = (decimal)ivaEsperadoD;

        // Act
        decimal subtotal = Math.Round(totalVenta / 1.19m, 2);
        decimal iva = Math.Round(totalVenta - subtotal, 2);

        // Assert
        Assert.Equal(subtotalEsperado, subtotal);
        Assert.Equal(ivaEsperado, iva);
        Assert.Equal(totalVenta, subtotal + iva);
    }

    [Fact]
    public void CalcularSubtotalLinea_ConCantidadYPrecioUnitario_MultiplicaExacto()
    {
        // Arrange
        int cantidad = 5;
        decimal precioUnitario = 24500m;

        // Act
        decimal subtotalLinea = cantidad * precioUnitario;

        // Assert
        Assert.Equal(122500m, subtotalLinea);
    }
}
