// Events listing — port of MainMsController.Events (ViewBag.Events = IPagedList<Events>).
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

export function useEventsData(p: number = 1) {
  const config = useRuntimeConfig()
  return useFetch<EventsData>(`${config.public.apiBase}/store/events`, {
    key: `events:${p}`,
    query: { p },
    default: (): EventsData => ({ blobUrl: '', items: [], currentPage: p, totalPages: 1 }),
  })
}

// Event detail — port of MainMsController.EventsDetail (Model = Events + Eventphotos).
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

export function useEventDetailData(eventid: string, p: number = 1) {
  const config = useRuntimeConfig()
  return useFetch<EventDetailData>(`${config.public.apiBase}/store/events/detail`, {
    key: `event-detail:${eventid}`,
    query: { eventid, p },
    default: (): EventDetailData => ({ blobUrl: '', item: null, pageNumber: p }),
  })
}
