<script setup lang="ts">
useHead({ title: '會員登入' })

const memberAuth = useMemberAuthStore()

// If already logged in, redirect immediately
if (memberAuth.isAuthenticated) {
  await navigateTo('/Member/Center')
}

const mobile = ref('')
const password = ref('')
const errorMsg = ref('')
const loading = ref(false)

async function submit() {
  errorMsg.value = ''
  loading.value = true
  try {
    await memberAuth.login(mobile.value.trim(), password.value)
    await navigateTo('/Member/Center')
  } catch (e: unknown) {
    const err = e as { data?: { message?: string } }
    errorMsg.value = err?.data?.message ?? '登入失敗，請確認手機號碼與密碼。'
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
          <h1>會員登入</h1>
          <form @submit.prevent="submit" style="display:flex; flex-direction:column; gap:1rem; margin-top:1.5rem;">
            <div>
              <label style="display:block; margin-bottom:0.25rem;">手機號碼</label>
              <input
                v-model="mobile"
                type="tel"
                required
                autocomplete="username"
                class="form-input"
                style="width:100%;"
                placeholder="09XXXXXXXX"
              />
            </div>
            <div>
              <label style="display:block; margin-bottom:0.25rem;">密碼</label>
              <input
                v-model="password"
                type="password"
                required
                autocomplete="current-password"
                class="form-input"
                style="width:100%;"
              />
            </div>
            <p v-if="errorMsg" style="color:red; margin:0;">{{ errorMsg }}</p>
            <button type="submit" :disabled="loading" class="btn outline-btn solidhover" style="width:100%;">
              {{ loading ? '登入中…' : '登入' }}
            </button>
          </form>
          <p style="margin-top:1.5rem; text-align:center;">
            還沒有帳號？<a href="/Member/Register">立即註冊</a>
          </p>
        </div>
      </div>
    </section>
  </main>
</template>
