using AutoMapper;
using Firmeza.Application.Abstractions;
using Firmeza.Application.Common;
using Firmeza.Application.Dtos.Customers;
using Firmeza.Application.Mappings;
using Firmeza.Domain.Entities;

namespace Firmeza.Application.Services.Customers;

public sealed class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IMapper _mapper;

    public CustomerService(
        ICustomerRepository customers,
        IUnitOfWork unitOfWork,
        IClock clock,
        IMapper? mapper = null)
    {
        _customers = customers;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _mapper = mapper ?? new MapperConfiguration(cfg => cfg.AddProfile<CustomerMappingProfile>()).CreateMapper();
    }

    public async Task<Result<PagedResponse<CustomerResponse>>> ListAsync(CustomerQuery query, CancellationToken cancellationToken = default)
    {
        var page = new PageRequest { Page = query.Page, PageSize = query.PageSize };
        var result = await _customers.ListAsync(new CustomerFilter(query.Q?.Trim(), query.OnlyActive), page, cancellationToken);
        var items = _mapper.Map<List<CustomerResponse>>(result.Items);

        return Result.Success(new PagedResponse<CustomerResponse>
        {
            Items = items,
            Page = page.Page,
            PageSize = page.Take,
            TotalCount = result.TotalCount
        });
    }

    public async Task<Result<CustomerResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _customers.FindByIdAsync(id, cancellationToken);

        return customer is null
            ? Result.Failure<CustomerResponse>(Error.EntityNotFound("Cliente", id))
            : Result.Success(_mapper.Map<CustomerResponse>(customer));
    }

    public async Task<Result<CustomerResponse>> CreateAsync(CustomerRequest request, CancellationToken cancellationToken = default)
    {
        var document = request.Document.Trim();
        var email = NormalizeEmail(request.Email);

        if (await _customers.DocumentExistsAsync(document, null, cancellationToken))
        {
            return Result.Failure<CustomerResponse>(Error.Conflict("Ya existe un cliente con ese documento."));
        }

        if (await _customers.EmailExistsAsync(email, null, cancellationToken))
        {
            return Result.Failure<CustomerResponse>(Error.Conflict("Ya existe un cliente con ese correo."));
        }

        var customer = _mapper.Map<Customer>(request);
        customer.Document = document;
        customer.Email = email;
        customer.CreatedAt = _clock.UtcNow;

        await _customers.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(_mapper.Map<CustomerResponse>(customer));
    }

    public async Task<Result<CustomerResponse>> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await _customers.FindByIdAsync(id, cancellationToken);
        if (customer is null)
        {
            return Result.Failure<CustomerResponse>(Error.EntityNotFound("Cliente", id));
        }

        var document = request.Document.Trim();
        var email = NormalizeEmail(request.Email);

        if (await _customers.DocumentExistsAsync(document, id, cancellationToken))
        {
            return Result.Failure<CustomerResponse>(Error.Conflict("Ya existe un cliente con ese documento."));
        }

        if (await _customers.EmailExistsAsync(email, id, cancellationToken))
        {
            return Result.Failure<CustomerResponse>(Error.Conflict("Ya existe un cliente con ese correo."));
        }

        _mapper.Map(request, customer);
        customer.Document = document;
        customer.Email = email;
        customer.UpdatedAt = _clock.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(_mapper.Map<CustomerResponse>(customer));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _customers.FindByIdAsync(id, cancellationToken);
        if (customer is null)
        {
            return Result.Success();
        }

        if (await _customers.HasSalesAsync(id, cancellationToken))
        {
            return Result.Failure(Error.Conflict("No se puede eliminar un cliente que tiene ventas asociadas."));
        }

        _customers.Remove(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
