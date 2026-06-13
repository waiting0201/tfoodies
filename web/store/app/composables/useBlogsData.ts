// Blogs (部落客分享) — GET /store/blogs. Non-paginated list ordered by sort; each card
// links to an external blog article. Mirrors old MainMs/Blogs.cshtml (ViewBag.Blogs).
export interface BlogItem {
  blogid: string; title: string; photo?: string; link: string
}
export interface BlogsData {
  blobUrl: string
  items: BlogItem[]
}

interface ApiBlogItem { blogId: string; title: string; photo: string; link: string }

export function useBlogsData() {
  const blobUrl = useRuntimeConfig().public.blobUrl as string
  return useFetch(`${useRuntimeConfig().public.apiBase}/store/blogs`, {
    key: 'blogs',
    default: (): BlogsData => ({ blobUrl, items: [] }),
    transform: (api: ApiBlogItem[]): BlogsData => ({
      blobUrl,
      items: (api ?? []).map(b => ({ blogid: b.blogId, title: b.title, photo: b.photo, link: b.link })),
    }),
  })
}
