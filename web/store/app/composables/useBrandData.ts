// Brand page — port of MainMsController.Brand (Model = Brands, ViewBag.Products).
export interface BrandPhoto { sort: number; photo: string }
export interface BrandProduct {
  productid: string; title: string; entitle?: string; capacity?: string
  photo?: string; price: number; fixprice: number
}
export interface BrandDetail {
  brandid: string; title: string; subtitle?: string
  banner?: string; logo?: string; ilogo?: string
  patternentitle?: string; patternchtitle?: string; parttnervideo?: string
  patternmemo?: string; patternclass?: string
  storybgclass?: string; slogan?: string; intro?: string
  storyentitle?: string; storychtitle?: string; storymemo?: string
  peoplephoto?: string; peopletitle?: string; peopleslogan?: string; peoplememo?: string
  photos: BrandPhoto[]
}
export interface BrandData {
  blobUrl: string
  brand: BrandDetail | null
  products: BrandProduct[]
  hasMore: boolean
}

export function useBrandData(brandtitle: string) {
  const config = useRuntimeConfig()
  return useFetch<BrandData>(`${config.public.apiBase}/store/brands/detail`, {
    key: `brand:${brandtitle}`,
    query: { brandtitle },
    default: (): BrandData => ({ blobUrl: '', brand: null, products: [], hasMore: false }),
  })
}
