// 後台共用圖片上傳：POST /admin/upload（multipart，field 名稱 file）→ 回傳 blob 檔名。
// 檔名格式與舊系統一致（yyyyMMddHHmmssff）。預覽請用 toBlobUrl(fileName)。
import { apiFetch } from './apiClient'

export async function uploadImage(file: File): Promise<string> {
  const fd = new FormData()
  fd.append('file', file)
  const { fileName } = await apiFetch<{ fileName: string }>('/admin/upload', { method: 'POST', body: fd })
  return fileName
}
