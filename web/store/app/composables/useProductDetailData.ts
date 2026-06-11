// Product detail — GET /store/products/detail?title=. Adapts ProductDetail (brand
// summary + photos[] + related recipes) into the legacy view-model the page binds.
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

interface ApiProductDetail {
  productId: string; title: string; enTitle?: string | null; intro?: string | null; memo: string
  price: number; fixPrice?: number | null; capacity?: string | null; unit?: string | null
  photo: string; isHot: boolean; isNew: boolean; isSet: boolean; isGroupBuy: boolean; added: number
  keyword?: string | null; description?: string | null; shortener?: string | null
  brand: { brandId: string; title: string; logo?: string | null; intro?: string | null; storyBgClass?: string | null; isDisplay: number }
  typeTitle: string
  photos: string[]
  recipes: ApiRecipeRef[]
}

export function useProductDetailData(title: string) {
  const blobUrl = useRuntimeConfig().public.blobUrl as string
  return useFetch(`${useRuntimeConfig().public.apiBase}/store/products/detail`, {
    key: `product-detail:${title}`,
    query: { title },
    default: (): ProductDetailData => ({ blobUrl, product: null }),
    transform: (api: ApiProductDetail): ProductDetailData => ({
      blobUrl,
      product: {
        productid: api.productId,
        title: api.title,
        entitle: api.enTitle ?? undefined,
        capacity: api.capacity ?? undefined,
        photo: api.photo,
        photos: mapPhotos(api.photos),
        price: api.price,
        fixprice: api.fixPrice ?? 0,
        isset: api.isSet,
        added: api.added,
        intro: api.intro ?? undefined,
        memo: api.memo,
        shortener: api.shortener ?? undefined,
        producttypetitle: api.typeTitle,
        brand: {
          brandid: api.brand.brandId,
          title: api.brand.title,
          intro: api.brand.intro ?? undefined,
          storybgclass: api.brand.storyBgClass ?? undefined,
          isdisplay: api.brand.isDisplay,
        },
        recipes: api.recipes.map(mapRecipeRef),
      },
    }),
  })
}
