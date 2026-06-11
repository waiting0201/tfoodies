// Products listing — GET /store/products[?producttypetitle=]. Adapts ProductsPage
// (productTypes + currentType + products) into the legacy view-model.
export interface ProductType { producttypeid: string; title: string; memo?: string }
export interface ProductsData {
  blobUrl: string
  producttypes: ProductType[]
  currentType: ProductType | null
  products: ViewProduct[]
}

interface ApiProductType { productTypeId: string; title: string; memo?: string | null }
interface ApiProductsPage {
  productTypes: ApiProductType[]
  currentType: ApiProductType | null
  products: ApiProduct[]
}

const mapType = (t: ApiProductType): ProductType => ({ producttypeid: t.productTypeId, title: t.title, memo: t.memo ?? undefined })

export function useProductsData(producttypetitle?: string) {
  const blobUrl = useRuntimeConfig().public.blobUrl as string
  return useFetch(`${useRuntimeConfig().public.apiBase}/store/products`, {
    key: `products:${producttypetitle ?? 'all'}`,
    query: producttypetitle ? { producttypetitle } : {},
    default: (): ProductsData => ({ blobUrl, producttypes: [], currentType: null, products: [] }),
    transform: (api: ApiProductsPage): ProductsData => ({
      blobUrl,
      producttypes: api.productTypes.map(mapType),
      currentType: api.currentType ? mapType(api.currentType) : null,
      products: api.products.map(mapProduct),
    }),
  })
}
