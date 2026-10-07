using System;
using System.Linq;
using System.Threading.Tasks;
using Firmeza.Domain.Identity;
using Firmeza.Infrastructure.Identity;
using Firmeza.Infrastructure.Persistence;
using Firmeza.Web.Presentation.ViewModels.Sales;
using Firmeza.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Firmeza.Web.Presentation.Controllers;

[Authorize(Roles = ApplicationRoles.Administrator)]
public class SalesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ReceiptPdfService _receiptService;

    public SalesController(ApplicationDbContext db, ReceiptPdfService receiptService)
    {
        _db = db;
        _receiptService = receiptService;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var query = _db.Sales.AsNoTracking().Include(sale => sale.Customer).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(sale => sale.SaleNumber.Contains(term) || sale.Customer.FullName.Contains(term));
        }

        ViewData["Query"] = q;
        var sales = await query.OrderByDescending(sale => sale.SaleDate).Select(sale => new SaleListItemViewModel
        {
            Id = sale.Id,
            SaleNumber = sale.SaleNumber,
            CustomerName = sale.Customer.FullName,
            SaleDate = sale.SaleDate,
            Status = sale.Status.ToString(),
            Total = sale.Total
        }).ToListAsync();

        return View(sales);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel()
    {
        var sales = await _db.Sales.AsNoTracking().Include(s => s.Customer).OrderByDescending(s => s.SaleDate).ToListAsync();
        var bytes = _receiptService.ExportSalesToExcel(sales);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Ventas_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf()
    {
        var sales = await _db.Sales.AsNoTracking().Include(s => s.Customer).OrderByDescending(s => s.SaleDate).ToListAsync();
        var bytes = _receiptService.ExportSalesToPdf(sales);
        return File(bytes, "application/pdf", $"Ventas_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var sale = await _db.Sales
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Details)
            .ThenInclude(detail => detail.Product)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (sale is null)
        {
            return NotFound();
        }

        // Generar comprobante físico en wwwroot/recibos y obtener URL
        var receiptUrl = _receiptService.GenerateAndSaveSaleReceipt(sale);
        ViewData["ReceiptUrl"] = receiptUrl;

        var model = new SaleDetailsViewModel
        {
            Id = sale.Id,
            SaleNumber = sale.SaleNumber,
            CustomerName = sale.Customer.FullName,
            CustomerDocument = sale.Customer.Document,
            SaleDate = sale.SaleDate,
            Status = sale.Status.ToString(),
            Total = sale.Total,
            Details = sale.Details.Select(detail => new SaleDetailViewModel
            {
                ProductName = detail.Product.Name,
                ProductSku = detail.Product.Sku,
                Quantity = detail.Quantity,
                UnitPrice = detail.UnitPrice,
                Subtotal = detail.Subtotal
            }).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadReceipt(Guid id)
    {
        var sale = await _db.Sales
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Details)
            .ThenInclude(detail => detail.Product)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (sale is null)
        {
            return NotFound();
        }

        var pdfBytes = _receiptService.GenerateSaleReceiptBytes(sale);
        return File(pdfBytes, "application/pdf", $"recibo_{sale.SaleNumber}.pdf");
    }
}
