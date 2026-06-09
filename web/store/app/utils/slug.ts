// Legacy slug convention (reference/old/tfoodies/Controllers/MainMsController.cs:90,108,137,280):
// the DB `title` is stored with "/", the URL uses "-". A request for /Product/olivar-oil-premium
// queries title == "olivar/oil/premium". Preserved EXACTLY so indexed URLs keep resolving.

/** URL segment (hyphens) -> DB title (slashes). */
export function urlSlugToTitle(slug: string): string {
  return slug.replaceAll('-', '/')
}

/** DB title (slashes) -> URL segment (hyphens). */
export function titleToUrlSlug(title: string): string {
  return title.replaceAll('/', '-')
}
