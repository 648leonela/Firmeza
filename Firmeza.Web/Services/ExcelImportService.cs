using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using OfficeOpenXml;
using Firmeza.Domain.Entities;
using Firmeza.Infrastructure.Persistence;
using Firmeza.Web.Presentation.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Firmeza.Web.Services;

public class ExcelImportService
{
    private readonly ApplicationDbContext _db;

    public ExcelImportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ImportResultViewModel> ImportarDesnormalizadoAsync(Stream fileStream)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var result = new ImportResultViewModel();

        using var package = new ExcelPackage(fileStream);
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();

        if (worksheet?.Dimension == null)
        {
            result.Errores.Add("El archivo Excel está vacío o no contiene hojas legibles.");
            return result;
        }

        int rowCount = worksheet.Dimension.Rows;
        int colCount = worksheet.Dimension.Columns;

        if (rowCount < 2)
        {
            result.Errores.Add("El archivo no contiene filas de datos, únicamente encabezados o celdas vacías.");
            return result;
        }

        // 1. Detección dinámica de columnas según encabezados
        var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int col = 1; col <= colCount; col++)
        {
            var header = worksheet.Cells[1, col].Text.Trim();
            if (!string.IsNullOrEmpty(header) && !colMap.ContainsKey(header))
            {
                colMap[header] = col;
            }
        }

        int FindCol(params string[] aliases)
        {
            foreach (var a in aliases)
            {
                if (colMap.TryGetValue(a, out var c))
                    return c;
            }
            return -1;
        }

        int cDoc = FindCol("Documento", "Cedula", "Nit", "DocCliente", "Doc_Cliente", "CustomerDocument", "IdCliente");
        int cNomCli = FindCol("NombreCliente", "Nombre_Cliente", "Cliente", "Nombre", "CustomerName", "Customer");
        int cEmail = FindCol("Email", "Correo", "EmailCliente", "CorreoCliente", "CustomerEmail");
        int cTel = FindCol("Telefono", "Celular", "Phone", "Tel", "TelefonoCliente");
        int cSku = FindCol("Sku", "Codigo", "CodigoProducto", "SkuProducto", "ProductSku", "Ref", "Referencia");
        int cNomProd = FindCol("NombreProducto", "Producto", "DescripcionProducto", "Descripcion", "Name", "ProductName");
        int cCat = FindCol("Categoria", "Category", "Linea", "Grupo");
        int cPrecio = FindCol("Precio", "Price", "PrecioUnitario", "UnitPrice", "Valor", "Costo");
        int cStock = FindCol("Stock", "Cantidad", "Inventario", "Cant", "Quantity");

        if (cDoc == -1 || cSku == -1)
        {
            result.Errores.Add("Columnas mínimas obligatorias ausentes: Se requiere columna de Cliente ('Documento') y de Producto ('SKU').");
            return result;
        }

        result.TotalFilas = rowCount - 1;

        // 2. Cargar datos en memoria de forma segura y tolerante a duplicados
        var existingCustomers = await _db.Customers.ToListAsync();
        var clientesPorDoc = new Dictionary<string, Customer>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in existingCustomers)
        {
            var doc = c.Document?.Trim() ?? "";
            if (!string.IsNullOrEmpty(doc) && !clientesPorDoc.ContainsKey(doc))
            {
                clientesPorDoc[doc] = c;
            }
        }

        var emailsExistentes = existingCustomers
            .Select(c => c.Email?.Trim().ToLowerInvariant())
            .Where(e => !string.IsNullOrEmpty(e))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingProducts = await _db.Products.ToListAsync();
        var productosPorSku = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in existingProducts)
        {
            var sku = p.Sku?.Trim().ToUpperInvariant() ?? "";
            if (!string.IsNullOrEmpty(sku) && !productosPorSku.ContainsKey(sku))
            {
                productosPorSku[sku] = p;
            }
        }

        for (int row = 2; row <= rowCount; row++)
        {
            var docCliente = cDoc > 0 ? worksheet.Cells[row, cDoc].Text.Trim() : "";
            var nomCliente = cNomCli > 0 ? worksheet.Cells[row, cNomCli].Text.Trim() : "";
            var emailCliente = cEmail > 0 ? worksheet.Cells[row, cEmail].Text.Trim().ToLowerInvariant() : "";
            var telCliente = cTel > 0 ? worksheet.Cells[row, cTel].Text.Trim() : "";
            var skuProducto = cSku > 0 ? worksheet.Cells[row, cSku].Text.Trim().ToUpperInvariant() : "";
            var nomProducto = cNomProd > 0 ? worksheet.Cells[row, cNomProd].Text.Trim() : "";
            var catProducto = cCat > 0 ? worksheet.Cells[row, cCat].Text.Trim() : "General";

            if (string.IsNullOrWhiteSpace(docCliente) && string.IsNullOrWhiteSpace(skuProducto))
            {
                continue; // Fila vacía
            }

            var erroresFila = new List<string>();

            if (string.IsNullOrWhiteSpace(docCliente)) erroresFila.Add("Falta documento del cliente.");
            if (string.IsNullOrWhiteSpace(nomCliente)) erroresFila.Add("Falta nombre del cliente.");
            if (string.IsNullOrWhiteSpace(skuProducto)) erroresFila.Add("Falta SKU del producto.");
            if (string.IsNullOrWhiteSpace(nomProducto)) erroresFila.Add("Falta nombre del producto.");

            var precioRaw = cPrecio > 0 ? worksheet.Cells[row, cPrecio].Value?.ToString() : null;
            if (!decimal.TryParse(precioRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var precio) || precio <= 0)
            {
                erroresFila.Add($"Precio inválido ('{precioRaw}'). Debe ser un número > 0.");
            }

            var stockRaw = cStock > 0 ? worksheet.Cells[row, cStock].Value?.ToString() : null;
            if (!int.TryParse(stockRaw, out var stock) || stock < 0)
            {
                erroresFila.Add($"Stock inválido ('{stockRaw}'). Debe ser un entero >= 0.");
            }

            if (erroresFila.Count > 0)
            {
                result.Errores.Add($"Fila {row}: {string.Join(" | ", erroresFila)}");
                continue;
            }

            // Normalizar e insertar/actualizar Cliente
            if (clientesPorDoc.TryGetValue(docCliente, out var cliExistente))
            {
                cliExistente.FullName = nomCliente;
                if (!string.IsNullOrWhiteSpace(telCliente)) cliExistente.Phone = telCliente;
                cliExistente.UpdatedAt = DateTimeOffset.UtcNow;
                result.ClientesActualizados++;
            }
            else
            {
                var emailFinal = string.IsNullOrWhiteSpace(emailCliente) ? $"{docCliente}@firmeza.com" : emailCliente;
                if (emailsExistentes.Contains(emailFinal))
                {
                    emailFinal = $"{docCliente}_{Guid.NewGuid().ToString()[..4]}@firmeza.com";
                }

                var nuevoCli = new Customer
                {
                    Id = Guid.NewGuid(),
                    Document = docCliente,
                    FullName = nomCliente,
                    Age = 18,
                    Email = emailFinal,
                    Phone = string.IsNullOrWhiteSpace(telCliente) ? "3000000000" : telCliente,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _db.Customers.Add(nuevoCli);
                clientesPorDoc[docCliente] = nuevoCli;
                emailsExistentes.Add(emailFinal);
                result.ClientesCreados++;
            }

            // Normalizar e insertar/actualizar Producto
            if (productosPorSku.TryGetValue(skuProducto, out var prodExistente))
            {
                prodExistente.Name = nomProducto;
                prodExistente.Price = precio;
                prodExistente.Stock = stock;
                if (!string.IsNullOrWhiteSpace(catProducto))
                {
                    prodExistente.Category = catProducto;
                }
                prodExistente.UpdatedAt = DateTimeOffset.UtcNow;
                result.ProductosActualizados++;
            }
            else
            {
                var nuevoProd = new Product
                {
                    Id = Guid.NewGuid(),
                    Sku = skuProducto,
                    Name = nomProducto,
                    Category = string.IsNullOrWhiteSpace(catProducto) ? "General" : catProducto,
                    Unit = "Unidad",
                    Price = precio,
                    Stock = stock,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _db.Products.Add(nuevoProd);
                productosPorSku[skuProducto] = nuevoProd;
                result.ProductosCreados++;
            }
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            result.Errores.Add($"Error al guardar en la base de datos: {ex.Message}");
        }

        return result;
    }
}