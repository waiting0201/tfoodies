// Brand page — GET /store/brands/detail?brandtitle=. Adapts BrandDetail (camelCase +
// productCount) into the legacy view-model; hasMore = brand has more than the 4 shown.
export interface BrandPhoto { sort: number; photo: string }
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
  products: ViewProduct[]
  hasMore: boolean
}

interface ApiBrandDetail {
  brandId: string; title: string; subtitle?: string | null; logo?: string | null; banner?: string | null
  patternEnTitle?: string | null; patternChTitle?: string | null; partnerVideo?: string | null
  patternMemo?: string | null; patternClass?: string | null
  iLogo?: string | null; slogan?: string | null; intro?: string | null
  storyBgClass?: string | null; storyEnTitle?: string | null; storyChTitle?: string | null; storyMemo?: string | null
  peopleTitle?: string | null; peopleSlogan?: string | null; peopleMemo?: string | null; peoplePhoto?: string | null
  productCount: number
  brandPhotos: string[]
  products: ApiProduct[]
}

export function useBrandData(brandtitle: string) {
  const blobUrl = useRuntimeConfig().public.blobUrl as string
  return useFetch(`${useRuntimeConfig().public.apiBase}/store/brands/detail`, {
    key: `brand:${brandtitle}`,
    query: { brandtitle },
    default: (): BrandData => ({ blobUrl, brand: null, products: [], hasMore: false }),
    transform: (api: ApiBrandDetail): BrandData => ({
      blobUrl,
      brand: {
        brandid: api.brandId,
        title: api.title,
        subtitle: api.subtitle ?? undefined,
        banner: api.banner ?? undefined,
        logo: api.logo ?? undefined,
        ilogo: api.iLogo ?? undefined,
        patternentitle: api.patternEnTitle ?? undefined,
        patternchtitle: api.patternChTitle ?? undefined,
        parttnervideo: api.partnerVideo ?? undefined,
        patternmemo: api.patternMemo ?? undefined,
        patternclass: api.patternClass ?? undefined,
        storybgclass: api.storyBgClass ?? undefined,
        slogan: api.slogan ?? undefined,
        intro: api.intro ?? undefined,
        storyentitle: api.storyEnTitle ?? undefined,
        storychtitle: api.storyChTitle ?? undefined,
        storymemo: api.storyMemo ?? undefined,
        peoplephoto: api.peoplePhoto ?? undefined,
        peopletitle: api.peopleTitle ?? undefined,
        peopleslogan: api.peopleSlogan ?? undefined,
        peoplememo: api.peopleMemo ?? undefined,
        photos: mapPhotos(api.brandPhotos),
      },
      products: api.products.map(mapProduct),
      hasMore: api.productCount > 4,
    }),
  })
}
