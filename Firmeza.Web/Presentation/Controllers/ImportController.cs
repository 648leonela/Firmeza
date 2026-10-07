using System;
using System.IO;
using System.Threading.Tasks;
using Firmeza.Domain.Identity;
using Firmeza.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Firmeza.Web.Presentation.Controllers;

[Authorize(Roles = ApplicationRoles.Administrator)]
public class ImportController : Controller
{
    private readonly ExcelImportService _importService;

    public ImportController(ExcelImportService importService)
    {
        _importService = importService;
    }

    [HttpGet]
    public IActionResult Index() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError("", "Por favor selecciona un archivo Excel (.xlsx).");
            return View();
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("", "El formato del archivo debe ser .xlsx.");
            return View();
        }

        using var stream = file.OpenReadStream();
        var resultado = await _importService.ImportarDesnormalizadoAsync(stream);

        return View("Result", resultado);
    }
}
