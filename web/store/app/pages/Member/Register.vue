<script setup lang="ts">
useHead({ title: '會員註冊' })

const config = useRuntimeConfig()

const form = reactive({
  mobile: '',
  name: '',
  email: '',
  password: '',
  confirmPassword: '',
})

const errorMsg = ref('')
const loading = ref(false)

// 與舊系統 ShoppingProfile.cshtml 的 jQuery validate 規則 + 新後端 Register 合約對齊：
// 手機 10 碼純數字（舊系統 minlength/maxlength=10 + number；沿用結帳頁的 09 開頭規則）、
// 姓名必填、密碼 6～20 字元、Email 必填且格式正確、兩次密碼需相同。
function clientValidate(): string | null {
  if (!/^09\d{8}$/.test(form.mobile.trim())) return '請輸入正確的手機格式，如：0987654321。'
  if (!form.name.trim()) return '請填寫姓名。'
  if (!form.email.trim()) return '請填寫電子郵件。'
  if (form.password.length < 6 || form.password.length > 20) return '請設定 6～20 字元的密碼。'
  if (form.password !== form.confirmPassword) return '兩次輸入的密碼不一致。'
  return null
}

async function submit() {
  errorMsg.value = ''
  const err = clientValidate()
  if (err) { errorMsg.value = err; return }
  loading.value = true
  try {
    await $fetch(`${config.public.apiBase}/auth/register`, {
      method: 'POST',
      // API 以 camelCase 大小寫敏感反序列化；PascalCase 會被當缺欄位回 400（同 login/profile 慣例）。
      body: {
        mobile: form.mobile.trim(),
        name: form.name.trim(),
        email: form.email.trim(),
        password: form.password,
      },
    })
    await navigateTo('/Member/Login')
  } catch (e: unknown) {
    const err = e as { data?: { message?: string } }
    errorMsg.value = err?.data?.message ?? '註冊失敗，請稍後再試。'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <main id="main">
    <section class="tallsection clr">
      <div class="restrict-wide allpadding">
        <!-- 標題（與購物車頁一致：h1 + direct-line）-->
        <div class="centered">
          <h1>會員註冊</h1>
          <div class="direct-line"></div>
        </div>

        <form class="register-form formstyle card" @submit.prevent="submit">
          <p class="ssl-note descript">🔒 本站採用 SSL 加密傳輸，請安心填寫。</p>

          <div class="field">
            <label><span class="must">*</span>手機號碼</label>
            <input
              v-model="form.mobile"
              type="tel"
              required
              autocomplete="username"
              class="input"
              maxlength="10"
              placeholder="例：0987654321"
            >
            <p class="descript hint">手機號碼將成為您的會員帳號，請輸入純數字。</p>
          </div>

          <div class="field">
            <label><span class="must">*</span>姓名</label>
            <input v-model="form.name" required class="input" maxlength="20" placeholder="請輸入姓名">
          </div>

          <div class="field">
            <label><span class="must">*</span>電子郵件</label>
            <input v-model="form.email" type="email" required class="input" placeholder="example@mail.com">
          </div>

          <div class="field">
            <label><span class="must">*</span>密碼</label>
            <input
              v-model="form.password"
              type="password"
              required
              autocomplete="new-password"
              class="input"
              maxlength="20"
              placeholder="6～20 字元"
            >
          </div>

          <div class="field">
            <label><span class="must">*</span>確認密碼</label>
            <input
              v-model="form.confirmPassword"
              type="password"
              required
              autocomplete="new-password"
              class="input"
              maxlength="20"
              placeholder="再次輸入密碼"
            >
          </div>

          <p v-if="errorMsg" class="submit-err">{{ errorMsg }}</p>

          <button type="submit" class="btn basic submit-btn" :disabled="loading">
            {{ loading ? '送出中…' : '立即註冊' }}
          </button>

          <p class="to-login descript">
            已有帳號？<a href="/Member/Login">立即登入</a>
          </p>
        </form>
      </div>
    </section>
  </main>
</template>

<style scoped>
/* 與結帳頁一致的「秀氣 整齊」卡片版型：置中單卡、細線、淺字、留白收斂視覺重量。
   品牌色 teal #26b7bc / 深 #1d8e92。 */
.register-form {
  max-width: 460px;
  margin: 0 auto;
  border: 1px solid #eee;
  border-radius: 6px;
  padding: 1.8em 2em 2em;
  background: #fff;
}
.ssl-note { margin: 0 0 1.4em; color: #8a8a8a; }

.field { margin-bottom: 1.2em; }
.field > label { display: block; font-size: .9em; color: #666; margin-bottom: .45em; }
.must { color: #ea5520; margin-right: .25em; }
.opt-note { font-size: .82em; color: #aaa; margin-left: .3em; }

.register-form .input {
  width: 100%; box-sizing: border-box; height: 42px; padding: 0 .85em;
  border: 1px solid #e2e2e2; border-radius: 4px; color: #444; font-size: .95em;
  background: #fff; transition: border-color .2s;
}
.register-form .input:focus { outline: none; border-color: #26b7bc; }
.hint { font-size: .8em; color: #aaa; margin: .4em 0 0; line-height: 1.5; }

.submit-err { color: #d0021b; font-size: .85em; margin: 0 0 .9em; }

.submit-btn {
  display: block; width: 100%; box-sizing: border-box; text-align: center;
  margin: 1.6em 0 0;
}

.to-login { text-align: center; margin: 1.3em 0 0; color: #888; }
.to-login a { color: #1d8e92; text-decoration: underline; }

@media (max-width: 600px) {
  .register-form { padding: 1.4em 1.2em 1.6em; }
}
</style>
