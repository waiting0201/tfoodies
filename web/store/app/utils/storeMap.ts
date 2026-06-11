// Adapter layer between the Store API (clean camelCase DTOs) and the legacy view-model
// field names the pages + ProductCard bind to (entitle, fixprice, isset, isdisabled…).
// Image fields stay bare filenames — pages prepend runtimeConfig.public.blobUrl, exactly
// as the legacy views prepended ViewBag.BlobUrl. Centralised so every composable that
// renders products/dates maps identically.

/** Store API product list item (System.Text.Json CamelCase of ProductListItem). */
export interface ApiProduct {
  productId: string
  title: string
  enTitle?: string | null
  price: number
  fixPrice?: number | null
  photo: string
  capacity?: string | null
  isHot: boolean
  isNew: boolean
  isSet: boolean
  isDisabled: boolean
  added: number
  sort: number
  brandTitle: string
  typeTitle: string
  shortener?: string | null
}

/** Legacy view-model product (what ProductCard + related-product sections consume). */
export interface ViewProduct {
  productid: string
  title: string
  entitle?: string
  capacity?: string
  photo?: string
  price: number
  fixprice: number
  isset: boolean
  isdisabled: boolean
  sort: number
}

export function mapProduct(p: ApiProduct): ViewProduct {
  return {
    productid: p.productId,
    title: p.title,
    entitle: p.enTitle ?? undefined,
    capacity: p.capacity ?? undefined,
    photo: p.photo,
    price: p.price,
    fixprice: p.fixPrice ?? 0,
    isset: p.isSet,
    isdisabled: p.isDisabled,
    sort: p.sort,
  }
}

/** Compact recipe reference (RecipeRef) → legacy {recipeid,title,rphoto}. */
export interface ApiRecipeRef { recipeId: string; title: string; rPhoto?: string | null }
export function mapRecipeRef(r: ApiRecipeRef) {
  return { recipeid: r.recipeId, title: r.title, rphoto: r.rPhoto ?? undefined }
}

/** ISO datetime → yyyy-MM-dd (legacy display format, same as .ToString("yyyy-MM-dd")). */
export function ymd(iso?: string | null): string {
  return iso ? String(iso).slice(0, 10) : ''
}

/** Bare string[] photo list → legacy {sort,photo}[] (sort = original order). */
export function mapPhotos(photos?: (string | null)[] | null) {
  return (photos ?? []).map((photo, sort) => ({ sort, photo: photo ?? '' }))
}
