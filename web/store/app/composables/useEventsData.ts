// Events listing — GET /store/events?p= (PaginatedResponse<EventListItem>).
export interface EventItem {
  eventid: string; title: string; photo?: string; summary?: string
  eventdate: string
}
export interface EventsData {
  blobUrl: string
  items: EventItem[]
  currentPage: number
  totalPages: number
}

interface ApiEventListItem { eventId: string; title: string; summary: string; photo: string; eventDate: string }
interface ApiPaged<T> { items: T[]; page: number; totalPages: number }

export function useEventsData(p: number = 1) {
  const blobUrl = useRuntimeConfig().public.blobUrl as string
  return useFetch(`${useRuntimeConfig().public.apiBase}/store/events`, {
    key: `events:${p}`,
    query: { p },
    default: (): EventsData => ({ blobUrl, items: [], currentPage: p, totalPages: 1 }),
    transform: (api: ApiPaged<ApiEventListItem>): EventsData => ({
      blobUrl,
      items: api.items.map(e => ({
        eventid: e.eventId, title: e.title, photo: e.photo,
        summary: e.summary, eventdate: ymd(e.eventDate),
      })),
      currentPage: api.page,
      totalPages: api.totalPages,
    }),
  })
}

// Event detail — GET /store/events/detail?eventid= (EventDetail + eventPhotos).
export interface EventPhoto { sort: number; photo: string }
export interface EventDetail {
  eventid: string; title: string; intro?: string
  eventdate: string; shortener?: string
  photos: EventPhoto[]
}
export interface EventDetailData {
  blobUrl: string
  item: EventDetail | null
  pageNumber: number
}

interface ApiEventDetail {
  eventId: string; title: string; summary: string; intro: string; photo: string
  keyword?: string | null; description?: string | null; eventDate: string; createDate: string; shortener?: string | null
  eventPhotos: string[]
}

export function useEventDetailData(eventid: string, p: number = 1) {
  const blobUrl = useRuntimeConfig().public.blobUrl as string
  return useFetch(`${useRuntimeConfig().public.apiBase}/store/events/detail`, {
    key: `event-detail:${eventid}`,
    query: { eventid, p },
    default: (): EventDetailData => ({ blobUrl, item: null, pageNumber: p }),
    transform: (api: ApiEventDetail): EventDetailData => ({
      blobUrl,
      item: {
        eventid: api.eventId,
        title: api.title,
        intro: api.intro,
        eventdate: ymd(api.eventDate),
        shortener: api.shortener ?? undefined,
        photos: mapPhotos(api.eventPhotos),
      },
      pageNumber: p,
    }),
  })
}
