using System.Collections.Generic;

namespace Firmeza.Web.Presentation.ViewModels;

public class ImportResultViewModel
{
    public int TotalFilas { get; set; }
    public int ClientesCreados { get; set; }
    public int ClientesActualizados { get; set; }
    public int ProductosCreados { get; set; }
    public int ProductosActualizados { get; set; }
    public List<string> Errores { get; set; } = new();
    public bool TieneErrores => Errores.Count > 0;
}
