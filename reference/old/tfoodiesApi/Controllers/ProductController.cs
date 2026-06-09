using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using tfoodies.Models;
using tfoodies.Service;

namespace tfoodiesApi.Controllers
{
    public class ProductController : BaseController
    {
        private IProductsService productsService;

        public ProductController()
        {
            productsService = new ProductsService();
        }

        public SearchProductResponse Get(string title = null, int skip = 0, int take = 10)
        {
            SearchProductResponse response = new SearchProductResponse();

            IEnumerable<Products> datas = productsService.Get();
            if (title != null) datas = datas.Where(a => a.title.Contains(title));
            int total = datas.Count();

            datas = datas.Skip(skip).Take(take);
            int returned = datas.Count();

            if(returned > 0)
            {
                response.code = "200";
                response.datas = datas;
                response.paging = new PagingResponse
                {
                    total = total,
                    returned = returned,
                    skip = skip,
                    take = take
                };
            }
            else
            {
                response.code = "202";
                response.message = "no datas";
            }

            return response;
        }
    }
}
