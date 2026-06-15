// Google reCAPTCHA v3 client helper（隱形驗證，無圖形輸入框）。
// 用於前台公開寫入端點（目前：缺貨「到貨通知我」登記）。
//
// 設計：
// - 僅在第一次需要 token 時才注入 https://www.google.com/recaptcha/api.js?render=<siteKey>，
//   避免每頁都載入 Google script。
// - 未設定 site key（runtimeConfig.public.recaptchaSiteKey 為空）→ execute() 回傳空字串，
//   後端 verifier 在 SecretKey 也未設定時會放行，功能仍可端到端運作。
declare global {
  interface Window {
    grecaptcha?: {
      ready: (cb: () => void) => void
      execute: (siteKey: string, opts: { action: string }) => Promise<string>
    }
  }
}

let scriptPromise: Promise<void> | null = null

function loadScript(siteKey: string): Promise<void> {
  if (window.grecaptcha) return Promise.resolve()
  if (scriptPromise) return scriptPromise

  scriptPromise = new Promise<void>((resolve, reject) => {
    const s = document.createElement('script')
    s.src = `https://www.google.com/recaptcha/api.js?render=${encodeURIComponent(siteKey)}`
    s.async = true
    s.defer = true
    s.onload = () => resolve()
    s.onerror = () => { scriptPromise = null; reject(new Error('reCAPTCHA script 載入失敗')) }
    document.head.appendChild(s)
  })
  return scriptPromise
}

export function useRecaptcha() {
  const siteKey = (useRuntimeConfig().public.recaptchaSiteKey as string) || ''

  /**
   * 取得指定 action 的 reCAPTCHA v3 token。未設定 site key 或載入失敗時回傳空字串
   * （讓呼叫端仍可送出；驗證強度由後端決定）。
   */
  async function execute(action: string): Promise<string> {
    if (!import.meta.client || !siteKey) return ''
    try {
      await loadScript(siteKey)
      return await new Promise<string>((resolve) => {
        window.grecaptcha!.ready(() => {
          window.grecaptcha!.execute(siteKey, { action }).then(resolve).catch(() => resolve(''))
        })
      })
    } catch {
      return ''
    }
  }

  return { execute }
}
