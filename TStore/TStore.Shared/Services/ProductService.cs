using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TStore.Shared.Models;
using TStore.Shared.Repositories;

namespace TStore.Shared.Services
{
    public interface IProductService
    {
        Task<IEnumerable<ProductModel>> GetProductsAsync(SimpleFilterModel filter);
    }

    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;

        public ProductService(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<IEnumerable<ProductModel>> GetProductsAsync(SimpleFilterModel filter)
        {
            IQueryable<Entities.Product> query = _productRepository.Get();

            if (!string.IsNullOrWhiteSpace(filter.Terms))
            {
                query = query.Where(p => p.Name.Contains(filter.Terms));
            }


            if (filter.PageSize != null && filter.Page != null)
            {
                query = query.Skip(filter.Page.Value * filter.PageSize.Value).Take(filter.PageSize.Value);
            }

            ProductModel[] products = await query
                .Select(p => new ProductModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price
                }).ToArrayAsync();

            return products;
        }
    }
}
