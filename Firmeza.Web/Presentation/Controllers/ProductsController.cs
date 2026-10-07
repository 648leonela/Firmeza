using Firmeza.Domain.Identity;
using Firmeza.Infrastructure.Identity;
using Firmeza.Infrastructure.Persistence;
using Firmeza.Domain.Entities;
using Firmeza.Web.Presentation.ViewModels.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Firmeza.Web.Services;

namespace Firmeza.Web.Presentation.Controllers;

[Authorize(Roles = ApplicationRoles.Administrator)]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ReceiptPdfService _pdfService;

    public ProductsController(ApplicationDbContext db, ReceiptPdfService pdfService)
    {
        _db = db;
        _pdfService = pdfService;
    }

    public async Task<IActionResult> Index(string? q, bool soloActivos = false)
    {
        var query = _db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(product => product.Name.Contains(term) || product.Sku.Contains(term) || product.Category.Contains(term));
        }

        if (soloActivos)
        {
            query = query.Where(product => product.IsActive);
        }

        ViewData["Query"] = q;
        ViewData["SoloActivos"] = soloActivos;
        var products = await query.OrderByDescending(product => product.IsActive).ThenBy(product => product.Name).Select(product => new ProductListItemViewModel
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            Category = product.Category,
            Unit = product.Unit,
            Price = product.Price,
            Stock = product.Stock,
            IsActive = product.IsActive
        }).ToListAsync();

        return View(products);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel()
    {
        var products = await _db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        var bytes = _pdfService.ExportProductsToExcel(products);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Productos_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf()
    {
        var products = await _db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        var bytes = _pdfService.ExportProductsToPdf(products);
        return File(bytes, "application/pdf", $"Productos_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
    }

    [HttpGet]
    public IActionResult Create() => View(new ProductFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel model)
    {
        await ValidateSkuAsync(model.Sku, null);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var product = new Product();
        Apply(model, product);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        TempData["Message"] = "Producto creado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var product = await _db.Products.FindAsync(id);
        return product is null ? NotFound() : View(ToForm(product));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ProductFormViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        await ValidateSkuAsync(model.Sku, model.Id);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var product = await _db.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        Apply(model, product);
        product.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Message"] = "Producto actualizado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null)
        {
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
            TempData["Message"] = "Producto eliminado correctamente.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "No se puede eliminar un producto que tiene ventas asociadas.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateSkuAsync(string sku, Guid? excludedId)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        if (await _db.Products.AnyAsync(product => product.Sku == normalized && (excludedId == null || product.Id != excludedId)))
        {
            ModelState.AddModelError(nameof(ProductFormViewModel.Sku), "Ya existe un producto con ese código.");
        }
    }

    private static void Apply(ProductFormViewModel model, Product product)
    {
        product.Sku = model.Sku.Trim().ToUpperInvariant();
        product.Name = model.Name.Trim();
        product.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        product.Category = model.Category.Trim();
        product.Unit = model.Unit.Trim();
        product.Price = model.Price;
        product.Stock = model.Stock;
        product.IsActive = model.IsActive;
    }

    private static ProductFormViewModel ToForm(Product product) => new()
    {
        Id = product.Id,
        Sku = product.Sku,
        Name = product.Name,
        Description = product.Description,
        Category = product.Category,
        Unit = product.Unit,
        Price = product.Price,
        Stock = product.Stock,
        IsActive = product.IsActive
    };
}
