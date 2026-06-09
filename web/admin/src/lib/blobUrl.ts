// 對應後端 AzureBlobOptions.BlobUrl = BaseUrl + "/" + ContainerName
// 與舊系統 ViewBag.BlobUrl（azure.blob.url + "/" + azure.blob.container）邏輯一致
const blobBase = [
  (import.meta.env.VITE_BLOB_URL as string ?? '').replace(/\/$/, ''),
  (import.meta.env.VITE_BLOB_CONTAINER as string ?? '').replace(/^\//, ''),
].filter(Boolean).join('/')

export function toBlobUrl(photo: string | null | undefined): string {
  if (!photo) return ''
  if (photo.startsWith('http://') || photo.startsWith('https://')) return photo
  return `${blobBase}/${photo}`
}
