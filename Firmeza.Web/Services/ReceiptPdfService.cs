using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using OfficeOpenXml;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Firmeza.Domain.Entities;

namespace Firmeza.Web.Services;

public class ReceiptPdfService
{
    private readonly IWebHostEnvironment _env;

    public ReceiptPdfService(IWebHostEnvironment env)
    {
        _env = env;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // 1. Generar recibo de venta como array de bytes en memoria
    public byte[] GenerateSaleReceiptBytes(Sale sale)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(35);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                // Header
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("FIRMEZA MATERIALES").FontSize(18).Bold().FontColor(Colors.Amber.Medium);
                        col.Item().Text("NIT: 900.123.456-7");
                        col.Item().Text("Dirección: Zona Industrial Principal, Bodega 4");
                    });

                    row.ConstantItem(200).Column(col =>
                    {
                        col.Item().Text("RECIBO DE VENTA").Bold().FontSize(12).AlignRight();
                        col.Item().Text($"Nº: {sale.SaleNumber}").Bold().AlignRight();
                        col.Item().Text($"Fecha: {sale.SaleDate:dd/MM/yyyy HH:mm}").AlignRight();
                    });
                });

                // Content
                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("DATOS DEL CLIENTE").Bold();
                    col.Item().PaddingTop(5).Text($"Cliente: {sale.Customer?.FullName ?? "Consumidor Final"}");
                    col.Item().Text($"Documento: {sale.Customer?.Document ?? "N/A"}");
                    col.Item().Text($"Correo: {sale.Customer?.Email ?? "N/A"}");

                    col.Item().PaddingTop(15).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4); // Producto
                            columns.RelativeColumn(1); // Cantidad
                            columns.RelativeColumn(2); // Precio Unitario
                            columns.RelativeColumn(2); // Subtotal
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Producto").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Cant.").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Precio Unit.").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Subtotal").Bold();
                        });

                        foreach (var item in sale.Details)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(item.Product?.Name ?? item.ProductId.ToString());
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(item.Quantity.ToString());
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(item.UnitPrice.ToString("C2"));
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(item.Subtotal.ToString("C2"));
                        }
                    });

                    // Totales e IVA (19% incluido)
                    var subtotal = Math.Round(sale.Total / 1.19m, 2);
                    var iva = sale.Total - subtotal;

                    col.Item().PaddingTop(15).AlignRight().Column(totals =>
                    {
                        totals.Item().Text($"Subtotal: {subtotal:C2}");
                        totals.Item().Text($"IVA (19%): {iva:C2}");
                        totals.Item().Text($"Total a Pagar: {sale.Total:C2}").Bold().FontSize(12);
                    });
                });

                page.Footer().AlignCenter().Text("Gracias por confiar en Firmeza. Documento informativo generado automáticamente.").FontSize(9).FontColor(Colors.Grey.Medium);
            });
        });

        return doc.GeneratePdf();
    }

    // 2. Generar recibo de venta y guardarlo en wwwroot/recibos
    public string GenerateAndSaveSaleReceipt(Sale sale)
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var folder = Path.Combine(webRoot, "recibos");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var fileName = $"recibo_{sale.SaleNumber}.pdf";
        var fullPath = Path.Combine(folder, fileName);

        if (!File.Exists(fullPath))
        {
            var bytes = GenerateSaleReceiptBytes(sale);
            File.WriteAllBytes(fullPath, bytes);
        }

        return $"/recibos/{fileName}";
    }

    // 3. Exportar listado de productos a PDF
    public byte[] ExportProductsToPdf(List<Product> products)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header().Text("Catálogo de Productos - Firmeza").FontSize(16).Bold().FontColor(Colors.Amber.Medium);
                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(1);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("SKU").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Nombre").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Categoría").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Precio").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Stock").Bold();
                    });
                    foreach (var p in products)
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(p.Sku);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(p.Name);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(p.Category);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(p.Price.ToString("C2"));
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(p.Stock.ToString());
                    }
                });
            });
        }).GeneratePdf();
    }

    // 4. Exportar listado de productos a Excel con EPPlus
    public byte[] ExportProductsToExcel(List<Product> products)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Productos");

        ws.Cells[1, 1].Value = "SKU";
        ws.Cells[1, 2].Value = "Nombre";
        ws.Cells[1, 3].Value = "Categoría";
        ws.Cells[1, 4].Value = "Unidad";
        ws.Cells[1, 5].Value = "Precio";
        ws.Cells[1, 6].Value = "Stock";
        ws.Cells[1, 7].Value = "Estado";
        ws.Row(1).Style.Font.Bold = true;

        for (int i = 0; i < products.Count; i++)
        {
            var p = products[i];
            ws.Cells[i + 2, 1].Value = p.Sku;
            ws.Cells[i + 2, 2].Value = p.Name;
            ws.Cells[i + 2, 3].Value = p.Category;
            ws.Cells[i + 2, 4].Value = p.Unit;
            ws.Cells[i + 2, 5].Value = p.Price;
            ws.Cells[i + 2, 6].Value = p.Stock;
            ws.Cells[i + 2, 7].Value = p.IsActive ? "Activo" : "Inactivo";
        }

        ws.Cells.AutoFitColumns();
        return package.GetAsByteArray();
    }

    // 5. Exportar listado de clientes a PDF
    public byte[] ExportCustomersToPdf(List<Customer> customers)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header().Text("Directorio de Clientes - Firmeza").FontSize(16).Bold().FontColor(Colors.Amber.Medium);
                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Documento").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Nombre").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Email").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Teléfono").Bold();
                    });
                    foreach (var c in customers)
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(c.Document);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(c.FullName);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(c.Email);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(c.Phone);
                    }
                });
            });
        }).GeneratePdf();
    }

    // 6. Exportar listado de clientes a Excel
    public byte[] ExportCustomersToExcel(List<Customer> customers)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Clientes");

        ws.Cells[1, 1].Value = "Documento";
        ws.Cells[1, 2].Value = "Nombre Completo";
        ws.Cells[1, 3].Value = "Email";
        ws.Cells[1, 4].Value = "Teléfono";
        ws.Cells[1, 5].Value = "Edad";
        ws.Cells[1, 6].Value = "Estado";
        ws.Row(1).Style.Font.Bold = true;

        for (int i = 0; i < customers.Count; i++)
        {
            var c = customers[i];
            ws.Cells[i + 2, 1].Value = c.Document;
            ws.Cells[i + 2, 2].Value = c.FullName;
            ws.Cells[i + 2, 3].Value = c.Email;
            ws.Cells[i + 2, 4].Value = c.Phone;
            ws.Cells[i + 2, 5].Value = c.Age;
            ws.Cells[i + 2, 6].Value = c.IsActive ? "Activo" : "Inactivo";
        }

        ws.Cells.AutoFitColumns();
        return package.GetAsByteArray();
    }

    // 7. Exportar listado de ventas a PDF
    public byte[] ExportSalesToPdf(List<Sale> sales)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header().Text("Historial de Ventas - Firmeza").FontSize(16).Bold().FontColor(Colors.Amber.Medium);
                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Nº Venta").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Cliente").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Fecha").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Estado").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Total").Bold();
                    });
                    foreach (var s in sales)
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(s.SaleNumber);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(s.Customer?.FullName ?? "N/A");
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(s.SaleDate.ToString("dd/MM/yyyy HH:mm"));
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(s.Status.ToString());
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(s.Total.ToString("C2"));
                    }
                });
            });
        }).GeneratePdf();
    }

    // 8. Exportar listado de ventas a Excel
    public byte[] ExportSalesToExcel(List<Sale> sales)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Ventas");

        ws.Cells[1, 1].Value = "Número Venta";
        ws.Cells[1, 2].Value = "Cliente";
        ws.Cells[1, 3].Value = "Documento";
        ws.Cells[1, 4].Value = "Fecha";
        ws.Cells[1, 5].Value = "Estado";
        ws.Cells[1, 6].Value = "Total";
        ws.Row(1).Style.Font.Bold = true;

        for (int i = 0; i < sales.Count; i++)
        {
            var s = sales[i];
            ws.Cells[i + 2, 1].Value = s.SaleNumber;
            ws.Cells[i + 2, 2].Value = s.Customer?.FullName ?? "N/A";
            ws.Cells[i + 2, 3].Value = s.Customer?.Document ?? "N/A";
            ws.Cells[i + 2, 4].Value = s.SaleDate.ToString("dd/MM/yyyy HH:mm");
            ws.Cells[i + 2, 5].Value = s.Status.ToString();
            ws.Cells[i + 2, 6].Value = s.Total;
        }

        ws.Cells.AutoFitColumns();
        return package.GetAsByteArray();
    }
}