// Products listing — port of MainMsController.Products (ViewBag.Producttypes/Brands + Model.Products).
export interface ProductType { producttypeid: string; title: string; memo?: string }
export interface ProductListItem {
  productid: string; title: string; entitle?: string; capacity?: string
  photo?: string; price: number; fixprice: number; isset?: boolean
  brandid?: string; sort?: number
}
export interface ProductsData {
  blobUrl: string
  producttypes: ProductType[]
  currentType: ProductType | null
  products: ProductListItem[]
}

export function useProductsData(producttypetitle?: string) {
  const config = useRuntimeConfig()
  return useFetch<ProductsData>(`${config.public.apiBase}/store/products`, {
    key: `products:${producttypetitle ?? 'all'}`,
    query: producttypetitle ? { producttypetitle } : {},
    default: (): ProductsData => ({
      blobUrl: '', producttypes: [], currentType: null, products: [],
    }),
  })
}
