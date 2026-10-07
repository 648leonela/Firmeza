using AutoMapper;
using Firmeza.Application.Abstractions;
using Firmeza.Application.Mappings;
using Firmeza.Application.Services.Auth;
using Firmeza.Application.Services.Customers;
using Firmeza.Application.Services.Dashboard;
using Firmeza.Application.Services.Products;
using Firmeza.Application.Services.Sales;
using Microsoft.Extensions.DependencyInjection;

namespace Firmeza.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.AddMaps(typeof(ProductMappingProfile).Assembly);
        });
        services.AddSingleton<IMapper>(mapperConfig.CreateMapper());

        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
