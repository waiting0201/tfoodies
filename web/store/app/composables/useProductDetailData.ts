// Product detail — port of MainMsController.ProductDetail (Model = Products, ViewBag.Products for set).
export interface ProductPhoto { sort: number; photo: string }
export interface ProductRecipe { recipeid: string; title: string; rphoto?: string }
export interface ProductBrand {
  brandid: string; title: string; intro?: string; storybgclass?: string
  isdisplay: number
}
export interface ProductDetail {
  productid: string; title: string; entitle?: string; capacity?: string
  photo?: string; photos: ProductPhoto[]
  price: number; fixprice: number; isset?: boolean
  added: number
  intro?: string; memo?: string
  shortener?: string
  producttypetitle?: string
  brand: ProductBrand
  recipes: ProductRecipe[]
}
export interface ProductDetailData {
  blobUrl: string
  product: ProductDetail | null
}

export function useProductDetailData(title: string) {
  const config = useRuntimeConfig()
  return useFetch<ProductDetailData>(`${config.public.apiBase}/store/products/detail`, {
    key: `product-detail:${title}`,
    query: { title },
    default: (): ProductDetailData => ({ blobUrl: '', product: null }),
  })
}
