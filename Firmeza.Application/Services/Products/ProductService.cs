using AutoMapper;
using Firmeza.Application.Abstractions;
using Firmeza.Application.Common;
using Firmeza.Application.Dtos.Products;
using Firmeza.Application.Mappings;
using Firmeza.Domain.Entities;

namespace Firmeza.Application.Services.Products;

public sealed class ProductService : IProductService
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IMapper _mapper;

    public ProductService(
        IProductRepository products,
        IUnitOfWork unitOfWork,
        IClock clock,
        IMapper? mapper = null)
    {
        _products = products;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _mapper = mapper ?? new MapperConfiguration(cfg => cfg.AddProfile<ProductMappingProfile>()).CreateMapper();
    }

    public async Task<Result<PagedResponse<ProductResponse>>> ListAsync(ProductQuery query, CancellationToken cancellationToken = default)
    {
        var page = ToPageRequest(query);
        var result = await _products.ListAsync(new ProductFilter(query.Q?.Trim(), query.OnlyActive), page, cancellationToken);
        var items = _mapper.Map<List<ProductResponse>>(result.Items);

        return Result.Success(new PagedResponse<ProductResponse>
        {
            Items = items,
            Page = page.Page,
            PageSize = page.Take,
            TotalCount = result.TotalCount
        });
    }

    public async Task<Result<ProductResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _products.FindByIdAsync(id, cancellationToken);

        return product is null
            ? Result.Failure<ProductResponse>(Error.EntityNotFound("Producto", id))
            : Result.Success(_mapper.Map<ProductResponse>(product));
    }

    public async Task<Result<ProductResponse>> CreateAsync(ProductRequest request, CancellationToken cancellationToken = default)
    {
        var sku = NormalizeSku(request.Sku);
        if (await _products.SkuExistsAsync(sku, null, cancellationToken))
        {
            return Result.Failure<ProductResponse>(DuplicateSku());
        }

        var product = _mapper.Map<Product>(request);
        product.Sku = sku;
        product.CreatedAt = _clock.UtcNow;

        await _products.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(_mapper.Map<ProductResponse>(product));
    }

    public async Task<Result<ProductResponse>> UpdateAsync(Guid id, ProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _products.FindByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return Result.Failure<ProductResponse>(Error.EntityNotFound("Producto", id));
        }

        var sku = NormalizeSku(request.Sku);
        if (await _products.SkuExistsAsync(sku, id, cancellationToken))
        {
            return Result.Failure<ProductResponse>(DuplicateSku());
        }

        _mapper.Map(request, product);
        product.Sku = sku;
        product.UpdatedAt = _clock.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(_mapper.Map<ProductResponse>(product));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _products.FindByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return Result.Success();
        }

        if (await _products.HasSalesAsync(id, cancellationToken))
        {
            return Result.Failure(Error.Conflict("No se puede eliminar un producto que tiene ventas asociadas."));
        }

        _products.Remove(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    internal static PageRequest ToPageRequest(ProductQuery query) => new() { Page = query.Page, PageSize = query.PageSize };

    private static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    private static Error DuplicateSku() => Error.Conflict("Ya existe un producto con ese codigo.");
}
