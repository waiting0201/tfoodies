// Thin fetch wrapper for the single-entry Functions API (/api/admin/*, /api/auth/*).
// A typed client will be generated from the API's OpenAPI doc later; this is the runtime
// it will sit on. Attaches the JWT access token and surfaces ProblemDetails errors.

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:7071/api'

let accessTokenProvider: () => string | null = () => null

export function setAccessTokenProvider(fn: () => string | null) {
  accessTokenProvider = fn
}

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  correlationId?: string
}

export class ApiError extends Error {
  status: number
  problem: ProblemDetails
  constructor(status: number, problem: ProblemDetails) {
    super(problem.title ?? `HTTP ${status}`)
    this.status = status
    this.problem = problem
  }
}

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = accessTokenProvider()
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  if (init.body && !(init.body instanceof FormData) && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json')
  if (token) headers.set('Authorization', `Bearer ${token}`)

  const res = await fetch(`${API_BASE}${path}`, { ...init, headers, credentials: 'include' })

  if (!res.ok) {
    let problem: ProblemDetails = { status: res.status, title: res.statusText }
    try {
      problem = { ...problem, ...(await res.json()) }
    } catch {
      /* non-JSON error body */
    }
    throw new ApiError(res.status, problem)
  }

  return (res.status === 204 ? undefined : await res.json()) as T
}

/**
 * 下載檔案（Excel 匯出等）。帶上 JWT，將回應以 Blob 觸發瀏覽器下載。
 * 伺服器若回傳 JSON 錯誤，會解析為 ApiError。
 */
export async function apiDownload(path: string, fallbackName = 'download'): Promise<void> {
  const token = accessTokenProvider()
  const headers = new Headers()
  if (token) headers.set('Authorization', `Bearer ${token}`)

  const res = await fetch(`${API_BASE}${path}`, { headers, credentials: 'include' })
  if (!res.ok) {
    let problem: ProblemDetails = { status: res.status, title: res.statusText }
    try { problem = { ...problem, ...(await res.json()) } } catch { /* non-JSON */ }
    throw new ApiError(res.status, problem)
  }

  // 從 Content-Disposition 取檔名（後端會帶），否則用 fallback。
  const cd = res.headers.get('Content-Disposition') ?? ''
  const match = /filename\*?=(?:UTF-8'')?["']?([^"';]+)/i.exec(cd)
  const fileName = match ? decodeURIComponent(match[1]) : fallbackName

  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(url)
}
