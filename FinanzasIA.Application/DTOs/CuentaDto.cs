using FinanzasIA.Core.Enums;

namespace FinanzasIA.Application.DTOs;

public class CuentaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public TipoCuenta Tipo { get; set; }
}

public class CreateCuentaDto
{
    public string Nombre { get; set; } = string.Empty;
    public TipoCuenta Tipo { get; set; }
}

public class UpdateCuentaDto
{
    public string Nombre { get; set; } = string.Empty;
    public TipoCuenta Tipo { get; set; }
}
