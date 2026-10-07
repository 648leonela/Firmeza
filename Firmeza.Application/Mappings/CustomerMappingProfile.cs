using AutoMapper;
using Firmeza.Application.Dtos.Customers;
using Firmeza.Domain.Entities;

namespace Firmeza.Application.Mappings;

public sealed class CustomerMappingProfile : Profile
{
    public CustomerMappingProfile()
    {
        CreateMap<Customer, CustomerResponse>();
        CreateMap<CustomerRequest, Customer>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Sales, opt => opt.Ignore());
    }
}
