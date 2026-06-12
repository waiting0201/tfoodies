<script setup lang="ts">
useHead({ title: '會員登入' })

const memberAuth = useMemberAuthStore()
const cart = useCartStore()

// If already logged in, redirect immediately
if (memberAuth.isAuthenticated) {
  await navigateTo('/Member/Center')
}

const mobile = ref('')
const password = ref('')
const remember = ref(true)
const errorMsg = ref('')
const loading = ref(false)

async function submit() {
  errorMsg.value = ''
  loading.value = true
  try {
    await memberAuth.login(mobile.value.trim(), password.value, remember.value)
    // 登入後一律導向會員中心（即使購物車有商品，使用者可從會員中心或 header 再前往結帳）。
    await navigateTo('/Member/Center')
  } catch (e: unknown) {
    const err = e as { data?: { message?: string } }
    errorMsg.value = err?.data?.message ?? '密碼錯誤！請再次輸入正確手機號碼與密碼。'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <main id="main">
    <section class="login-wrap clr">
      <div class="login-card">
        <!-- 會員登入 -->
        <div class="login-panel">
          <header class="panel-head">
            <h1>會員登入</h1>
            <p class="sub">歡迎回到食在呼，請輸入您的帳號密碼。</p>
          </header>

          <form class="login-form" @submit.prevent="submit">
            <div class="field">
              <label for="login-mobile">手機號碼</label>
              <input
                id="login-mobile"
                v-model="mobile"
                type="tel"
                required
                autocomplete="username"
                placeholder="09XXXXXXXX"
              >
            </div>

            <div class="field">
              <label for="login-pwd">密碼</label>
              <input
                id="login-pwd"
                v-model="password"
                type="password"
                required
                autocomplete="current-password"
                placeholder="請輸入密碼"
              >
            </div>

            <transition name="fade">
              <p v-if="errorMsg" class="error">{{ errorMsg }}</p>
            </transition>

            <div class="form-row">
              <label class="remember">
                <input v-model="remember" type="checkbox">
                <span>記住帳號</span>
              </label>
              <NuxtLink to="/Member/Forget" class="forget">忘記密碼？</NuxtLink>
            </div>

            <button type="submit" class="submit-btn" :disabled="loading">
              {{ loading ? '登入中…' : '登入' }}
            </button>
          </form>
        </div>

        <!-- 首次購物 -->
        <aside class="signup-panel">
          <div class="signup-inner">
            <h2>首次購物</h2>
            <p>
              商品加入購物車後，即可直接結帳購物。<br>
              成為會員可享訂單查詢、預購與專屬優惠。
            </p>
            <NuxtLink to="/Member/Register" class="ghost-btn">立即註冊</NuxtLink>
            <NuxtLink v-if="cart.count > 0" to="/Cart" class="ghost-btn cart">
              前往結帳（{{ cart.count }} 件）
            </NuxtLink>
          </div>
        </aside>
      </div>
    </section>
  </main>
</template>

<style scoped>
.login-wrap {
  display: flex;
  justify-content: center;
  padding: 4rem 1.25rem 5rem;
  background: linear-gradient(160deg, #f4fbfb 0%, #ffffff 55%);
}

.login-card {
  display: grid;
  grid-template-columns: 1.1fr 0.9fr;
  width: 100%;
  max-width: 860px;
  background: #fff;
  border-radius: 16px;
  overflow: hidden;
  box-shadow: 0 18px 48px -22px rgba(21, 100, 103, 0.45);
}

/* ---- 會員登入 ---- */
.login-panel {
  padding: 3rem 2.75rem;
}

.panel-head h1 {
  margin: 0;
  font-size: 1.6rem;
  letter-spacing: 0.06em;
  color: #156467;
}

.panel-head .sub {
  margin: 0.5rem 0 0;
  font-size: 0.9rem;
  color: #9b9b9b;
}

.login-form {
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

.error {
  margin: 0 0 1rem;
  padding: 0.6rem 0.8rem;
  font-size: 0.85rem;
  color: #d0021b;
  background: #fdecec;
  border-radius: 8px;
}

.form-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1.6rem;
  font-size: 0.85rem;
}

.remember {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  color: #3e3e3e;
  cursor: pointer;
}

.remember input {
  width: 16px;
  height: 16px;
  accent-color: #26b7bc;
}

.forget {
  color: #9b9b9b;
  text-decoration: none;
  transition: color 0.2s;
}

.forget:hover {
  color: #26b7bc;
}

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

.submit-btn:hover:not(:disabled) {
  background: #1d8e92;
}

.submit-btn:active:not(:disabled) {
  transform: translateY(1px);
}

.submit-btn:disabled {
  opacity: 0.6;
  cursor: default;
}

/* ---- 首次購物 ---- */
.signup-panel {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 3rem 2.5rem;
  color: #fff;
  background: linear-gradient(155deg, #26b7bc 0%, #156467 100%);
}

.signup-inner {
  text-align: center;
  max-width: 280px;
}

.signup-inner h2 {
  margin: 0 0 0.9rem;
  font-size: 1.35rem;
  letter-spacing: 0.08em;
  color: #fff;
}

.signup-inner p {
  margin: 0 0 1.8rem;
  font-size: 0.88rem;
  line-height: 1.8;
  color: rgba(255, 255, 255, 0.88);
}

.ghost-btn {
  display: block;
  padding: 0.7rem 1rem;
  margin: 0.7rem auto 0;
  font-size: 0.95rem;
  letter-spacing: 0.1em;
  color: #fff;
  text-decoration: none;
  border: 1.5px solid rgba(255, 255, 255, 0.85);
  border-radius: 9px;
  transition: background 0.2s, color 0.2s;
}

.ghost-btn:hover {
  background: #fff;
  color: #156467;
}

.ghost-btn.cart {
  background: #ea5520;
  border-color: #ea5520;
}

.ghost-btn.cart:hover {
  background: #fff;
  color: #ea5520;
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

/* ---- 響應式 ---- */
@media (max-width: 720px) {
  .login-wrap {
    padding: 2.5rem 1rem 3.5rem;
  }

  .login-card {
    grid-template-columns: 1fr;
    max-width: 440px;
  }

  .login-panel {
    padding: 2.5rem 1.75rem 2rem;
  }

  .signup-panel {
    padding: 2.25rem 1.75rem;
  }
}
</style>
