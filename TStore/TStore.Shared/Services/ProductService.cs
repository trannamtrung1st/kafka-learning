using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TStore.Shared.Constants;
using TStore.Shared.Models;
using TStore.Shared.Repositories;

namespace TStore.Shared.Services
{
    public interface IProductService
    {
        Task<IEnumerable<ProductModel>> GetProductsAsync(SimpleFilterModel filter);
        Task UpdateProductAsync(ProductModel model);
    }

    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly ICommonMessagePublisher _messagePublisher;

        public ProductService(IProductRepository productRepository,
            ICommonMessagePublisher messagePublisher)
        {
            _productRepository = productRepository;
            _messagePublisher = messagePublisher;
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

        public async Task UpdateProductAsync(ProductModel model)
        {
            Entities.Product productEntity = new Entities.Product
            {
                Id = model.Id,
                Name = model.Name,
                Price = model.Price
            };

            _productRepository.Update(productEntity);

            await _productRepository.UnitOfWork.SaveChangesAsync();

            await _messagePublisher.PublishAndWaitAsync(
                EventConstants.Events.ProductUpdated,
                productEntity.Id.ToString(),
                model);
        }
    }
}
