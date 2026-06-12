<script setup lang="ts">
useHead({ title: '忘記密碼' })

const config = useRuntimeConfig()
const memberAuth = useMemberAuthStore()

// 已登入者不需重設流程，導回會員中心。
if (memberAuth.isAuthenticated) {
  await navigateTo('/Member/Center')
}

// 對齊舊系統：輸入手機 + Email，比對相符後系統寄送新密碼至信箱，
// 請使用者登入後自行修改密碼。對應後端 POST /auth/forgot-password。
const mobile = ref('')
const email = ref('')

const errorMsg = ref('')
const okMsg = ref('')
const loading = ref(false)

async function submit() {
  errorMsg.value = ''
  okMsg.value = ''
  if (!/^09\d{8}$/.test(mobile.value.trim())) {
    errorMsg.value = '請輸入正確的手機格式，如：0987654321。'
    return
  }
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value.trim())) {
    errorMsg.value = 'Email 格式錯誤。'
    return
  }
  loading.value = true
  try {
    const res = await $fetch<{ message: string }>(
      `${config.public.apiBase}/auth/forgot-password`,
      {
        method: 'POST',
        body: { mobile: mobile.value.trim(), email: email.value.trim() },
      },
    )
    okMsg.value = res.message ?? '密碼已寄出，請至您的Email信箱收信！'
  } catch (e: unknown) {
    // 錯誤統一為 { error: { code, message } }（ApiErrorResponse）。
    const err = e as { data?: { error?: { message?: string }, message?: string } }
    errorMsg.value = err?.data?.error?.message ?? err?.data?.message ?? '查無此會員或發送失敗，請稍後再試。'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <main id="main">
    <section class="forget-wrap clr">
      <div class="forget-card">
        <header class="panel-head">
          <h1>忘記密碼</h1>
          <p class="sub">
            請輸入您註冊的手機號碼及 Email，系統將寄送新密碼至您的信箱，
            請登入會員後更改您的密碼，謝謝！
          </p>
        </header>

        <form class="forget-form" @submit.prevent="submit">
          <div class="field">
            <label for="fg-mobile">手機號碼</label>
            <input
              id="fg-mobile"
              v-model="mobile"
              type="tel"
              required
              autocomplete="username"
              maxlength="10"
              placeholder="例：0987654321"
            >
          </div>

          <div class="field">
            <label for="fg-email">Email</label>
            <input
              id="fg-email"
              v-model="email"
              type="email"
              required
              autocomplete="email"
              placeholder="example@mail.com"
            >
          </div>

          <transition name="fade">
            <p v-if="errorMsg" class="msg error">{{ errorMsg }}</p>
          </transition>
          <transition name="fade">
            <p v-if="okMsg" class="msg ok">{{ okMsg }}</p>
          </transition>

          <button type="submit" class="submit-btn" :disabled="loading || !!okMsg">
            {{ loading ? '處理中…' : '確定送出' }}
          </button>
        </form>

        <p class="to-login">
          想起密碼了？<NuxtLink to="/Member/Login">返回登入</NuxtLink>
        </p>
      </div>
    </section>
  </main>
</template>

<style scoped>
.forget-wrap {
  display: flex;
  justify-content: center;
  padding: 4rem 1.25rem 5rem;
  background: linear-gradient(160deg, #f4fbfb 0%, #ffffff 55%);
}

.forget-card {
  width: 100%;
  max-width: 440px;
  padding: 3rem 2.75rem;
  background: #fff;
  border-radius: 16px;
  box-shadow: 0 18px 48px -22px rgba(21, 100, 103, 0.45);
}

.panel-head h1 {
  margin: 0;
  font-size: 1.6rem;
  letter-spacing: 0.06em;
  color: #156467;
}

.panel-head .sub {
  margin: 0.6rem 0 0;
  font-size: 0.9rem;
  line-height: 1.7;
  color: #9b9b9b;
}

.forget-form {
  margin-top: 2rem;
}

.field {
  margin-bottom: 1.25rem;
}

.field label {
  display: block;
  margin-bottom: 0.4rem;
  font-size: 0.85rem;
  letter-spacing: 0.04em;
  color: #3e3e3e;
}

.field input {
  width: 100%;
  box-sizing: border-box;
  padding: 0.7rem 0.9rem;
  font-size: 0.95rem;
  color: #393939;
  border: 1px solid #e1e1e1;
  border-radius: 9px;
  background: #fafafa;
  transition: border-color 0.2s, box-shadow 0.2s, background 0.2s;
}

.field input:focus {
  outline: none;
  border-color: #26b7bc;
  background: #fff;
  box-shadow: 0 0 0 3px rgba(38, 183, 188, 0.15);
}

.msg {
  margin: 0 0 1rem;
  padding: 0.6rem 0.8rem;
  font-size: 0.85rem;
  border-radius: 8px;
}

.msg.error { color: #d0021b; background: #fdecec; }
.msg.ok { color: #156467; background: #e6f6f6; }

.submit-btn {
  width: 100%;
  padding: 0.8rem;
  font-size: 1rem;
  letter-spacing: 0.12em;
  color: #fff;
  background: #26b7bc;
  border: none;
  border-radius: 9px;
  cursor: pointer;
  transition: background 0.2s, transform 0.1s, box-shadow 0.2s;
  box-shadow: 0 10px 20px -10px rgba(38, 183, 188, 0.8);
}

.submit-btn:hover:not(:disabled) { background: #1d8e92; }
.submit-btn:active:not(:disabled) { transform: translateY(1px); }
.submit-btn:disabled { opacity: 0.6; cursor: default; }

.to-login {
  margin: 1.6rem 0 0;
  text-align: center;
  font-size: 0.85rem;
  color: #888;
}

.to-login a {
  color: #1d8e92;
  text-decoration: underline;
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

@media (max-width: 720px) {
  .forget-wrap {
    padding: 2.5rem 1rem 3.5rem;
  }

  .forget-card {
    padding: 2.5rem 1.75rem 2rem;
  }
}
</style>
