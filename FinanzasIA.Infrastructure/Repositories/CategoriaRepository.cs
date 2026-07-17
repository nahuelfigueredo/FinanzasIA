using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Interfaces;
using FinanzasIA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanzasIA.Infrastructure.Repositories;

public class CategoriaRepository : ICategoriaRepository
{
    private readonly FinanzasDbContext _context;

    public CategoriaRepository(FinanzasDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<Categoria>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Categorias.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(usuarioId))
        {
            query = query.Where(x => x.UsuarioId == usuarioId || x.UsuarioId == null);
        }

        return await query
            .OrderBy(x => x.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<Categoria?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Categorias
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Categoria> AddAsync(Categoria categoria, CancellationToken cancellationToken = default)
    {
        await _context.Categorias.AddAsync(categoria, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return categoria;
    }

    public async Task UpdateAsync(Categoria categoria, CancellationToken cancellationToken = default)
    {
        categoria.FechaModificacion = DateTime.UtcNow;
        _context.Categorias.Update(categoria);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Categoria categoria, CancellationToken cancellationToken = default)
    {
        _context.Categorias.Remove(categoria);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Categorias.AnyAsync(x => x.Id == id, cancellationToken);
    }
}
