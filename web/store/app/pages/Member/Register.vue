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

async function submit() {
  errorMsg.value = ''
  if (form.password !== form.confirmPassword) {
    errorMsg.value = '兩次輸入的密碼不一致。'
    return
  }
  loading.value = true
  try {
    await $fetch(`${config.public.apiBase}/auth/register`, {
      method: 'POST',
      // API 以 camelCase 大小寫敏感反序列化；PascalCase 會被當缺欄位回 400（同 login/profile 慣例）。
      body: {
        mobile: form.mobile.trim(),
        name: form.name.trim(),
        email: form.email.trim() || undefined,
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
      <div class="restrict-wide">
        <div class="centered allpadding" style="max-width:420px; margin:0 auto;">
          <h1>會員註冊</h1>
          <form @submit.prevent="submit" style="display:flex; flex-direction:column; gap:1rem; margin-top:1.5rem;">
            <div>
              <label style="display:block; margin-bottom:0.25rem;">手機號碼 <span style="color:red;">*</span></label>
              <input
                v-model="form.mobile"
                type="tel"
                required
                autocomplete="username"
                class="form-input"
                style="width:100%;"
                placeholder="09XXXXXXXX"
              />
            </div>
            <div>
              <label style="display:block; margin-bottom:0.25rem;">姓名 <span style="color:red;">*</span></label>
              <input
                v-model="form.name"
                required
                class="form-input"
                style="width:100%;"
              />
            </div>
            <div>
              <label style="display:block; margin-bottom:0.25rem;">Email（選填）</label>
              <input
                v-model="form.email"
                type="email"
                class="form-input"
                style="width:100%;"
              />
            </div>
            <div>
              <label style="display:block; margin-bottom:0.25rem;">密碼 <span style="color:red;">*</span></label>
              <input
                v-model="form.password"
                type="password"
                required
                autocomplete="new-password"
                class="form-input"
                style="width:100%;"
              />
            </div>
            <div>
              <label style="display:block; margin-bottom:0.25rem;">確認密碼 <span style="color:red;">*</span></label>
              <input
                v-model="form.confirmPassword"
                type="password"
                required
                autocomplete="new-password"
                class="form-input"
                style="width:100%;"
              />
            </div>
            <p v-if="errorMsg" style="color:red; margin:0;">{{ errorMsg }}</p>
            <button type="submit" :disabled="loading" class="btn outline-btn solidhover" style="width:100%;">
              {{ loading ? '送出中…' : '立即註冊' }}
            </button>
          </form>
          <p style="margin-top:1.5rem; text-align:center;">
            已有帳號？<a href="/Member/Login">立即登入</a>
          </p>
        </div>
      </div>
    </section>
  </main>
</template>
