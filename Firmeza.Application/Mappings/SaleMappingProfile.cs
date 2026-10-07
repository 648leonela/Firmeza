using AutoMapper;
using Firmeza.Application.Dtos.Sales;
using Firmeza.Domain.Entities;

namespace Firmeza.Application.Mappings;

public sealed class SaleMappingProfile : Profile
{
    public SaleMappingProfile()
    {
        CreateMap<Sale, SaleSummaryResponse>()
            .ForCtorParam(nameof(SaleSummaryResponse.CustomerName), opt => opt.MapFrom(src => src.Customer != null ? src.Customer.FullName : string.Empty))
            .ForCtorParam(nameof(SaleSummaryResponse.LineCount), opt => opt.MapFrom(src => src.Details != null ? src.Details.Count : 0));

        CreateMap<SaleDetail, SaleLineResponse>()
            .ForCtorParam(nameof(SaleLineResponse.ProductSku), opt => opt.MapFrom(src => src.Product != null ? src.Product.Sku : string.Empty))
            .ForCtorParam(nameof(SaleLineResponse.ProductName), opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : string.Empty));

        CreateMap<Sale, SaleResponse>()
            .ForCtorParam(nameof(SaleResponse.CustomerName), opt => opt.MapFrom(src => src.Customer != null ? src.Customer.FullName : string.Empty))
            .ForCtorParam(nameof(SaleResponse.CustomerDocument), opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Document : string.Empty))
            .ForCtorParam(nameof(SaleResponse.Lines), opt => opt.MapFrom(src => src.Details));
    }
}
